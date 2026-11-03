namespace PodDeck.Core.Settings;

/// <summary>
/// La línea de comandos que se guarda en la clave Run de Windows para arrancar con la sesión.
/// </summary>
public static class StartupCommand
{
    /// <summary>Nombre del valor dentro de <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.</summary>
    public const string RunValueName = "PodDeck";

    /// <summary>
    /// Entre comillas siempre: Windows parte la línea por espacios y una ruta como
    /// <c>C:\Users\Ana María\…</c> arrancaría otra cosa o nada.
    /// </summary>
    public static string For(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (executablePath.Contains('"', StringComparison.Ordinal))
        {
            throw new ArgumentException("An executable path cannot contain quotes.", nameof(executablePath));
        }

        return $"\"{executablePath}\"";
    }

    /// <summary>
    /// Si el valor guardado arranca este mismo ejecutable. Las rutas de Windows no distinguen
    /// mayúsculas y el valor puede haberlo escrito otra herramienta sin comillas.
    /// </summary>
    public static bool Launches(string? storedCommand, string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (string.IsNullOrWhiteSpace(storedCommand))
        {
            return false;
        }

        string command = storedCommand.Trim();
        string path = command;
        if (command.StartsWith('"'))
        {
            int closing = command.IndexOf('"', 1);
            path = closing > 0 ? command[1..closing] : command[1..];
        }

        return string.Equals(path, executablePath, StringComparison.OrdinalIgnoreCase);
    }
}
