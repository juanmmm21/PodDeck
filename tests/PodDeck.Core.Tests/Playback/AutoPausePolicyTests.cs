using Microsoft.Extensions.Time.Testing;
using PodDeck.Core.Playback;
using PodDeck.Core.Tracking;

namespace PodDeck.Core.Tests.Playback;

public sealed class AutoPausePolicyTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly FakeTimeProvider _time = new();
    private readonly AutoPausePolicy _policy;

    public AutoPausePolicyTests()
    {
        _policy = new AutoPausePolicy(_time, Window);
    }

    [Fact]
    public void Decide_Removal_Pauses()
    {
        Assert.Equal(PlaybackAction.Pause, _policy.Decide(new EarChange(2, 1)));
    }

    [Fact]
    public void Decide_PutBackAfterConfirmedPause_Resumes()
    {
        _policy.Decide(new EarChange(2, 1));
        _policy.ConfirmPaused();
        _time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(PlaybackAction.Resume, _policy.Decide(new EarChange(1, 2)));
        Assert.False(_policy.HasPendingResume);
    }

    [Fact]
    public void Decide_PutBackWhenNothingWasPaused_DoesNothing()
    {
        _policy.Decide(new EarChange(2, 1));

        Assert.Equal(PlaybackAction.None, _policy.Decide(new EarChange(1, 2)));
    }

    // Juan, 2026-11-03: con un solo auricular puesto, quitárselo no paraba la música. Venía de haber
    // pausado al quitarse el otro y haber vuelto a darle a reproducir a mano.
    [Fact]
    public void Decide_SecondRemovalWhilePaused_PausesAgain()
    {
        _policy.Decide(new EarChange(2, 1));
        _policy.ConfirmPaused();

        Assert.Equal(PlaybackAction.Pause, _policy.Decide(new EarChange(1, 0)));
    }

    [Fact]
    public void Decide_SecondRemovalWhilePaused_KeepsOriginalResumeTarget()
    {
        _policy.Decide(new EarChange(2, 1));
        _policy.ConfirmPaused();
        _policy.Decide(new EarChange(1, 0));
        _policy.ConfirmPaused();

        Assert.Equal(PlaybackAction.None, _policy.Decide(new EarChange(0, 1)));
        Assert.Equal(PlaybackAction.Resume, _policy.Decide(new EarChange(1, 2)));
    }

    [Fact]
    public void Decide_OnlyPodRemoved_PausesAndResumesWhenPutBack()
    {
        Assert.Equal(PlaybackAction.Pause, _policy.Decide(new EarChange(1, 0)));
        _policy.ConfirmPaused();

        Assert.Equal(PlaybackAction.Resume, _policy.Decide(new EarChange(0, 1)));
    }

    [Fact]
    public void Decide_OnlyOneOfTwoPutBack_WaitsForTheOther()
    {
        _policy.Decide(new EarChange(2, 1));
        _policy.ConfirmPaused();
        _policy.Decide(new EarChange(1, 0));

        Assert.Equal(PlaybackAction.None, _policy.Decide(new EarChange(0, 1)));
        Assert.Equal(PlaybackAction.Resume, _policy.Decide(new EarChange(1, 2)));
    }

    [Fact]
    public void Decide_PutBackAfterWindow_DoesNotResume()
    {
        _policy.Decide(new EarChange(1, 0));
        _policy.ConfirmPaused();
        _time.Advance(Window + TimeSpan.FromSeconds(1));

        Assert.Equal(PlaybackAction.None, _policy.Decide(new EarChange(0, 1)));
        Assert.False(_policy.HasPendingResume);
    }

    [Fact]
    public void Decide_RemovalAfterExpiredPause_PausesAgain()
    {
        _policy.Decide(new EarChange(2, 1));
        _policy.ConfirmPaused();
        _time.Advance(Window + TimeSpan.FromSeconds(1));

        Assert.Equal(PlaybackAction.Pause, _policy.Decide(new EarChange(1, 0)));
    }

    [Fact]
    public void Decide_Disabled_DoesNothing()
    {
        _policy.IsEnabled = false;

        Assert.Equal(PlaybackAction.None, _policy.Decide(new EarChange(2, 1)));
        Assert.Equal(PlaybackAction.None, _policy.Decide(new EarChange(1, 2)));
    }

    [Fact]
    public void IsEnabled_TurnedOffWhilePaused_ForgetsPendingResume()
    {
        _policy.Decide(new EarChange(2, 1));
        _policy.ConfirmPaused();
        _policy.IsEnabled = false;
        _policy.IsEnabled = true;

        Assert.False(_policy.HasPendingResume);
        Assert.Equal(PlaybackAction.None, _policy.Decide(new EarChange(1, 2)));
    }

    [Fact]
    public void Constructor_NonPositiveWindow_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutoPausePolicy(_time, TimeSpan.Zero));
    }
}
