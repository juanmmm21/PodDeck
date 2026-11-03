using PodDeck.Core.Tracking;

namespace PodDeck.Core.Playback;

/// <summary>Lo que la app debe hacer con la reproducción de Windows tras un cambio en los oídos.</summary>
public enum PlaybackAction
{
    None,
    Pause,
    Resume,
}

/// <summary>
/// Decide cuándo pausar y reanudar la reproducción al quitarse o ponerse los AirPods, como hace el
/// iPhone. No es seguro entre hilos: en la app se usa siempre desde el hilo de la interfaz.
/// </summary>
/// <remarks>
/// Solo se reanuda lo que PodDeck pausó (<see cref="ConfirmPaused"/>), cuando vuelven a estar puestos
/// tantos auriculares como antes de la pausa y dentro de <see cref="ResumeWindow"/>: ponerse los
/// AirPods una hora después no debe arrancar la música que dejaste a medias.
/// </remarks>
public sealed class AutoPausePolicy
{
    public static readonly TimeSpan DefaultResumeWindow = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _time;
    private bool _isEnabled = true;
    private int? _countBeforePause;
    private DateTimeOffset? _pausedAt;

    public AutoPausePolicy(TimeProvider time, TimeSpan? resumeWindow = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
        ResumeWindow = resumeWindow ?? DefaultResumeWindow;
        if (ResumeWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(resumeWindow), resumeWindow, "ResumeWindow must be positive.");
        }
    }

    public TimeSpan ResumeWindow { get; }

    /// <summary>Al desactivarlo se olvida cualquier pausa pendiente de reanudar.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (!value)
            {
                Forget();
            }
        }
    }

    /// <summary>True mientras hay una pausa de PodDeck que todavía se puede reanudar.</summary>
    public bool HasPendingResume => _pausedAt is DateTimeOffset pausedAt && _time.GetUtcNow() - pausedAt <= ResumeWindow;

    public PlaybackAction Decide(EarChange change)
    {
        if (!_isEnabled)
        {
            return PlaybackAction.None;
        }

        if (change.IsRemoval)
        {
            // Cada vez que se quita uno se pausa, aunque PodDeck ya hubiera pausado antes: si le diste
            // a reproducir con un solo auricular puesto, quitártelo también debe pararla. Lo que no
            // cambia es a cuántos auriculares hay que volver para reanudar.
            if (!HasPendingResume)
            {
                _countBeforePause = change.Before;
                _pausedAt = null;
            }

            return PlaybackAction.Pause;
        }

        if (!HasPendingResume)
        {
            Forget();
            return PlaybackAction.None;
        }

        if (change.After < _countBeforePause)
        {
            return PlaybackAction.None;
        }

        Forget();
        return PlaybackAction.Resume;
    }

    /// <summary>La app lo llama si la pausa paró algo de verdad; si no sonaba nada, no hay que reanudar.</summary>
    public void ConfirmPaused()
    {
        if (_isEnabled && _countBeforePause is not null)
        {
            _pausedAt = _time.GetUtcNow();
        }
    }

    private void Forget()
    {
        _countBeforePause = null;
        _pausedAt = null;
    }
}
