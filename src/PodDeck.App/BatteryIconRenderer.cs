using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;

namespace PodDeck.App;

/// <summary>Dibuja el icono de la bandeja: el porcentaje del auricular más bajo sobre un fondo de color.</summary>
internal static class BatteryIconRenderer
{
    private const int LowBatteryPercent = 20;

    private static readonly Color HealthyColor = Color.FromArgb(0x2E, 0x9E, 0x5B);
    private static readonly Color LowColor = Color.FromArgb(0xD9, 0x3F, 0x3F);
    private static readonly Color UnknownColor = Color.FromArgb(0x6B, 0x6B, 0x6B);

    /// <summary>Crea un icono nuevo; quien lo recibe es dueño de él y debe liberarlo.</summary>
    public static Icon Render(int? percent, Size size)
    {
        using var bitmap = new Bitmap(size.Width, size.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);

            Color background = percent switch
            {
                null => UnknownColor,
                < LowBatteryPercent => LowColor,
                _ => HealthyColor,
            };
            using (var brush = new SolidBrush(background))
            using (GraphicsPath shape = RoundedRectangle(new Rectangle(0, 0, size.Width - 1, size.Height - 1), size.Width / 4))
            {
                graphics.FillPath(brush, shape);
            }

            // 100 no cabe legible en 16 px: se dibuja como «F» (full), igual que hacen otras apps de bandeja.
            string label = percent switch
            {
                null => "–",
                100 => "F",
                int value => value.ToString(CultureInfo.InvariantCulture),
            };
            float fontSize = size.Height * (label.Length > 1 ? 0.5f : 0.62f);
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString(label, font, Brushes.White, new RectangleF(0, 0, size.Width, size.Height), format);
        }

        IntPtr handle = bitmap.GetHicon();
        using Icon borrowed = Icon.FromHandle(handle);
        var icon = (Icon)borrowed.Clone();
        if (!NativeMethods.DestroyIcon(handle))
        {
            // Perder un HICON no rompe nada visible, pero repetido agota los objetos GDI del proceso.
            Trace.TraceWarning($"DestroyIcon failed with Win32 error {Marshal.GetLastPInvokeError()}.");
        }

        return icon;
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
