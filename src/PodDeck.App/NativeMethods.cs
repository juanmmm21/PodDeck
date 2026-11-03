using System.Runtime.InteropServices;

namespace PodDeck.App;

internal static partial class NativeMethods
{
    // Icon.FromHandle no se adueña del HICON que crea Bitmap.GetHicon: hay que liberarlo a mano.
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(IntPtr handle);
}
