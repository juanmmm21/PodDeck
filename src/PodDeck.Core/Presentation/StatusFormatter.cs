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

    /// <summary>
    /// Indicadores de la ventana que salta al abrir el estuche: izquierdo, derecho y estuche, o uno
    /// solo para los cascos.
    /// </summary>
    public static IReadOnlyList<BatteryGauge> PopupGauges(AirPodsStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (status.Model.IsHeadphones())
        {
            return [new BatteryGauge("Battery", status.HeadphonesBattery)];
        }

        return
        [
            new BatteryGauge("Left", status.Left.Battery),
            new BatteryGauge("Right", status.Right.Battery),
            new BatteryGauge("Case", status.Case),
        ];
    }

    /// <summary>El número que se dibuja en el icono: la batería del auricular más bajo, o la de los cascos.</summary>
    public static int? IconPercent(AirPodsStatus? status) => status switch
    {
        null => null,
        { Model: var model } when model.IsHeadphones() => status.HeadphonesBattery.Percent,
        _ => status.LowestPodPercent,
    };

    private static string HeadphonesBattery(AirPodsStatus status) => Long(status.HeadphonesBattery);

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
