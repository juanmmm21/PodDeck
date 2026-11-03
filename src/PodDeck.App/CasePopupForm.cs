using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using PodDeck.Core.Devices;
using PodDeck.Core.Presentation;

namespace PodDeck.App;

/// <summary>
/// La ventanita que sale sobre la bandeja al abrir el estuche, como la tarjeta del iPhone: nombre del
/// modelo y un anillo de batería por componente. No roba el foco y se esconde sola.
/// </summary>
internal sealed class CasePopupForm : Form
{
    private const int LowBatteryPercent = 20;

    // Medidas a 96 ppp; se escalan con el DPI del monitor en el que sale la ventana.
    private const int BaseWidth = 340;
    private const int BaseHeight = 168;
    private const int BaseMargin = 12;
    private const int BaseRingDiameter = 64;
    private const int BaseRingThickness = 6;

    private static readonly TimeSpan VisibleFor = TimeSpan.FromSeconds(8);

    private static readonly Color BackgroundColor = Color.FromArgb(0x20, 0x20, 0x22);
    private static readonly Color TrackColor = Color.FromArgb(0x3C, 0x3C, 0x40);
    private static readonly Color HealthyColor = Color.FromArgb(0x34, 0xC7, 0x59);
    private static readonly Color LowColor = Color.FromArgb(0xFF, 0x45, 0x3A);
    private static readonly Color SecondaryTextColor = Color.FromArgb(0xA0, 0xA0, 0xA6);

    private readonly System.Windows.Forms.Timer _hideTimer = new();
    private AirPodsStatus? _status;

    public CasePopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = BackgroundColor;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None;

        _hideTimer.Interval = (int)VisibleFor.TotalMilliseconds;
        _hideTimer.Tick += (_, _) => Hide();
        Click += (_, _) => Hide();
    }

    // Que salga sin quitarle el foco a lo que el usuario esté escribiendo.
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WsExToolWindow = 0x00000080;
            const int WsExNoActivate = 0x08000000;
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    /// <summary>Muestra la ventana con este estado y reinicia la cuenta atrás para esconderla.</summary>
    public void ShowStatus(AirPodsStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        _status = status;
        if (!Visible)
        {
            PlaceAboveTray();
            Show();
        }

        Invalidate();
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    /// <summary>Refresca los números si la ventana está a la vista; si no, no hace nada.</summary>
    public void UpdateStatus(AirPodsStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (Visible)
        {
            _status = status;
            Invalidate();
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        if (!Visible)
        {
            _hideTimer.Stop();
        }

        base.OnVisibleChanged(e);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Esquinas redondeadas nativas de Windows 11; en Windows 10 la llamada falla y se queda cuadrada.
        int preference = NativeMethods.DwmWindowCornerRound;
        int result = NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmWindowCornerPreference, ref preference, sizeof(int));
        if (result != 0)
        {
            Trace.TraceInformation($"Rounded corners unavailable (DwmSetWindowAttribute returned 0x{result:X8}).");
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_status is null)
        {
            return;
        }

        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        float scale = DeviceDpi / 96f;
        float margin = 18 * scale;

        using var titleFont = new Font("Segoe UI Semibold", 13 * scale, GraphicsUnit.Pixel);
        using var percentFont = new Font("Segoe UI Semibold", 15 * scale, GraphicsUnit.Pixel);
        using var labelFont = new Font("Segoe UI", 12 * scale, GraphicsUnit.Pixel);
        using var titleBrush = new SolidBrush(Color.White);
        using var secondaryBrush = new SolidBrush(SecondaryTextColor);
        using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        graphics.DrawString(_status.DisplayName, titleFont, titleBrush, margin, margin * 0.8f);

        IReadOnlyList<BatteryGauge> gauges = StatusFormatter.PopupGauges(_status);
        float diameter = BaseRingDiameter * scale;
        float thickness = BaseRingThickness * scale;
        float slotWidth = (ClientSize.Width - (2 * margin)) / gauges.Count;
        float ringTop = 52 * scale;

        for (int index = 0; index < gauges.Count; index++)
        {
            BatteryGauge gauge = gauges[index];
            float centerX = margin + (slotWidth * index) + (slotWidth / 2);
            var ring = new RectangleF(centerX - (diameter / 2), ringTop, diameter, diameter);
            DrawRing(graphics, ring, thickness, gauge);
            graphics.DrawString(gauge.PercentText, percentFont, titleBrush, ring, centered);

            string label = gauge.Battery.IsCharging ? $"{gauge.Label} ⚡" : gauge.Label;
            var labelBounds = new RectangleF(centerX - (slotWidth / 2), ring.Bottom + (6 * scale), slotWidth, 20 * scale);
            graphics.DrawString(label, labelFont, secondaryBrush, labelBounds, centered);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hideTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void DrawRing(Graphics graphics, RectangleF bounds, float thickness, BatteryGauge gauge)
    {
        RectangleF inset = RectangleF.Inflate(bounds, -thickness / 2, -thickness / 2);
        using (var track = new Pen(TrackColor, thickness))
        {
            graphics.DrawEllipse(track, inset);
        }

        if (gauge.Fill is not double fill || fill <= 0)
        {
            return;
        }

        Color color = gauge.Battery.Percent < LowBatteryPercent ? LowColor : HealthyColor;
        using var pen = new Pen(color, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        // Empieza arriba (−90°) y gira en el sentido de las agujas, como el indicador del iPhone.
        graphics.DrawArc(pen, inset, -90, (float)(360 * Math.Min(fill, 1.0)));
    }

    private void PlaceAboveTray()
    {
        // La bandeja casi siempre está en la pantalla principal; WorkingArea ya descuenta la barra de tareas.
        Screen screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        Rectangle area = screen.WorkingArea;
        float scale = DeviceDpi / 96f;
        var size = new Size((int)(BaseWidth * scale), (int)(BaseHeight * scale));
        int margin = (int)(BaseMargin * scale);
        Bounds = new Rectangle(area.Right - size.Width - margin, area.Bottom - size.Height - margin, size.Width, size.Height);
    }
}
