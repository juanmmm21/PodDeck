using Microsoft.Extensions.Time.Testing;
using PodDeck.Core.Devices;
using PodDeck.Core.Tracking;

namespace PodDeck.Core.Tests.Tracking;

public sealed class AirPodsTrackerTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly AirPodsTracker _tracker;
    private readonly List<AirPodsStatus?> _changes = [];

    public AirPodsTrackerTests()
    {
        _tracker = new AirPodsTracker(_time);
        _tracker.CurrentChanged += (_, status) => _changes.Add(status);
    }

    [Fact]
    public void Report_FirstAdvertisement_BecomesCurrent()
    {
        AirPodsStatus status = Status(0x2027, left: 80);

        _tracker.Report(status, -60);

        Assert.Equal(status, _tracker.Current);
        Assert.Equal([status], _changes);
    }

    [Fact]
    public void Report_SameStatusAgain_DoesNotRaiseChanged()
    {
        _tracker.Report(Status(0x2027, left: 80), -60);
        _tracker.Report(Status(0x2027, left: 80), -62);

        Assert.Single(_changes);
    }

    [Fact]
    public void Report_BatteryChange_RaisesChanged()
    {
        _tracker.Report(Status(0x2027, left: 80), -60);
        _tracker.Report(Status(0x2027, left: 70), -60);

        Assert.Equal(2, _changes.Count);
        Assert.Equal(70, _tracker.Current?.Left.Battery.Percent);
    }

    [Fact]
    public void Report_SignalBelowMinimum_IsIgnored()
    {
        _tracker.Report(Status(0x2027, left: 80), -95);

        Assert.Null(_tracker.Current);
        Assert.Empty(_changes);
    }

    [Fact]
    public void Report_SlightlyStrongerOtherModel_KeepsTrackedPair()
    {
        _tracker.Report(Status(0x2027, left: 80), -60);
        _tracker.Report(Status(0x200E, left: 30), -55);

        Assert.Equal((ushort)0x2027, _tracker.Current?.ProductId);
    }

    [Fact]
    public void Report_MuchStrongerOtherModel_SwitchesToIt()
    {
        _tracker.Report(Status(0x2027, left: 80), -80);
        _tracker.Report(Status(0x200E, left: 30), -50);

        Assert.Equal((ushort)0x200E, _tracker.Current?.ProductId);
    }

    [Fact]
    public void ExpireStale_AfterSilence_ClearsCurrent()
    {
        _tracker.Report(Status(0x2027, left: 80), -60);

        _time.Advance(TimeSpan.FromSeconds(21));
        _tracker.ExpireStale();

        Assert.Null(_tracker.Current);
        Assert.Equal(2, _changes.Count);
        Assert.Null(_changes[^1]);
    }

    [Fact]
    public void ExpireStale_WithinWindow_KeepsCurrent()
    {
        _tracker.Report(Status(0x2027, left: 80), -60);

        _time.Advance(TimeSpan.FromSeconds(19));
        _tracker.ExpireStale();

        Assert.NotNull(_tracker.Current);
        Assert.Single(_changes);
    }

    [Fact]
    public void ExpireStale_TrackedPairGone_FallsBackToRemainingPair()
    {
        _tracker.Report(Status(0x2027, left: 80), -50);
        _time.Advance(TimeSpan.FromSeconds(10));
        _tracker.Report(Status(0x200E, left: 30), -70);

        _time.Advance(TimeSpan.FromSeconds(11));
        _tracker.ExpireStale();

        Assert.Equal((ushort)0x200E, _tracker.Current?.ProductId);
    }

    [Fact]
    public void Report_WithPairedProducts_IgnoresOtherModels()
    {
        _tracker.SetPairedProducts([0x2027]);

        _tracker.Report(Status(0x200E, left: 30), -40);

        Assert.Null(_tracker.Current);
        Assert.Empty(_changes);
    }

    [Fact]
    public void Report_WithPairedProducts_KeepsPairedModelEvenIfAnotherIsStronger()
    {
        _tracker.SetPairedProducts([0x2027]);

        _tracker.Report(Status(0x2027, left: 80), -85);
        _tracker.Report(Status(0x200E, left: 30), -40);

        Assert.Equal((ushort)0x2027, _tracker.Current?.ProductId);
        Assert.Single(_changes);
    }

    [Fact]
    public void SetPairedProducts_DropsTrackedUnpairedModel()
    {
        _tracker.Report(Status(0x200E, left: 30), -40);
        _tracker.Report(Status(0x2027, left: 80), -85);

        _tracker.SetPairedProducts([0x2027]);

        Assert.Equal((ushort)0x2027, _tracker.Current?.ProductId);
        Assert.Equal(2, _changes.Count);
    }

    [Fact]
    public void SetPairedProducts_PairedModelNotNearby_ClearsCurrent()
    {
        _tracker.Report(Status(0x200E, left: 30), -40);

        _tracker.SetPairedProducts([0x2027]);

        Assert.Null(_tracker.Current);
        Assert.Null(_changes[^1]);
    }

    [Fact]
    public void SetPairedProducts_Empty_AcceptsAnyModelAgain()
    {
        _tracker.SetPairedProducts([0x2027]);
        _tracker.SetPairedProducts([]);

        _tracker.Report(Status(0x200E, left: 30), -40);

        Assert.Equal((ushort)0x200E, _tracker.Current?.ProductId);
    }

    [Fact]
    public void SetPairedProducts_SameSet_DoesNotRaiseChanged()
    {
        _tracker.Report(Status(0x2027, left: 80), -60);

        _tracker.SetPairedProducts([0x2027]);
        _tracker.SetPairedProducts([0x2027, 0x2027]);

        Assert.Single(_changes);
        Assert.Equal(new HashSet<ushort> { 0x2027 }, _tracker.PairedProducts);
    }

    [Fact]
    public void Constructor_InvalidSmoothing_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AirPodsTracker(_time, new TrackerOptions { SignalSmoothing = 0 }));
    }

    [Fact]
    public void Report_TrackedPairRepeatsStatus_RaisesReportedEveryTime()
    {
        var reported = new List<AirPodsStatus>();
        _tracker.StatusReported += (_, status) => reported.Add(status);

        _tracker.Report(Status(0x2027, left: 80), -60);
        _tracker.Report(Status(0x2027, left: 80), -61);

        Assert.Equal(2, reported.Count);
        Assert.Single(_changes);
    }

    [Fact]
    public void Report_UntrackedPair_DoesNotRaiseReported()
    {
        var reported = new List<AirPodsStatus>();
        _tracker.StatusReported += (_, status) => reported.Add(status);
        _tracker.Report(Status(0x2027, left: 80), -50);

        _tracker.Report(Status(0x2014, left: 30), -80);

        Assert.Equal([Status(0x2027, left: 80)], reported);
    }

    private static AirPodsStatus Status(ushort productId, int left) => new(
        AirPodsModels.FromProductId(productId),
        productId,
        new PodStatus(new BatteryReading(left, false), IsInEar: true, IsInCase: false),
        new PodStatus(new BatteryReading(left, false), IsInEar: true, IsInCase: false),
        BatteryReading.Unavailable,
        IsLidOpen: false);
}
