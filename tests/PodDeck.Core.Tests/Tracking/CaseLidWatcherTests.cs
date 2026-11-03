using Microsoft.Extensions.Time.Testing;
using PodDeck.Core.Devices;
using PodDeck.Core.Tracking;

namespace PodDeck.Core.Tests.Tracking;

public sealed class CaseLidWatcherTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly CaseLidWatcher _watcher;

    public CaseLidWatcherTests()
    {
        _watcher = new CaseLidWatcher(_time, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Update_NothingNearby_ReportsNothing()
    {
        Assert.Equal(LidChange.None, _watcher.Update(null));
        Assert.False(_watcher.IsOpen);
    }

    [Fact]
    public void Update_FirstOpenLid_ReportsOpened()
    {
        Assert.Equal(LidChange.Opened, _watcher.Update(Status(lidOpen: true)));
        Assert.True(_watcher.IsOpen);
    }

    [Fact]
    public void Update_LidStaysOpen_ReportsOpenedOnlyOnce()
    {
        _watcher.Update(Status(lidOpen: true));
        _time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(LidChange.None, _watcher.Update(Status(lidOpen: true, casePercent: 90)));
    }

    [Fact]
    public void Update_ClosedLidSeenBriefly_DoesNotCloseYet()
    {
        _watcher.Update(Status(lidOpen: true));
        _time.Advance(TimeSpan.FromMilliseconds(300));

        Assert.Equal(LidChange.None, _watcher.Update(Status(lidOpen: false)));
        Assert.True(_watcher.IsOpen);
    }

    [Fact]
    public void Update_BounceBackToOpen_DoesNotReportOpenedAgain()
    {
        _watcher.Update(Status(lidOpen: true));
        _time.Advance(TimeSpan.FromMilliseconds(300));
        _watcher.Update(Status(lidOpen: false));
        _time.Advance(TimeSpan.FromMilliseconds(300));

        Assert.Equal(LidChange.None, _watcher.Update(Status(lidOpen: true)));
    }

    [Fact]
    public void Update_ClosedLidPersists_ReportsClosedOnce()
    {
        _watcher.Update(Status(lidOpen: true));
        _time.Advance(TimeSpan.FromMilliseconds(300));
        _watcher.Update(Status(lidOpen: false));
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(LidChange.Closed, _watcher.Update(Status(lidOpen: false)));
        Assert.Equal(LidChange.None, _watcher.Update(Status(lidOpen: false)));
        Assert.False(_watcher.IsOpen);
    }

    [Fact]
    public void Update_AirPodsGone_ReportsClosedImmediately()
    {
        _watcher.Update(Status(lidOpen: true));

        Assert.Equal(LidChange.Closed, _watcher.Update(null));
    }

    [Fact]
    public void Update_ReopenedAfterConfirmedClose_ReportsOpenedAgain()
    {
        _watcher.Update(Status(lidOpen: true));
        _time.Advance(TimeSpan.FromSeconds(2));
        _watcher.Update(Status(lidOpen: false));

        Assert.Equal(LidChange.Opened, _watcher.Update(Status(lidOpen: true)));
    }

    [Fact]
    public void Constructor_NegativeConfirmation_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaseLidWatcher(_time, TimeSpan.FromSeconds(-1)));
    }

    private static AirPodsStatus Status(bool lidOpen, int casePercent = 80) => new(
        AirPodsModel.AirPodsPro3,
        0x2027,
        new PodStatus(new BatteryReading(70, true), IsInEar: false, IsInCase: true),
        new PodStatus(new BatteryReading(70, true), IsInEar: false, IsInCase: true),
        new BatteryReading(casePercent, false),
        lidOpen);
}
