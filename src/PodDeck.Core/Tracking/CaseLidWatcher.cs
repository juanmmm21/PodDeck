using PodDeck.Core.Devices;

namespace PodDeck.Core.Tracking;

/// <summary>Lo que ha pasado con la tapa del estuche desde la última llamada a <see cref="CaseLidWatcher.Update"/>.</summary>
public enum LidChange
{
    None,
    Opened,
    Closed,
}

/// <summary>
/// Convierte el estado de tapa de cada anuncio en dos sucesos limpios, «se ha abierto» y «se ha
/// cerrado», que es lo que necesita la ventana emergente. No es seguro entre hilos, igual que
/// <see cref="AirPodsTracker"/>.
/// </summary>
/// <remarks>
/// La tapa solo cuenta como abierta si hay algún auricular dentro, y un anuncio suelto con la tapa
/// cerrada en mitad de varios abiertos no debe cerrar la ventana para volver a abrirla al instante.
/// Por eso el cierre solo se da por bueno cuando lleva <see cref="CloseConfirmation"/> sin verse la
/// tapa abierta, o cuando los AirPods desaparecen del todo. La apertura se notifica en el acto.
/// </remarks>
public sealed class CaseLidWatcher
{
    public static readonly TimeSpan DefaultCloseConfirmation = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _time;
    private DateTimeOffset _lastSeenOpen;

    public CaseLidWatcher(TimeProvider time, TimeSpan? closeConfirmation = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
        CloseConfirmation = closeConfirmation ?? DefaultCloseConfirmation;
        if (CloseConfirmation < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(closeConfirmation), closeConfirmation, "CloseConfirmation cannot be negative.");
        }
    }

    public TimeSpan CloseConfirmation { get; }

    public bool IsOpen { get; private set; }

    /// <summary>
    /// Hay que llamarlo con cada estado nuevo del tracker y también periódicamente, porque el cierre
    /// se confirma con el paso del tiempo aunque no llegue ningún cambio.
    /// </summary>
    public LidChange Update(AirPodsStatus? current)
    {
        DateTimeOffset now = _time.GetUtcNow();
        if (current is { IsLidOpen: true })
        {
            _lastSeenOpen = now;
            if (IsOpen)
            {
                return LidChange.None;
            }

            IsOpen = true;
            return LidChange.Opened;
        }

        if (!IsOpen || (current is not null && now - _lastSeenOpen < CloseConfirmation))
        {
            return LidChange.None;
        }

        IsOpen = false;
        return LidChange.Closed;
    }
}
