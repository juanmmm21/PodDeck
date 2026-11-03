using PodDeck.Core.Devices;
using PodDeck.Core.Tracking;

namespace PodDeck.Core.Tests.Tracking;

public sealed class EarDetectorTests
{
    private readonly EarDetector _detector = new(confirmationFrames: 2);

    [Fact]
    public void Observe_FirstStatus_SetsBaselineWithoutChange()
    {
        Assert.Null(_detector.Observe(Status(leftInEar: true, rightInEar: true)));
        Assert.Equal(2, _detector.InEarCount);
    }

    [Fact]
    public void Observe_RemovalSeenTwice_ReportsRemoval()
    {
        _detector.Observe(Status(leftInEar: true, rightInEar: true));

        Assert.Null(_detector.Observe(Status(leftInEar: false, rightInEar: true)));
        EarChange? change = _detector.Observe(Status(leftInEar: false, rightInEar: true));

        Assert.Equal(new EarChange(2, 1), change);
        Assert.True(change!.Value.IsRemoval);
        Assert.Equal(1, _detector.InEarCount);
    }

    [Fact]
    public void Observe_LastPodRemoved_ReportsRemovalToZero()
    {
        _detector.Observe(Status(leftInEar: false, rightInEar: true));
        _detector.Observe(Status(leftInEar: false, rightInEar: false));

        Assert.Equal(new EarChange(1, 0), _detector.Observe(Status(leftInEar: false, rightInEar: false)));
    }

    [Fact]
    public void Observe_ChangeReportedOnlyOnce()
    {
        _detector.Observe(Status(leftInEar: true, rightInEar: true));
        _detector.Observe(Status(leftInEar: false, rightInEar: true));
        _detector.Observe(Status(leftInEar: false, rightInEar: true));

        Assert.Null(_detector.Observe(Status(leftInEar: false, rightInEar: true)));
    }

    [Fact]
    public void Observe_SingleStrayFrame_IsIgnored()
    {
        _detector.Observe(Status(leftInEar: true, rightInEar: true));
        _detector.Observe(Status(leftInEar: false, rightInEar: true));

        Assert.Null(_detector.Observe(Status(leftInEar: true, rightInEar: true)));
        Assert.Null(_detector.Observe(Status(leftInEar: false, rightInEar: true)));
        Assert.Equal(2, _detector.InEarCount);
    }

    [Fact]
    public void Observe_PodPutBack_ReportsInsertion()
    {
        _detector.Observe(Status(leftInEar: false, rightInEar: true));
        _detector.Observe(Status(leftInEar: true, rightInEar: true));

        EarChange? change = _detector.Observe(Status(leftInEar: true, rightInEar: true));

        Assert.Equal(new EarChange(1, 2), change);
        Assert.False(change!.Value.IsRemoval);
    }

    [Fact]
    public void Observe_SwappedSidesSameCount_ReportsNothing()
    {
        _detector.Observe(Status(leftInEar: true, rightInEar: false));

        Assert.Null(_detector.Observe(Status(leftInEar: false, rightInEar: true)));
        Assert.Null(_detector.Observe(Status(leftInEar: false, rightInEar: true)));
    }

    [Fact]
    public void Observe_PendingCountChanges_StartsCountingAgain()
    {
        _detector.Observe(Status(leftInEar: true, rightInEar: true));
        _detector.Observe(Status(leftInEar: false, rightInEar: true));

        Assert.Null(_detector.Observe(Status(leftInEar: false, rightInEar: false)));
        Assert.Equal(new EarChange(2, 0), _detector.Observe(Status(leftInEar: false, rightInEar: false)));
    }

    [Fact]
    public void Reset_ForgetsBaseline()
    {
        _detector.Observe(Status(leftInEar: true, rightInEar: true));
        _detector.Reset();
        Assert.Null(_detector.InEarCount);

        Assert.Null(_detector.Observe(Status(leftInEar: false, rightInEar: false)));
        Assert.Null(_detector.Observe(Status(leftInEar: false, rightInEar: false)));
    }

    [Fact]
    public void Observe_DifferentModel_ResetsBaseline()
    {
        _detector.Observe(Status(leftInEar: true, rightInEar: true));

        Assert.Null(_detector.Observe(Status(leftInEar: false, rightInEar: false, productId: 0x2014)));
        Assert.Equal(0, _detector.InEarCount);
    }

    [Fact]
    public void Constructor_ZeroFrames_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EarDetector(confirmationFrames: 0));
    }

    private static AirPodsStatus Status(bool leftInEar, bool rightInEar, ushort productId = 0x2027) => new(
        AirPodsModels.FromProductId(productId),
        productId,
        new PodStatus(new BatteryReading(80, false), leftInEar, IsInCase: false),
        new PodStatus(new BatteryReading(70, false), rightInEar, IsInCase: false),
        BatteryReading.Unavailable,
        IsLidOpen: false);
}
