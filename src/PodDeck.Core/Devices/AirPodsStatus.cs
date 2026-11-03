namespace PodDeck.Core.Devices;

/// <summary>Instantánea completa de unos AirPods decodificada de un anuncio de proximidad.</summary>
public sealed record AirPodsStatus(
    AirPodsModel Model,
    ushort ProductId,
    PodStatus Left,
    PodStatus Right,
    BatteryReading Case,
    bool IsLidOpen)
{
    /// <summary>Batería más baja de los auriculares que informan; la que decide cuándo hay que cargar.</summary>
    public int? LowestPodPercent
    {
        get
        {
            int?[] levels = [Left.Battery.Percent, Right.Battery.Percent];
            return levels.Where(level => level.HasValue).Min();
        }
    }

    /// <summary>
    /// Batería única de unos cascos (AirPods Max). Solo informan por uno de los dos nibbles, y cuál
    /// depende del lado «primario».
    /// </summary>
    public BatteryReading HeadphonesBattery => Left.Battery.IsAvailable ? Left.Battery : Right.Battery;

    public string DisplayName => Model == AirPodsModel.Unknown
        ? $"{Model.DisplayName()} (product 0x{ProductId:X4})"
        : Model.DisplayName();
}
