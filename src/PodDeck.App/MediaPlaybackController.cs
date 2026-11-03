using Windows.Media.Control;

namespace PodDeck.App;

/// <summary>
/// Pausa y reanuda lo que suena en Windows a través de los controles multimedia del sistema (los
/// mismos que usan las teclas de reproducción), sin saber qué app es.
/// </summary>
internal sealed class MediaPlaybackController
{
    private readonly List<string> _pausedApps = [];
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    /// <summary>
    /// Pausa todas las sesiones que están sonando y devuelve true si ha pausado alguna. Con
    /// <paramref name="keepPrevious"/> se suman a las de la pausa anterior, que siguen pendientes de
    /// reanudar; sin él, se empieza de cero.
    /// </summary>
    public async Task<bool> PauseAllPlayingAsync(bool keepPrevious)
    {
        if (!keepPrevious)
        {
            _pausedApps.Clear();
        }

        bool pausedAny = false;
        GlobalSystemMediaTransportControlsSessionManager manager = await GetManagerAsync();
        foreach (GlobalSystemMediaTransportControlsSession session in manager.GetSessions())
        {
            if (session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                && await session.TryPauseAsync())
            {
                pausedAny = true;
                if (!_pausedApps.Contains(session.SourceAppUserModelId, StringComparer.OrdinalIgnoreCase))
                {
                    _pausedApps.Add(session.SourceAppUserModelId);
                }
            }
        }

        return pausedAny;
    }

    /// <summary>
    /// Reanuda solo las sesiones que pausó <see cref="PauseAllPlayingAsync"/> y que siguen en pausa:
    /// si mientras tanto has puesto otra cosa o has cerrado la app, no se toca.
    /// </summary>
    public async Task ResumePausedAsync()
    {
        if (_pausedApps.Count == 0)
        {
            return;
        }

        var toResume = new HashSet<string>(_pausedApps, StringComparer.OrdinalIgnoreCase);
        _pausedApps.Clear();
        GlobalSystemMediaTransportControlsSessionManager manager = await GetManagerAsync();
        foreach (GlobalSystemMediaTransportControlsSession session in manager.GetSessions())
        {
            if (toResume.Contains(session.SourceAppUserModelId)
                && session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused
                && !await session.TryPlayAsync())
            {
                System.Diagnostics.Trace.TraceWarning($"Could not resume media session {session.SourceAppUserModelId}.");
            }
        }
    }

    public void Forget() => _pausedApps.Clear();

    private async Task<GlobalSystemMediaTransportControlsSessionManager> GetManagerAsync() =>
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync()
            ?? throw new InvalidOperationException("The system media session manager is unavailable.");
}
