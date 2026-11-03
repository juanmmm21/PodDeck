using System.Runtime.InteropServices;
using PodDeck.Bluetooth;
using PodDeck.Core.Devices;
using PodDeck.Core.Presentation;
using PodDeck.Core.Protocol;
using PodDeck.Core.Tracking;
using Windows.Devices.Bluetooth;

namespace PodDeck.App;

/// <summary>
/// La app entera vive en el icono de la bandeja: escucha los anuncios, decide qué AirPods son los
/// tuyos y refresca icono, tooltip y menú cuando cambia algo.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan HousekeepingInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(10);

    private readonly SynchronizationContext _uiContext;
    private readonly AppleAdvertisementScanner _scanner = new();
    private readonly AirPodsTracker _tracker = new(TimeProvider.System);
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripMenuItem _titleItem = new() { Enabled = false };
    private readonly ToolStripSeparator _detailsSeparator = new();
    private readonly List<ToolStripMenuItem> _detailItems = [];
    private readonly System.Windows.Forms.Timer _housekeepingTimer = new();
    private Icon? _currentIcon;
    private string? _scannerProblem;
    private DateTimeOffset _nextRestartAttempt = DateTimeOffset.MinValue;

    public TrayApplicationContext()
    {
        _uiContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("The tray context must be created on the WinForms UI thread.");

        _menu.Items.Add(_titleItem);
        _menu.Items.Add(_detailsSeparator);
        _menu.Items.Add(new ToolStripMenuItem("Exit", image: null, (_, _) => ExitThread()));

        _notifyIcon = new NotifyIcon { ContextMenuStrip = _menu, Visible = true };

        _scanner.AdvertisementReceived += OnAdvertisementReceived;
        _scanner.Stopped += OnScannerStopped;
        _tracker.CurrentChanged += (_, _) => Render();

        _housekeepingTimer.Interval = (int)HousekeepingInterval.TotalMilliseconds;
        _housekeepingTimer.Tick += (_, _) => Housekeep();
        _housekeepingTimer.Start();

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
        if (ProximityPairingParser.TryParse(advertisement.Data.Span, out AirPodsStatus? status) && status is not null)
        {
            short signal = advertisement.SignalStrengthDbm;
            _uiContext.Post(_ => _tracker.Report(status, signal), null);
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
