using PodDeck.Core.Alerts;
using PodDeck.Core.Devices;

namespace PodDeck.Core.Tests.Alerts;

public sealed class LowBatteryMonitorTests
{
    private readonly LowBatteryMonitor _monitor = new();

    [Fact]
    public void Update_AboveThreshold_NoAlert()
    {
        Assert.Empty(_monitor.Update(Earbuds(new(30, false), new(40, false), new(50, false))));
    }

    [Fact]
    public void Update_PodDropsToThreshold_AlertsOnce()
    {
        _monitor.Update(Earbuds(new(30, false), new(40, false), BatteryReading.Unavailable));

        Assert.Equal([new LowBatteryAlert(BatteryComponent.Left, 20)],
            _monitor.Update(Earbuds(new(20, false), new(40, false), BatteryReading.Unavailable)));
        Assert.Empty(_monitor.Update(Earbuds(new(20, false), new(40, false), BatteryReading.Unavailable)));
        Assert.Empty(_monitor.Update(Earbuds(new(10, false), new(40, false), BatteryReading.Unavailable)));
    }

    [Fact]
    public void Update_BothPodsLow_AlertsForEach()
    {
        IReadOnlyList<LowBatteryAlert> alerts = _monitor.Update(Earbuds(new(10, false), new(20, false), new(80, false)));

        Assert.Equal([new LowBatteryAlert(BatteryComponent.Left, 10), new LowBatteryAlert(BatteryComponent.Right, 20)], alerts);
    }

    [Fact]
    public void Update_LowButCharging_NoAlert()
    {
        Assert.Empty(_monitor.Update(Earbuds(new(10, true), new(10, true), new(10, true))));
    }

    [Fact]
    public void Update_LowCase_Alerts()
    {
        Assert.Equal([new LowBatteryAlert(BatteryComponent.Case, 10)],
            _monitor.Update(Earbuds(BatteryReading.Unavailable, BatteryReading.Unavailable, new(10, false))));
    }

    [Fact]
    public void Update_ReadingBouncesAboveThreshold_DoesNotAlertAgain()
    {
        _monitor.Update(Earbuds(new(20, false), new(40, false), BatteryReading.Unavailable));
        _monitor.Update(Earbuds(new(30, false), new(40, false), BatteryReading.Unavailable));

        Assert.Empty(_monitor.Update(Earbuds(new(20, false), new(40, false), BatteryReading.Unavailable)));
    }

    [Fact]
    public void Update_AfterCharging_AlertsOnNextDischarge()
    {
        _monitor.Update(Earbuds(new(20, false), new(40, false), BatteryReading.Unavailable));
        _monitor.Update(Earbuds(new(60, true), new(40, false), BatteryReading.Unavailable));

        Assert.Equal([new LowBatteryAlert(BatteryComponent.Left, 20)],
            _monitor.Update(Earbuds(new(20, false), new(40, false), BatteryReading.Unavailable)));
    }

    [Fact]
    public void Update_PodStopsReporting_KeepsAlertState()
    {
        _monitor.Update(Earbuds(new(20, false), new(40, false), BatteryReading.Unavailable));
        _monitor.Update(Earbuds(BatteryReading.Unavailable, new(40, false), BatteryReading.Unavailable));
        _monitor.Update(null);

        Assert.Empty(_monitor.Update(Earbuds(new(20, false), new(40, false), BatteryReading.Unavailable)));
    }

    [Fact]
    public void Update_Disabled_NoAlert()
    {
        _monitor.IsEnabled = false;

        Assert.Empty(_monitor.Update(Earbuds(new(10, false), new(10, false), new(10, false))));
    }

    [Fact]
    public void ThresholdPercent_Raised_AlertsAgainAtNewThreshold()
    {
        _monitor.Update(Earbuds(new(20, false), new(40, false), BatteryReading.Unavailable));

        _monitor.ThresholdPercent = 30;

        Assert.Equal([new LowBatteryAlert(BatteryComponent.Left, 20)],
            _monitor.Update(Earbuds(new(20, false), new(40, false), BatteryReading.Unavailable)));
    }

    [Fact]
    public void ThresholdPercent_Lowered_DoesNotAlertAboveIt()
    {
        _monitor.ThresholdPercent = 10;

        Assert.Empty(_monitor.Update(Earbuds(new(20, false), new(30, false), new(20, false))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void ThresholdPercent_OutOfRange_Throws(int threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _monitor.ThresholdPercent = threshold);
    }

    [Fact]
    public void Update_Headphones_AlertsOnSingleBattery()
    {
        AirPodsStatus status = Earbuds(BatteryReading.Unavailable, new(10, false), BatteryReading.Unavailable) with
        {
            Model = AirPodsModel.AirPodsMax,
            ProductId = 0x200A,
        };

        Assert.Equal([new LowBatteryAlert(BatteryComponent.Headphones, 10)], _monitor.Update(status));
    }

    [Fact]
    public void Update_DifferentModel_StartsFresh()
    {
        _monitor.Update(Earbuds(new(20, false), new(40, false), BatteryReading.Unavailable));
        AirPodsStatus other = Earbuds(new(20, false), new(40, false), BatteryReading.Unavailable) with
        {
            Model = AirPodsModel.AirPodsPro2,
            ProductId = 0x2014,
        };

        Assert.Equal([new LowBatteryAlert(BatteryComponent.Left, 20)], _monitor.Update(other));
    }

    private static AirPodsStatus Earbuds(BatteryReading left, BatteryReading right, BatteryReading @case) => new(
        AirPodsModel.AirPodsPro3,
        0x2027,
        new PodStatus(left, IsInEar: true, IsInCase: false),
        new PodStatus(right, IsInEar: true, IsInCase: false),
        @case,
        IsLidOpen: false);
}
