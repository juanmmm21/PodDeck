namespace PodDeck.App;

internal static class Program
{
    // Un mutex con nombre por sesión evita dos iconos en la bandeja si se abre la app dos veces.
    private const string SingleInstanceMutexName = @"Local\PodDeck.SingleInstance";

    [STAThread]
    private static int Main()
    {
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
