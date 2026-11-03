using PodDeck.Core.Devices;

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
