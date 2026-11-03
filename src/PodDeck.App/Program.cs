using PodDeck.Core.Alerts;
using PodDeck.Core.Devices;
using PodDeck.Core.Presentation;

namespace PodDeck.App;

internal static class Program
{
    // Un mutex con nombre por sesión evita dos iconos en la bandeja si se abre la app dos veces.
    private const string SingleInstanceMutexName = @"Local\PodDeck.SingleInstance";

    private static readonly AirPodsStatus PreviewStatus = new(
        AirPodsModel.AirPodsPro3,
        0x2027,
        new PodStatus(new BatteryReading(80, true), IsInEar: false, IsInCase: true),
        new PodStatus(new BatteryReading(70, true), IsInEar: false, IsInCase: true),
        new BatteryReading(15, false),
        IsLidOpen: true);

    [STAThread]
    private static int Main(string[] args)
    {
        // Enseña la ventana del estuche con datos de ejemplo, sin Bluetooth: sirve para revisarla a ojo.
        if (args.Contains("--preview-popup"))
        {
            ApplicationConfiguration.Initialize();
            using var preview = new CasePopupForm();
            preview.VisibleChanged += (_, _) =>
            {
                if (!preview.Visible)
                {
                    Application.ExitThread();
                }
            };
            preview.ShowStatus(PreviewStatus);
            Application.Run();
            return 0;
        }

        // Lanza el aviso de batería baja con datos de ejemplo y sale: sirve para revisarlo a ojo.
        if (args.Contains("--preview-alert"))
        {
            ApplicationConfiguration.Initialize();
            using Icon batteryIcon = BatteryIconRenderer.Render(10, SystemInformation.SmallIconSize);
            using var icon = new NotifyIcon { Icon = batteryIcon, Visible = true };
            icon.ShowBalloonTip(10_000, StatusFormatter.LowBatteryTitle(PreviewStatus),
                StatusFormatter.LowBatteryText([new LowBatteryAlert(BatteryComponent.Left, 10), new LowBatteryAlert(BatteryComponent.Case, 20)]),
                ToolTipIcon.Warning);
            using var exitTimer = new System.Windows.Forms.Timer { Interval = 12_000 };
            exitTimer.Tick += (_, _) => Application.ExitThread();
            exitTimer.Start();
            Application.Run();
            icon.Visible = false;
            return 0;
        }

        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            return 0;
        }

        ApplicationConfiguration.Initialize();
        using var context = new TrayApplicationContext();
        Application.Run(context);
        return 0;
    }
}
