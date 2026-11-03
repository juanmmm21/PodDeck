using PodDeck.Core.Devices;

namespace PodDeck.Core.Presentation;

/// <summary>Textos que la app muestra en la bandeja, independientes de la interfaz para poder probarlos.</summary>
public static class StatusFormatter
{
    // Windows rechaza tooltips de icono de bandeja de más de 127 caracteres.
    public const int MaxTooltipLength = 127;

    public const string NotNearbyText = "PodDeck — no AirPods nearby";

    public static string Tooltip(AirPodsStatus? status)
    {
        if (status is null)
        {
            return NotNearbyText;
        }

        string text = status.Model.IsHeadphones()
            ? $"{status.DisplayName} — {HeadphonesBattery(status)}"
            : $"{status.DisplayName} — L {Short(status.Left.Battery)} · R {Short(status.Right.Battery)} · Case {Short(status.Case)}";
        return text.Length <= MaxTooltipLength ? text : text[..MaxTooltipLength];
    }

    /// <summary>Una línea por componente para el menú contextual.</summary>
    public static IReadOnlyList<string> DetailLines(AirPodsStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (status.Model.IsHeadphones())
        {
            return [$"Battery: {HeadphonesBattery(status)}"];
        }

        return
        [
            $"Left: {Pod(status.Left)}",
            $"Right: {Pod(status.Right)}",
            $"Case: {Long(status.Case)}" + (status.IsLidOpen ? " · lid open" : string.Empty),
        ];
    }

    /// <summary>El número que se dibuja en el icono: la batería del auricular más bajo, o la de los cascos.</summary>
    public static int? IconPercent(AirPodsStatus? status) => status switch
    {
        null => null,
        { Model: var model } when model.IsHeadphones() => HeadphonesReading(status).Percent,
        _ => status.LowestPodPercent,
    };

    // Los cascos solo informan por uno de los dos nibbles, y cuál depende del lado «primario».
    private static BatteryReading HeadphonesReading(AirPodsStatus status) =>
        status.Left.Battery.IsAvailable ? status.Left.Battery : status.Right.Battery;

    private static string HeadphonesBattery(AirPodsStatus status) => Long(HeadphonesReading(status));

    private static string Pod(PodStatus pod)
    {
        string where = pod.IsInEar ? " · in ear" : pod.IsInCase ? " · in case" : string.Empty;
        return Long(pod.Battery) + where;
    }

    private static string Short(BatteryReading reading) => reading.Percent switch
    {
        null => "--",
        int percent when reading.IsCharging => $"{percent}%⚡",
        int percent => $"{percent}%",
    };

    private static string Long(BatteryReading reading) => reading.Percent switch
    {
        null => "not reporting",
        int percent when reading.IsCharging => $"{percent}% (charging)",
        int percent => $"{percent}%",
    };
}
