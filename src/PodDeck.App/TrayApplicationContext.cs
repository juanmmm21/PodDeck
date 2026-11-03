using System.Runtime.InteropServices;
using PodDeck.Bluetooth;
using PodDeck.Core.Alerts;
using PodDeck.Core.Devices;
using PodDeck.Core.Playback;
using PodDeck.Core.Presentation;
using PodDeck.Core.Protocol;
using PodDeck.Core.Settings;
using PodDeck.Core.Tracking;
using Windows.Devices.Bluetooth;

namespace PodDeck.App;

/// <summary>
/// La app entera vive en el icono de la bandeja: escucha los anuncios, decide qué AirPods son los
/// tuyos y refresca icono, tooltip y menú cuando cambia algo. Al abrir el estuche saca la ventanita
/// con la batería, que también se abre con un clic en el icono. Al quitarse un auricular pausa lo que
/// suene y lo reanuda al volver a ponérselo, y avisa con una notificación cuando la batería baja del
/// umbral elegido en el menú. Lo elegido en el menú se guarda y puede arrancar con Windows.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    // Corto porque el cierre de la tapa se confirma con el paso del tiempo y la ventana debe irse al momento.
    private static readonly TimeSpan HousekeepingInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(10);
    private const int LowBatteryNotificationMilliseconds = 10_000;

    private readonly SynchronizationContext _uiContext;
    private readonly AppleAdvertisementScanner _scanner = new();
    private readonly PairedAirPodsWatcher _pairedWatcher = new();
    private readonly AirPodsTracker _tracker = new(TimeProvider.System);
    private readonly CaseLidWatcher _lidWatcher = new(TimeProvider.System);
    private readonly EarDetector _earDetector = new();
    private readonly AutoPausePolicy _autoPause = new(TimeProvider.System);
    private readonly MediaPlaybackController _media = new();
    private readonly LowBatteryMonitor _lowBattery = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly StartupRegistration _startup = new();
    private readonly CasePopupForm _popup = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripMenuItem _titleItem = new() { Enabled = false };
    private readonly ToolStripSeparator _detailsSeparator = new();
    private readonly ToolStripMenuItem _autoPauseItem = new("Pause media when an AirPod is removed") { CheckOnClick = true };
    private readonly ToolStripMenuItem _lowBatteryMenu = new("Low battery alert");
    private readonly ToolStripMenuItem _startupItem = new("Start with Windows");
    private readonly List<ToolStripMenuItem> _detailItems = [];
    private readonly System.Windows.Forms.Timer _housekeepingTimer = new();
    private Icon? _currentIcon;
    private string? _scannerProblem;
    private DateTimeOffset _nextRestartAttempt = DateTimeOffset.MinValue;
    private AppSettings _settings;

    public TrayApplicationContext()
    {
        _uiContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("The tray context must be created on the WinForms UI thread.");

        _settings = _settingsStore.Load();
        _autoPause.IsEnabled = _settings.AutoPauseEnabled;
        _autoPauseItem.Checked = _settings.AutoPauseEnabled;
        ApplyLowBatteryThreshold(_settings.LowBatteryThresholdPercent);

        _menu.Items.Add(_titleItem);
        _menu.Items.Add(_detailsSeparator);
        _menu.Items.Add(_autoPauseItem);
        BuildLowBatteryMenu();
        _menu.Items.Add(_lowBatteryMenu);
        _startupItem.Enabled = _startup.IsAvailable;
        _startupItem.Checked = _startup.IsEnabled();
        _startupItem.Click += OnStartupClicked;
        _menu.Items.Add(_startupItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Exit", image: null, (_, _) => ExitThread()));

        _notifyIcon = new NotifyIcon { ContextMenuStrip = _menu, Visible = true };
        _notifyIcon.MouseClick += OnNotifyIconClick;
        _autoPauseItem.CheckedChanged += OnAutoPauseToggled;

        _scanner.AdvertisementReceived += OnAdvertisementReceived;
        _scanner.Stopped += OnScannerStopped;
        _pairedWatcher.PairedProductsChanged += OnPairedProductsChanged;
        _tracker.CurrentChanged += (_, status) =>
        {
            Render();
            FollowLid(status);
            WarnIfLow(status);
            if (status is null)
            {
                _earDetector.Reset();
            }
        };
        // La detección de oído va con cada anuncio y no con cada cambio: confirmar con dos anuncios
        // seguidos es más rápido que esperar a un temporizador.
        _tracker.StatusReported += (_, status) => FollowEars(status);

        _housekeepingTimer.Interval = (int)HousekeepingInterval.TotalMilliseconds;
        _housekeepingTimer.Tick += (_, _) => Housekeep();
        _housekeepingTimer.Start();

        TryStartPairedWatcher();
        TryStartScanner();
        Render();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _housekeepingTimer.Dispose();
            _scanner.AdvertisementReceived -= OnAdvertisementReceived;
            _scanner.Stopped -= OnScannerStopped;
            _scanner.Dispose();
            _pairedWatcher.PairedProductsChanged -= OnPairedProductsChanged;
            _pairedWatcher.Dispose();
            _popup.Dispose();
            _notifyIcon.MouseClick -= OnNotifyIconClick;
            _autoPauseItem.CheckedChanged -= OnAutoPauseToggled;
            _lowBatteryMenu.DropDownItemClicked -= OnLowBatteryChoiceClicked;
            _startupItem.Click -= OnStartupClicked;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _currentIcon?.Dispose();
        }

        base.Dispose(disposing);
    }

    // Llega desde un hilo de WinRT: se decodifica ahí y solo el resultado salta al hilo de la interfaz,
    // que es el único que toca el tracker.
    private void OnAdvertisementReceived(object? sender, AppleAdvertisement advertisement)
    {
        if (ProximityPairingParser.TryParse(advertisement.Data.Span, out AirPodsStatus? status))
        {
            short signal = advertisement.SignalStrengthDbm;
            _uiContext.Post(_ => _tracker.Report(status, signal), null);
        }
    }

    private void OnPairedProductsChanged(object? sender, IReadOnlySet<ushort> productIds) =>
        _uiContext.Post(_ => _tracker.SetPairedProducts(productIds), null);

    private void TryStartPairedWatcher()
    {
        try
        {
            _pairedWatcher.Start();
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Sin la lista de emparejados la app sigue funcionando con el par más cercano, como antes.
            System.Diagnostics.Trace.TraceWarning($"Paired device watcher failed to start: {exception.Message}");
        }
    }

    private void OnScannerStopped(object? sender, BluetoothError error)
    {
        if (error == BluetoothError.Success)
        {
            return;
        }

        _uiContext.Post(_ =>
        {
            _scannerProblem = Describe(error);
            _nextRestartAttempt = DateTimeOffset.UtcNow + RestartDelay;
            Render();
        }, null);
    }

    private void Housekeep()
    {
        _tracker.ExpireStale();
        // El cierre de la tapa se confirma con el tiempo, aunque el tracker no cambie.
        FollowLid(_tracker.Current);
        if (_scannerProblem is not null && DateTimeOffset.UtcNow >= _nextRestartAttempt)
        {
            TryStartScanner();
            Render();
        }
    }

    private void TryStartScanner()
    {
        try
        {
            _scanner.Start();
            _scannerProblem = null;
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Sin adaptador o con el Bluetooth apagado el watcher no arranca: se reintenta en silencio,
            // porque el usuario puede encenderlo en cualquier momento.
            _scannerProblem = "Bluetooth unavailable — retrying";
            _nextRestartAttempt = DateTimeOffset.UtcNow + RestartDelay;
        }
    }

    private void FollowLid(AirPodsStatus? status)
    {
        switch (_lidWatcher.Update(status))
        {
            case LidChange.Opened when status is not null:
                _popup.ShowStatus(status);
                break;
            case LidChange.Closed:
                _popup.Hide();
                break;
            default:
                if (status is not null)
                {
                    _popup.UpdateStatus(status);
                }

                break;
        }
    }

    private void FollowEars(AirPodsStatus status)
    {
        if (_earDetector.Observe(status) is not EarChange change)
        {
            return;
        }

        // Se mira antes de decidir: si ya había una pausa pendiente, lo que se pause ahora se suma a ella.
        bool keepPrevious = _autoPause.HasPendingResume;
        switch (_autoPause.Decide(change))
        {
            case PlaybackAction.Pause:
                _ = PauseMediaAsync(keepPrevious);
                break;
            case PlaybackAction.Resume:
                _ = ResumeMediaAsync();
                break;
            case PlaybackAction.None:
                break;
            default:
                throw new InvalidOperationException("Unhandled playback action.");
        }
    }

    // Las continuaciones vuelven al hilo de la interfaz, que es el único que toca la política.
    private async Task PauseMediaAsync(bool keepPrevious)
    {
        try
        {
            if (await _media.PauseAllPlayingAsync(keepPrevious))
            {
                _autoPause.ConfirmPaused();
            }
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException)
        {
            System.Diagnostics.Trace.TraceWarning($"Could not pause media: {exception.Message}");
        }
    }

    private async Task ResumeMediaAsync()
    {
        try
        {
            await _media.ResumePausedAsync();
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException)
        {
            System.Diagnostics.Trace.TraceWarning($"Could not resume media: {exception.Message}");
        }
    }

    private void WarnIfLow(AirPodsStatus? status)
    {
        IReadOnlyList<LowBatteryAlert> alerts = _lowBattery.Update(status);
        if (status is not null && alerts.Count > 0)
        {
            // En Windows 10 y 11 el globo del icono de bandeja se muestra como notificación del sistema.
            _notifyIcon.ShowBalloonTip(LowBatteryNotificationMilliseconds, StatusFormatter.LowBatteryTitle(status),
                StatusFormatter.LowBatteryText(alerts), ToolTipIcon.Warning);
        }
    }

    private void BuildLowBatteryMenu()
    {
        var off = new ToolStripMenuItem("Off") { Tag = AppSettings.LowBatteryOff, Checked = !_lowBattery.IsEnabled };
        _lowBatteryMenu.DropDownItems.Add(off);
        foreach (int threshold in LowBatteryMonitor.ThresholdChoices)
        {
            _lowBatteryMenu.DropDownItems.Add(new ToolStripMenuItem($"At {threshold}%")
            {
                Tag = threshold,
                Checked = _lowBattery.IsEnabled && threshold == _lowBattery.ThresholdPercent,
            });
        }

        _lowBatteryMenu.DropDownItemClicked += OnLowBatteryChoiceClicked;
    }

    private void OnLowBatteryChoiceClicked(object? sender, ToolStripItemClickedEventArgs e)
    {
        if (e.ClickedItem?.Tag is not int threshold)
        {
            return;
        }

        ApplyLowBatteryThreshold(threshold);
        SaveSettings(_settings with { LowBatteryThresholdPercent = threshold });
        foreach (ToolStripMenuItem item in _lowBatteryMenu.DropDownItems.OfType<ToolStripMenuItem>())
        {
            item.Checked = ReferenceEquals(item, e.ClickedItem);
        }

        WarnIfLow(_tracker.Current);
    }

    private void ApplyLowBatteryThreshold(int threshold)
    {
        _lowBattery.IsEnabled = threshold != AppSettings.LowBatteryOff;
        if (_lowBattery.IsEnabled)
        {
            _lowBattery.ThresholdPercent = threshold;
        }
    }

    private void OnAutoPauseToggled(object? sender, EventArgs e)
    {
        _autoPause.IsEnabled = _autoPauseItem.Checked;
        if (!_autoPauseItem.Checked)
        {
            _media.Forget();
        }

        SaveSettings(_settings with { AutoPauseEnabled = _autoPauseItem.Checked });
    }

    private void SaveSettings(AppSettings settings)
    {
        _settings = settings;
        // Si no se puede guardar, el cambio sigue valiendo hasta que se cierre la app.
        _ = _settingsStore.TrySave(settings);
    }

    private void OnStartupClicked(object? sender, EventArgs e)
    {
        bool wanted = !_startupItem.Checked;
        if (_startup.TrySet(wanted))
        {
            _startupItem.Checked = _startup.IsEnabled();
        }
        else
        {
            _notifyIcon.ShowBalloonTip(LowBatteryNotificationMilliseconds, "PodDeck",
                "Windows did not let PodDeck change its startup entry.", ToolTipIcon.Error);
        }
    }

    private void OnNotifyIconClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _tracker.Current is AirPodsStatus status)
        {
            _popup.ShowStatus(status);
        }
    }

    private void Render()
    {
        AirPodsStatus? status = _tracker.Current;

        Icon nextIcon = BatteryIconRenderer.Render(StatusFormatter.IconPercent(status), SystemInformation.SmallIconSize);
        _notifyIcon.Icon = nextIcon;
        _currentIcon?.Dispose();
        _currentIcon = nextIcon;

        string tooltip = _scannerProblem is null ? StatusFormatter.Tooltip(status) : $"PodDeck — {_scannerProblem}";
        _notifyIcon.Text = tooltip.Length <= StatusFormatter.MaxTooltipLength
            ? tooltip
            : tooltip[..StatusFormatter.MaxTooltipLength];

        _titleItem.Text = _scannerProblem ?? status?.DisplayName ?? "No AirPods nearby";
        foreach (ToolStripMenuItem item in _detailItems)
        {
            _menu.Items.Remove(item);
            item.Dispose();
        }

        _detailItems.Clear();
        IReadOnlyList<string> lines = status is null ? [] : StatusFormatter.DetailLines(status);
        int insertAt = _menu.Items.IndexOf(_detailsSeparator);
        foreach (string line in lines)
        {
            var item = new ToolStripMenuItem(line) { Enabled = false };
            _menu.Items.Insert(insertAt++, item);
            _detailItems.Add(item);
        }
    }

    private static string Describe(BluetoothError error) => error switch
    {
        BluetoothError.RadioNotAvailable => "Bluetooth is off — retrying",
        BluetoothError.DeviceNotConnected or BluetoothError.NotSupported => "No Bluetooth LE adapter found",
        BluetoothError.DisabledByPolicy or BluetoothError.DisabledByUser => "Bluetooth access is disabled",
        _ => $"Bluetooth error ({error}) — retrying",
    };
}
