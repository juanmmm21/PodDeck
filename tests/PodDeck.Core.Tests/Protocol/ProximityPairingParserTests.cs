using PodDeck.Core.Devices;
using PodDeck.Core.Protocol;

namespace PodDeck.Core.Tests.Protocol;

public sealed class ProximityPairingParserTests
{
    // Capturado el 2026-11-03 de unos AirPods Pro 3 reales: auricular derecho puesto al 70 %,
    // el izquierdo y el estuche sin informar.
    private const string RealAirPodsPro3Capture = "071901272002F78F110005847C976E73433A3A56E05D8769543790";

    [Fact]
    public void TryParse_RealAirPodsPro3Capture_DecodesRightPodInEar()
    {
        AirPodsStatus status = Parse(RealAirPodsPro3Capture);

        Assert.Equal(AirPodsModel.AirPodsPro3, status.Model);
        Assert.Equal((ushort)0x2027, status.ProductId);
        Assert.Equal(new PodStatus(new BatteryReading(70, false), IsInEar: true, IsInCase: false), status.Right);
        Assert.Equal(BatteryReading.Unavailable, status.Left.Battery);
        Assert.False(status.Left.IsInEar);
        Assert.Equal(BatteryReading.Unavailable, status.Case);
        Assert.False(status.IsLidOpen);
        Assert.Equal(70, status.LowestPodPercent);
    }

    [Fact]
    public void TryParse_PrimaryIsLeft_MapsLowNibbleToLeftPod()
    {
        // Estado 0x2A: primario izquierdo (0x20), primario en oreja (0x02) y secundario en oreja (0x08).
        AirPodsStatus status = Parse(Frame(status: 0x2A, pods: 0x58, chargingAndCase: 0x06));

        Assert.Equal(new BatteryReading(80, false), status.Left.Battery);
        Assert.Equal(new BatteryReading(50, false), status.Right.Battery);
        Assert.True(status.Left.IsInEar);
        Assert.True(status.Right.IsInEar);
        Assert.Equal(new BatteryReading(60, false), status.Case);
        Assert.Equal(50, status.LowestPodPercent);
    }

    [Fact]
    public void TryParse_PrimaryIsRight_MapsLowNibbleToRightPod()
    {
        AirPodsStatus status = Parse(Frame(status: 0x0A, pods: 0x58, chargingAndCase: 0x06));

        Assert.Equal(new BatteryReading(80, false), status.Right.Battery);
        Assert.Equal(new BatteryReading(50, false), status.Left.Battery);
    }

    [Fact]
    public void TryParse_BothPodsChargingInOpenCase_ReportsChargingAndLidOpen()
    {
        // 0x24: primario izquierdo y los dos en el estuche; carga 0x7: primario, secundario y estuche.
        AirPodsStatus status = Parse(Frame(status: 0x24, pods: 0x99, chargingAndCase: 0x7A, lid: 0x31));

        Assert.True(status.Left.IsInCase);
        Assert.True(status.Right.IsInCase);
        Assert.False(status.Left.IsInEar);
        Assert.True(status.Left.Battery.IsCharging);
        Assert.True(status.Right.Battery.IsCharging);
        Assert.Equal(new BatteryReading(100, true), status.Case);
        Assert.True(status.IsLidOpen);
    }

    [Fact]
    public void TryParse_LidClosedFlag_ReportsLidClosed()
    {
        AirPodsStatus status = Parse(Frame(status: 0x24, pods: 0x99, chargingAndCase: 0x0A, lid: 0x39));

        Assert.False(status.IsLidOpen);
    }

    [Fact]
    public void TryParse_OnlySecondaryInCase_KeepsPrimaryInEar()
    {
        // 0x32: primario izquierdo en oreja (0x02) y un auricular, el otro, en el estuche (0x10).
        AirPodsStatus status = Parse(Frame(status: 0x32, pods: 0x46, chargingAndCase: 0x25));

        Assert.True(status.Left.IsInEar);
        Assert.False(status.Left.IsInCase);
        Assert.True(status.Right.IsInCase);
        Assert.False(status.Right.IsInEar);
        Assert.True(status.Right.Battery.IsCharging);
        Assert.False(status.Left.Battery.IsCharging);
    }

    [Fact]
    public void TryParse_UnavailablePod_IsNeverCharging()
    {
        AirPodsStatus status = Parse(Frame(status: 0x20, pods: 0xF5, chargingAndCase: 0x3F));

        Assert.Equal(BatteryReading.Unavailable, status.Right.Battery);
        Assert.True(status.Left.Battery.IsCharging);
        Assert.Equal(BatteryReading.Unavailable, status.Case);
    }

    [Fact]
    public void TryParse_UnknownProductId_KeepsRawIdForDisplay()
    {
        AirPodsStatus status = Parse(Frame(status: 0x20, pods: 0x55, chargingAndCase: 0x05, productId: 0x2099));

        Assert.Equal(AirPodsModel.Unknown, status.Model);
        Assert.Equal("AirPods (product 0x2099)", status.DisplayName);
    }

    [Fact]
    public void TryParse_ProximityMessageAfterAnotherContinuityMessage_FindsIt()
    {
        // Un mensaje «nearby» (0x10) de 2 bytes delante del de proximidad.
        AirPodsStatus status = Parse("10020B1C" + Frame(status: 0x22, pods: 0x77, chargingAndCase: 0x07));

        Assert.Equal(new BatteryReading(70, false), status.Left.Battery);
    }

    [Theory]
    [InlineData("")]
    [InlineData("07")]
    [InlineData("1005031C18D1F4")]
    [InlineData("0719012720")]
    [InlineData("0705012720020F")]
    public void TryParse_MissingOrTruncatedMessage_ReturnsFalse(string hex)
    {
        Assert.False(ProximityPairingParser.TryParse(Convert.FromHexString(hex), out AirPodsStatus? status));
        Assert.Null(status);
    }

    [Theory]
    [InlineData(0xB5, 0x05)]
    [InlineData(0x5C, 0x05)]
    [InlineData(0x55, 0x0D)]
    public void TryParse_BatteryNibbleOutOfRange_ReturnsFalse(byte pods, byte chargingAndCase)
    {
        Assert.False(ProximityPairingParser.TryParse(
            Convert.FromHexString(Frame(status: 0x20, pods, chargingAndCase)), out _));
    }

    private static AirPodsStatus Parse(string hex)
    {
        Assert.True(ProximityPairingParser.TryParse(Convert.FromHexString(hex), out AirPodsStatus? status));
        Assert.NotNull(status);
        return status;
    }

    private static string Frame(byte status, byte pods, byte chargingAndCase, byte lid = 0x11, ushort productId = 0x2027)
    {
        byte[] frame = new byte[27];
        frame[0] = 0x07;
        frame[1] = 0x19;
        frame[2] = 0x01;
        frame[3] = (byte)(productId & 0xFF);
        frame[4] = (byte)(productId >> 8);
        frame[5] = status;
        frame[6] = pods;
        frame[7] = chargingAndCase;
        frame[8] = lid;
        return Convert.ToHexString(frame);
    }
}
