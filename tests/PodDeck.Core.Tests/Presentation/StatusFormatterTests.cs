using PodDeck.Core.Devices;
using PodDeck.Core.Presentation;

namespace PodDeck.Core.Tests.Presentation;

public sealed class StatusFormatterTests
{
    [Fact]
    public void Tooltip_NoAirPods_SaysNotNearby()
    {
        Assert.Equal(StatusFormatter.NotNearbyText, StatusFormatter.Tooltip(null));
        Assert.Null(StatusFormatter.IconPercent(null));
    }

    [Fact]
    public void Tooltip_Earbuds_ListsEveryComponent()
    {
        AirPodsStatus status = Earbuds(left: new(80, false), right: new(null, false), @case: new(50, true));

        Assert.Equal("AirPods Pro 3 — L 80% · R -- · Case 50%⚡", StatusFormatter.Tooltip(status));
    }

    [Fact]
    public void Tooltip_LongestModelName_FitsWindowsLimit()
    {
        AirPodsStatus status = Earbuds(new(100, true), new(100, true), new(100, true)) with
        {
            Model = AirPodsModel.AirPodsPro2UsbC,
            ProductId = 0x2024,
        };

        Assert.True(StatusFormatter.Tooltip(status).Length <= StatusFormatter.MaxTooltipLength);
    }

    [Fact]
    public void DetailLines_Earbuds_DescribePositionAndLid()
    {
        AirPodsStatus status = Earbuds(new(80, false), new(60, true), new(40, false)) with
        {
            Left = new PodStatus(new BatteryReading(80, false), IsInEar: true, IsInCase: false),
            Right = new PodStatus(new BatteryReading(60, true), IsInEar: false, IsInCase: true),
            IsLidOpen = true,
        };

        Assert.Equal(
            ["Left: 80% · in ear", "Right: 60% (charging) · in case", "Case: 40% · lid open"],
            StatusFormatter.DetailLines(status));
    }

    [Fact]
    public void IconPercent_Earbuds_UsesLowestPod()
    {
        Assert.Equal(30, StatusFormatter.IconPercent(Earbuds(new(30, false), new(90, false), new(100, false))));
    }

    [Fact]
    public void Headphones_UseTheReportingSideOnly()
    {
        AirPodsStatus status = Earbuds(new(null, false), new(70, false), BatteryReading.Unavailable) with
        {
            Model = AirPodsModel.AirPodsMax,
            ProductId = 0x200A,
        };

        Assert.Equal("AirPods Max — 70%", StatusFormatter.Tooltip(status));
        Assert.Equal(["Battery: 70%"], StatusFormatter.DetailLines(status));
        Assert.Equal(70, StatusFormatter.IconPercent(status));
    }

    [Fact]
    public void PopupGauges_Earbuds_ShowLeftRightAndCase()
    {
        AirPodsStatus status = Earbuds(new(80, true), new(null, false), new(100, false));

        IReadOnlyList<BatteryGauge> gauges = StatusFormatter.PopupGauges(status);

        Assert.Equal(["Left", "Right", "Case"], gauges.Select(gauge => gauge.Label));
        Assert.Equal(["80%", "--", "100%"], gauges.Select(gauge => gauge.PercentText));
        Assert.Equal([0.8, null, 1.0], gauges.Select(gauge => gauge.Fill));
        Assert.True(gauges[0].Battery.IsCharging);
    }

    [Fact]
    public void PopupGauges_Headphones_ShowSingleBattery()
    {
        AirPodsStatus status = Earbuds(new(null, false), new(40, false), BatteryReading.Unavailable) with
        {
            Model = AirPodsModel.AirPodsMaxUsbC,
            ProductId = 0x201F,
        };

        BatteryGauge gauge = Assert.Single(StatusFormatter.PopupGauges(status));
        Assert.Equal(new BatteryGauge("Battery", new BatteryReading(40, false)), gauge);
    }

    private static AirPodsStatus Earbuds(BatteryReading left, BatteryReading right, BatteryReading @case) => new(
        AirPodsModel.AirPodsPro3,
        0x2027,
        new PodStatus(left, IsInEar: false, IsInCase: false),
        new PodStatus(right, IsInEar: false, IsInCase: false),
        @case,
        IsLidOpen: false);
}
