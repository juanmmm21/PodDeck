using PodDeck.Core.Devices;

namespace PodDeck.Core.Alerts;

/// <summary>Componente con batería propia que puede avisar.</summary>
public enum BatteryComponent
{
    Left,
    Right,
    Case,
    Headphones,
}

/// <summary>Un componente acaba de bajar al umbral sin estar cargando.</summary>
public readonly record struct LowBatteryAlert(BatteryComponent Component, int Percent);

/// <summary>
/// Decide cuándo avisar de batería baja: una sola vez por componente y descarga. No es seguro entre
/// hilos, igual que <see cref="Tracking.AirPodsTracker"/>.
/// </summary>
/// <remarks>
/// Tras avisar, un componente solo vuelve a poder avisar cuando se le ve cargando. Subir de nuevo
/// por encima del umbral no basta: la batería llega en pasos del 10 % y una lectura que baila entre
/// 20 y 30 % repetiría el aviso cada pocos minutos. Una lectura que no llega (auricular en el estuche
/// cerrado) no cambia nada.
/// </remarks>
public sealed class LowBatteryMonitor
{
    public const int DefaultThresholdPercent = 20;

    /// <summary>Umbrales que ofrece el menú de la app.</summary>
    public static IReadOnlyList<int> ThresholdChoices { get; } = [10, 20, 30];

    private readonly HashSet<BatteryComponent> _alerted = [];
    private ushort? _productId;
    private int _thresholdPercent = DefaultThresholdPercent;

    /// <summary>Al cambiarlo se olvidan los avisos ya dados, para que el umbral nuevo pueda avisar.</summary>
    public int ThresholdPercent
    {
        get => _thresholdPercent;
        set
        {
            if (value is < 1 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "ThresholdPercent must be between 1 and 100.");
            }

            _thresholdPercent = value;
            _alerted.Clear();
        }
    }

    public bool IsEnabled { get; set; } = true;

    public IReadOnlyList<LowBatteryAlert> Update(AirPodsStatus? status)
    {
        if (status is null)
        {
            return [];
        }

        if (status.ProductId != _productId)
        {
            _productId = status.ProductId;
            _alerted.Clear();
        }

        var alerts = new List<LowBatteryAlert>();
        foreach ((BatteryComponent component, BatteryReading reading) in Readings(status))
        {
            if (reading.Percent is not int percent)
            {
                continue;
            }

            if (reading.IsCharging)
            {
                _alerted.Remove(component);
            }
            else if (IsEnabled && percent <= _thresholdPercent && _alerted.Add(component))
            {
                alerts.Add(new LowBatteryAlert(component, percent));
            }
        }

        return alerts;
    }

    private static IEnumerable<(BatteryComponent Component, BatteryReading Reading)> Readings(AirPodsStatus status)
    {
        if (status.Model.IsHeadphones())
        {
            yield return (BatteryComponent.Headphones, status.HeadphonesBattery);
            yield break;
        }

        yield return (BatteryComponent.Left, status.Left.Battery);
        yield return (BatteryComponent.Right, status.Right.Battery);
        yield return (BatteryComponent.Case, status.Case);
    }
}
