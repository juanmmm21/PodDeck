using PodDeck.Core.Pairing;

namespace PodDeck.Core.Tests.Pairing;

public sealed class PairedDeviceIdTests
{
    // Leído el 2026-11-03 del Administrador de dispositivos del PC de Juan: servicio A2DP de sus AirPods Pro 3.
    private const string RealAirPodsPro3AudioSink =
        @"BTHENUM\{0000110B-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&2027\A&1D440A10&0&74778624D7BB_C00000000";

    [Fact]
    public void TryParseAppleProductId_RealAirPodsPro3Service_ReturnsProductId()
    {
        Assert.True(PairedDeviceId.TryParseAppleProductId(RealAirPodsPro3AudioSink, out ushort productId));
        Assert.Equal((ushort)0x2027, productId);
    }

    [Fact]
    public void TryParseAppleProductId_LowercaseId_IsAccepted()
    {
        Assert.True(PairedDeviceId.TryParseAppleProductId(RealAirPodsPro3AudioSink.ToLowerInvariant(), out ushort productId));
        Assert.Equal((ushort)0x2027, productId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"BTHENUM\Dev_74778624D7BB\A&1D440A10&0&BluetoothDevice_74778624D7BB")]
    [InlineData(@"BTHENUM\{0000110B-0000-1000-8000-00805F9B34FB}_VID&000205AC_PID&2027\X")]
    [InlineData(@"BTHENUM\{0000110B-0000-1000-8000-00805F9B34FB}_VID&0001004D_PID&2027\X")]
    [InlineData(@"BTHENUM\{0000110B-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&20")]
    [InlineData(@"BTHENUM\{0000110B-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&ZZZZ\X")]
    [InlineData(@"USB\VID_046D&PID_C52B\5&2B6F2A8&0&2")]
    public void TryParseAppleProductId_NotAnAppleBluetoothService_ReturnsFalse(string? instanceId)
    {
        Assert.False(PairedDeviceId.TryParseAppleProductId(instanceId, out _));
    }
}
