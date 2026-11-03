namespace PodDeck.Core.Tracking;

/// <summary>Ajustes con los que <see cref="AirPodsTracker"/> decide qué AirPods son los tuyos.</summary>
public sealed record TrackerOptions
{
    /// <summary>Anuncios más débiles que esto se ignoran: son de AirPods de otra habitación o de otra persona.</summary>
    public short MinimumSignalDbm { get; init; } = -90;

    /// <summary>Tiempo sin anuncios tras el que unos AirPods se dan por guardados o lejos.</summary>
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>Ventaja de señal que necesita otro modelo para quitarle el puesto al seguido ahora.</summary>
    public double SwitchMarginDb { get; init; } = 10;

    /// <summary>Peso de cada medida nueva en la media móvil de la señal (0-1].</summary>
    public double SignalSmoothing { get; init; } = 0.3;
}
