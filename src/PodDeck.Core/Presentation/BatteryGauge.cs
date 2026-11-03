using PodDeck.Core.Devices;

namespace PodDeck.Core.Presentation;

/// <summary>Un indicador de la ventana emergente: qué componente es, su batería y el texto a mostrar.</summary>
public sealed record BatteryGauge(string Label, BatteryReading Battery)
{
    /// <summary>Fracción de batería (0-1) para dibujar el anillo, o null si el componente no informa.</summary>
    public double? Fill => Battery.Percent / 100.0;

    public string PercentText => Battery.Percent is int percent ? $"{percent}%" : "--";
}
