using PodDeck.Core.Settings;

namespace PodDeck.Core.Tests.Settings;

public sealed class SettingsSerializerTests
{
    [Fact]
    public void RoundTrip_KeepsEveryValue()
    {
        var settings = new AppSettings(AutoPauseEnabled: false, LowBatteryThresholdPercent: 30);

        SettingsReadResult result = SettingsSerializer.Deserialize(SettingsSerializer.Serialize(settings));

        Assert.Equal(settings, result.Settings);
        Assert.Null(result.Problem);
    }

    [Fact]
    public void RoundTrip_AlertOff()
    {
        var settings = new AppSettings(AutoPauseEnabled: true, AppSettings.LowBatteryOff);

        AppSettings read = SettingsSerializer.Deserialize(SettingsSerializer.Serialize(settings)).Settings;

        Assert.Equal(settings, read);
        Assert.False(read.IsLowBatteryAlertEnabled);
    }

    [Fact]
    public void Serialize_UsesReadableCamelCaseNames()
    {
        string json = SettingsSerializer.Serialize(AppSettings.Default);

        Assert.Contains("\"autoPauseEnabled\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"lowBatteryThresholdPercent\": 20", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_MissingFields_FallBackToDefaults()
    {
        SettingsReadResult result = SettingsSerializer.Deserialize("""{ "autoPauseEnabled": false }""");

        Assert.Equal(new AppSettings(false, AppSettings.Default.LowBatteryThresholdPercent), result.Settings);
        Assert.Null(result.Problem);
    }

    [Fact]
    public void Deserialize_IgnoresUnknownFields()
    {
        SettingsReadResult result = SettingsSerializer.Deserialize("""{ "lowBatteryThresholdPercent": 10, "future": [1, 2] }""");

        Assert.Equal(new AppSettings(true, 10), result.Settings);
        Assert.Null(result.Problem);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(-5)]
    [InlineData(200)]
    public void Deserialize_UnsupportedThreshold_UsesDefaultAndReports(int threshold)
    {
        SettingsReadResult result = SettingsSerializer.Deserialize($$"""{ "autoPauseEnabled": false, "lowBatteryThresholdPercent": {{threshold}} }""");

        Assert.Equal(new AppSettings(false, AppSettings.Default.LowBatteryThresholdPercent), result.Settings);
        Assert.NotNull(result.Problem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("""{ "autoPauseEnabled": "yes" }""")]
    [InlineData("[]")]
    public void Deserialize_BrokenFile_UsesDefaultsAndReports(string json)
    {
        SettingsReadResult result = SettingsSerializer.Deserialize(json);

        Assert.Equal(AppSettings.Default, result.Settings);
        Assert.NotNull(result.Problem);
    }

    [Fact]
    public void Deserialize_Null_UsesDefaultsAndReports()
    {
        SettingsReadResult result = SettingsSerializer.Deserialize("null");

        Assert.Equal(AppSettings.Default, result.Settings);
        Assert.NotNull(result.Problem);
    }
}
