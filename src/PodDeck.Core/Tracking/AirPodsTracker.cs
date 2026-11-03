using PodDeck.Core.Devices;

namespace PodDeck.Core.Tracking;

/// <summary>
/// Convierte el chorro de anuncios en un único estado estable: los AirPods más cercanos y cuándo
/// dejan de estar disponibles. No es seguro entre hilos: quien lo use debe llamarlo siempre desde
/// el mismo hilo (en la app, el de la interfaz).
/// </summary>
/// <remarks>
/// Los AirPods cambian de dirección Bluetooth aleatoria cada pocos minutos, así que la dirección no
/// sirve para reconocerlos. Se agrupan por modelo y se elige el de mejor señal media, con margen
/// para no saltar entre dos pares cercanos a cada anuncio.
/// </remarks>
public sealed class AirPodsTracker
{
    private readonly TimeProvider _time;
    private readonly TrackerOptions _options;
    private readonly Dictionary<ushort, Candidate> _candidates = [];
    private ushort? _trackedProductId;

    public AirPodsTracker(TimeProvider time, TrackerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
        _options = options ?? new TrackerOptions();
        if (_options.SignalSmoothing is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), _options.SignalSmoothing, "SignalSmoothing must be in (0, 1].");
        }

        if (_options.StaleAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), _options.StaleAfter, "StaleAfter must be positive.");
        }
    }

    /// <summary>Estado de los AirPods seguidos, o null si no hay ninguno cerca.</summary>
    public AirPodsStatus? Current { get; private set; }

    /// <summary>Solo se dispara cuando cambia algo visible: batería, posición, modelo o disponibilidad.</summary>
    public event EventHandler<AirPodsStatus?>? CurrentChanged;

    public void Report(AirPodsStatus status, short signalDbm)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (signalDbm < _options.MinimumSignalDbm)
        {
            return;
        }

        DateTimeOffset now = _time.GetUtcNow();
        if (_candidates.TryGetValue(status.ProductId, out Candidate? candidate))
        {
            candidate.Update(status, signalDbm, now, _options.SignalSmoothing);
        }
        else
        {
            _candidates[status.ProductId] = new Candidate(status, signalDbm, now);
        }

        Reselect(now);
    }

    /// <summary>Olvida los AirPods que llevan demasiado sin anunciarse. Hay que llamarlo periódicamente.</summary>
    public void ExpireStale() => Reselect(_time.GetUtcNow());

    private void Reselect(DateTimeOffset now)
    {
        foreach (ushort productId in _candidates.Where(pair => now - pair.Value.LastSeen > _options.StaleAfter)
                     .Select(pair => pair.Key).ToList())
        {
            _candidates.Remove(productId);
        }

        Candidate? tracked = _trackedProductId is ushort id ? _candidates.GetValueOrDefault(id) : null;
        Candidate? strongest = _candidates.Values.MaxBy(candidate => candidate.SmoothedSignalDbm);
        if (tracked is null
            || (strongest is not null && strongest.SmoothedSignalDbm - tracked.SmoothedSignalDbm >= _options.SwitchMarginDb))
        {
            tracked = strongest;
        }

        _trackedProductId = tracked?.Status.ProductId;
        AirPodsStatus? next = tracked?.Status;
        if (!Equals(next, Current))
        {
            Current = next;
            CurrentChanged?.Invoke(this, next);
        }
    }

    private sealed class Candidate(AirPodsStatus status, short signalDbm, DateTimeOffset lastSeen)
    {
        public AirPodsStatus Status { get; private set; } = status;

        public double SmoothedSignalDbm { get; private set; } = signalDbm;

        public DateTimeOffset LastSeen { get; private set; } = lastSeen;

        public void Update(AirPodsStatus status, short signalDbm, DateTimeOffset now, double smoothing)
        {
            Status = status;
            SmoothedSignalDbm += smoothing * (signalDbm - SmoothedSignalDbm);
            LastSeen = now;
        }
    }
}
