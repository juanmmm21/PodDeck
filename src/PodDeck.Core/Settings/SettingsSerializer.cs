using System.Text.Json;
using System.Text.Json.Serialization;

namespace PodDeck.Core.Settings;

/// <summary>Resultado de leer el fichero de ajustes.</summary>
/// <param name="Settings">Ajustes listos para usar; nunca nulos.</param>
/// <param name="Problem">Por qué se ignoró algo del fichero, o <c>null</c> si se leyó entero.</param>
public sealed record SettingsReadResult(AppSettings Settings, string? Problem);

/// <summary>
/// Pasa <see cref="AppSettings"/> a JSON y de vuelta. Leer nunca falla: un fichero roto o un valor
/// fuera de rango no puede impedir que la app arranque, así que lo que no sirve vuelve a su valor por
/// defecto y se cuenta en <see cref="SettingsReadResult.Problem"/>.
/// </summary>
public static class SettingsSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Serialize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonSerializer.Serialize(new SettingsDocument(settings.AutoPauseEnabled, settings.LowBatteryThresholdPercent), Options);
    }

    public static SettingsReadResult Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        SettingsDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<SettingsDocument>(json, Options);
        }
        catch (JsonException exception)
        {
            return new SettingsReadResult(AppSettings.Default, $"Settings file is not valid JSON: {exception.Message}");
        }

        if (document is null)
        {
            return new SettingsReadResult(AppSettings.Default, "Settings file is empty.");
        }

        AppSettings defaults = AppSettings.Default;
        bool autoPause = document.AutoPauseEnabled ?? defaults.AutoPauseEnabled;
        int threshold = defaults.LowBatteryThresholdPercent;
        string? problem = null;
        if (document.LowBatteryThresholdPercent is int stored)
        {
            if (AppSettings.IsValidThreshold(stored))
            {
                threshold = stored;
            }
            else
            {
                problem = $"Ignored unsupported low battery threshold {stored}%.";
            }
        }

        return new SettingsReadResult(new AppSettings(autoPause, threshold), problem);
    }

    // Campos anulables para distinguir «no está en el fichero» de un valor guardado: un fichero de una
    // versión anterior con menos campos se lee sin perder lo que sí trae.
    private sealed record SettingsDocument(bool? AutoPauseEnabled, int? LowBatteryThresholdPercent);
}
