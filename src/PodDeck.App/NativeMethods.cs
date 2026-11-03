using System.Runtime.InteropServices;

namespace PodDeck.App;

internal static partial class NativeMethods
{
    internal const int DwmWindowCornerPreference = 33;
    internal const int DwmWindowCornerRound = 2;

    // Icon.FromHandle no se adueña del HICON que crea Bitmap.GetHicon: hay que liberarlo a mano.
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(IntPtr handle);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);
}
