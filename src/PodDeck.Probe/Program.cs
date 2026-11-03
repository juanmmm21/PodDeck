using System.Globalization;
using PodDeck.Bluetooth;
using PodDeck.Core.Devices;
using PodDeck.Core.Protocol;

// Herramienta de diagnóstico: imprime cada anuncio de AirPods que llega. Con --raw imprime también
// los anuncios de Apple que no son de proximidad, para investigar formatos nuevos. Con --paired solo
// lista los modelos de Apple emparejados con el PC, que es lo que usa la app para filtrar.
if (args.Contains("--paired"))
{
    return await ListPairedAsync();
}

bool includeRaw = args.Contains("--raw");
TimeSpan? duration = ParseDuration(args);

using var scanner = new AppleAdvertisementScanner();
using var finished = new CancellationTokenSource();
var output = new Lock();

scanner.AdvertisementReceived += (_, advertisement) =>
{
    bool parsed = ProximityPairingParser.TryParse(advertisement.Data.Span, out AirPodsStatus? status);
    if (!parsed && !includeRaw)
    {
        return;
    }

    string line = string.Create(CultureInfo.InvariantCulture,
        $"{advertisement.Timestamp:HH:mm:ss.fff} {advertisement.SignalStrengthDbm,4} dBm  {Convert.ToHexString(advertisement.Data.Span)}");
    if (status is not null)
    {
        line += Environment.NewLine + "    " + Describe(status);
    }

    lock (output)
    {
        Console.WriteLine(line);
    }
};
scanner.Stopped += (_, error) =>
{
    Console.Error.WriteLine($"Bluetooth scanner stopped: {error}");
    finished.Cancel();
};
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    finished.Cancel();
};

try
{
    scanner.Start();
}
catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
{
    Console.Error.WriteLine($"Could not start Bluetooth scanning: {exception.Message}");
    return 1;
}

Console.Error.WriteLine(duration is null
    ? "Listening for AirPods advertisements. Press Ctrl+C to stop."
    : $"Listening for AirPods advertisements for {duration.Value.TotalSeconds:0} s.");
try
{
    await Task.Delay(duration ?? Timeout.InfiniteTimeSpan, finished.Token);
}
catch (OperationCanceledException)
{
    // Ctrl+C o parada del escáner: salida normal.
}

scanner.Stop();
return 0;

static async Task<int> ListPairedAsync()
{
    using var watcher = new PairedAirPodsWatcher();
    var firstResult = new TaskCompletionSource<IReadOnlySet<ushort>>(TaskCreationOptions.RunContinuationsAsynchronously);
    watcher.PairedProductsChanged += (_, products) => firstResult.TrySetResult(products);
    watcher.Start();

    Task completed = await Task.WhenAny(firstResult.Task, Task.Delay(TimeSpan.FromSeconds(10)));
    if (completed != firstResult.Task)
    {
        Console.Error.WriteLine("Device enumeration did not finish within 10 s.");
        return 1;
    }

    IReadOnlySet<ushort> paired = await firstResult.Task;
    if (paired.Count == 0)
    {
        Console.WriteLine("No paired Apple audio devices.");
    }

    foreach (ushort productId in paired.Order())
    {
        Console.WriteLine($"0x{productId:X4}  {AirPodsModels.FromProductId(productId).DisplayName()}");
    }

    return 0;
}

static string Describe(AirPodsStatus status) =>
    $"{status.DisplayName}: L {Pod(status.Left)} | R {Pod(status.Right)} | case {status.Case}"
    + (status.IsLidOpen ? " | lid open" : string.Empty);

static string Pod(PodStatus pod) =>
    pod.Battery + (pod.IsInEar ? " in ear" : pod.IsInCase ? " in case" : string.Empty);

static TimeSpan? ParseDuration(string[] arguments)
{
    int index = Array.IndexOf(arguments, "--seconds");
    if (index < 0)
    {
        return null;
    }

    if (index + 1 >= arguments.Length
        || !int.TryParse(arguments[index + 1], CultureInfo.InvariantCulture, out int seconds)
        || seconds <= 0)
    {
        throw new ArgumentException("--seconds expects a positive integer.");
    }

    return TimeSpan.FromSeconds(seconds);
}
