using PodDeck.Core.Alerts;

namespace PodDeck.Core.Settings;

/// <summary>Lo que el usuario elige en el menú y debe sobrevivir a un reinicio.</summary>
/// <param name="AutoPauseEnabled">Pausar la reproducción al quitarse un auricular.</param>
/// <param name="LowBatteryThresholdPercent">Umbral del aviso de batería baja; 0 lo apaga.</param>
public sealed record AppSettings(bool AutoPauseEnabled, int LowBatteryThresholdPercent)
{
    public const int LowBatteryOff = 0;

    public static AppSettings Default { get; } = new(AutoPauseEnabled: true, LowBatteryMonitor.DefaultThresholdPercent);

    public bool IsLowBatteryAlertEnabled => LowBatteryThresholdPercent != LowBatteryOff;

    /// <summary>Solo valen los umbrales que ofrece el menú o apagado.</summary>
    public static bool IsValidThreshold(int percent) =>
        percent == LowBatteryOff || LowBatteryMonitor.ThresholdChoices.Contains(percent);
}
