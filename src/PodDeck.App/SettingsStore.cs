using PodDeck.Core.Settings;

namespace PodDeck.App;

/// <summary>Guarda los ajustes en <c>%APPDATA%\PodDeck\settings.json</c>.</summary>
internal sealed class SettingsStore
{
    private readonly string _path;

    public SettingsStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodDeck", "settings.json"))
    {
    }

    public SettingsStore(string path) => _path = path;

    public AppSettings Load()
    {
        string json;
        try
        {
            json = File.ReadAllText(_path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return AppSettings.Default;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning($"Could not read settings from {_path}: {exception.Message}");
            return AppSettings.Default;
        }

        SettingsReadResult result = SettingsSerializer.Deserialize(json);
        if (result.Problem is not null)
        {
            System.Diagnostics.Trace.TraceWarning($"{_path}: {result.Problem}");
        }

        return result.Settings;
    }

    /// <summary>Devuelve false si no se pudo guardar; la app sigue con los ajustes en memoria.</summary>
    public bool TrySave(AppSettings settings)
    {
        string temporaryPath = _path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            // Se escribe aparte y se sustituye de golpe: un corte a mitad no deja un fichero a medias.
            File.WriteAllText(temporaryPath, SettingsSerializer.Serialize(settings));
            File.Move(temporaryPath, _path, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning($"Could not save settings to {_path}: {exception.Message}");
            return false;
        }
    }
}
