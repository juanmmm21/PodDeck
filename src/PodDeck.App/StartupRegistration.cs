using Microsoft.Win32;
using PodDeck.Core.Settings;

namespace PodDeck.App;

/// <summary>
/// Arranque con Windows mediante la clave Run del usuario: no pide permisos de administrador ni
/// instalador, y quitar el valor lo deshace por completo.
/// </summary>
internal sealed class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string? _executablePath;

    public StartupRegistration()
    {
        // Lanzada con «dotnet PodDeck.dll» la ruta del proceso es la de dotnet.exe: registrar eso no
        // arrancaría PodDeck, así que solo se ofrece con el .exe publicado.
        string? processPath = Environment.ProcessPath;
        _executablePath = processPath is not null
            && string.Equals(Path.GetFileName(processPath), "PodDeck.exe", StringComparison.OrdinalIgnoreCase)
            ? processPath
            : null;
    }

    public bool IsAvailable => _executablePath is not null;

    public bool IsEnabled()
    {
        if (_executablePath is null)
        {
            return false;
        }

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return StartupCommand.Launches(key?.GetValue(StartupCommand.RunValueName) as string, _executablePath);
    }

    /// <summary>Devuelve false si el registro no dejó cambiarlo.</summary>
    public bool TrySet(bool enabled)
    {
        if (_executablePath is null)
        {
            return false;
        }

        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (enabled)
            {
                key.SetValue(StartupCommand.RunValueName, StartupCommand.For(_executablePath), RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(StartupCommand.RunValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            System.Diagnostics.Trace.TraceWarning($"Could not change the startup entry: {exception.Message}");
            return false;
        }
    }
}
