namespace PodDeck.Core.Devices;

/// <summary>
/// Batería de un componente (auricular o estuche). <see cref="Percent"/> es null cuando el
/// componente no informa, por ejemplo un auricular guardado en un estuche cerrado.
/// </summary>
public readonly record struct BatteryReading(int? Percent, bool IsCharging)
{
    public static BatteryReading Unavailable { get; } = new(null, false);

    public bool IsAvailable => Percent.HasValue;

    public override string ToString() => Percent switch
    {
        null => "--",
        int percent when IsCharging => $"{percent}% (charging)",
        int percent => $"{percent}%",
    };
}
