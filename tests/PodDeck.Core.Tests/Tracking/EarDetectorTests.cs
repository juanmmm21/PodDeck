using Microsoft.Extensions.Time.Testing;
using PodDeck.Core.Devices;
using PodDeck.Core.Tracking;

namespace PodDeck.Core.Tests.Tracking;

public sealed class EarDetectorTests
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(500);

    private readonly FakeTimeProvider _time = new();
    private readonly EarDetector _detector;

    public EarDetectorTests()
    {
        _detector = new EarDetector(_time, Settle);
    }

    [Fact]
    public void Update_FirstStatus_SetsBaselineWithoutChange()
    {
        Assert.Null(_detector.Update(Status(leftInEar: true, rightInEar: true)));
        Assert.Equal(2, _detector.InEarCount);
    }

    [Fact]
    public void Update_PodRemovedAndSettled_ReportsRemoval()
    {
        _detector.Update(Status(leftInEar: true, rightInEar: true));

        Assert.Null(_detector.Update(Status(leftInEar: false, rightInEar: true)));
        _time.Advance(Settle);
        EarChange? change = _detector.Update(Status(leftInEar: false, rightInEar: true));

        Assert.Equal(new EarChange(2, 1), change);
        Assert.True(change!.Value.IsRemoval);
        Assert.Equal(1, _detector.InEarCount);
    }

    [Fact]
    public void Update_ChangeReportedOnlyOnce()
    {
        _detector.Update(Status(leftInEar: true, rightInEar: true));
        _detector.Update(Status(leftInEar: false, rightInEar: true));
        _time.Advance(Settle);
        _detector.Update(Status(leftInEar: false, rightInEar: true));
        _time.Advance(Settle);

        Assert.Null(_detector.Update(Status(leftInEar: false, rightInEar: true)));
    }

    [Fact]
    public void Update_BriefFlicker_IsIgnored()
    {
        _detector.Update(Status(leftInEar: true, rightInEar: true));
        _detector.Update(Status(leftInEar: false, rightInEar: true));
        _time.Advance(TimeSpan.FromMilliseconds(200));
        _detector.Update(Status(leftInEar: true, rightInEar: true));
        _time.Advance(Settle);

        Assert.Null(_detector.Update(Status(leftInEar: true, rightInEar: true)));
        Assert.Equal(2, _detector.InEarCount);
    }

    [Fact]
    public void Update_PodPutBack_ReportsInsertion()
    {
        _detector.Update(Status(leftInEar: false, rightInEar: true));
        _detector.Update(Status(leftInEar: true, rightInEar: true));
        _time.Advance(Settle);

        EarChange? change = _detector.Update(Status(leftInEar: true, rightInEar: true));

        Assert.Equal(new EarChange(1, 2), change);
        Assert.False(change!.Value.IsRemoval);
    }

    [Fact]
    public void Update_SwappedSidesSameCount_ReportsNothing()
    {
        _detector.Update(Status(leftInEar: true, rightInEar: false));
        _time.Advance(Settle);

        Assert.Null(_detector.Update(Status(leftInEar: false, rightInEar: true)));
    }

    [Fact]
    public void Update_PendingCountChanges_RestartsSettleTime()
    {
        _detector.Update(Status(leftInEar: true, rightInEar: true));
        _detector.Update(Status(leftInEar: false, rightInEar: true));
        _time.Advance(TimeSpan.FromMilliseconds(400));
        _detector.Update(Status(leftInEar: false, rightInEar: false));
        _time.Advance(TimeSpan.FromMilliseconds(400));

        Assert.Null(_detector.Update(Status(leftInEar: false, rightInEar: false)));
        _time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(new EarChange(2, 0), _detector.Update(Status(leftInEar: false, rightInEar: false)));
    }

    [Fact]
    public void Update_AirPodsGone_ForgetsBaseline()
    {
        _detector.Update(Status(leftInEar: true, rightInEar: true));
        _detector.Update(null);
        Assert.Null(_detector.InEarCount);

        Assert.Null(_detector.Update(Status(leftInEar: false, rightInEar: false)));
        _time.Advance(Settle);
        Assert.Null(_detector.Update(Status(leftInEar: false, rightInEar: false)));
    }

    [Fact]
    public void Update_DifferentModel_ResetsBaseline()
    {
        _detector.Update(Status(leftInEar: true, rightInEar: true));
        _time.Advance(Settle);

        Assert.Null(_detector.Update(Status(leftInEar: false, rightInEar: false, productId: 0x2014)));
        Assert.Equal(0, _detector.InEarCount);
    }

    [Fact]
    public void Constructor_NegativeSettleTime_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EarDetector(_time, TimeSpan.FromSeconds(-1)));
    }

    private static AirPodsStatus Status(bool leftInEar, bool rightInEar, ushort productId = 0x2027) => new(
        AirPodsModels.FromProductId(productId),
        productId,
        new PodStatus(new BatteryReading(80, false), leftInEar, IsInCase: false),
        new PodStatus(new BatteryReading(70, false), rightInEar, IsInCase: false),
        BatteryReading.Unavailable,
        IsLidOpen: false);
}
