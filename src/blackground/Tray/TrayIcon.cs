using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using WinForms = System.Windows.Forms;

namespace blackground.Tray;

public sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _notify;
    private bool _isActive;

    public event EventHandler? ToggleRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? AboutRequested;
    public event EventHandler? ExitRequested;

    public TrayIcon()
    {
        _notify = new WinForms.NotifyIcon
        {
            Icon = BuildIcon(filled: false),
            Text = "blackground",
            Visible = true,
        };

        var menu = new WinForms.ContextMenuStrip();

        var settings = new WinForms.ToolStripMenuItem("Settings...");
        settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(settings);

        var about = new WinForms.ToolStripMenuItem("About");
        about.Click += (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(about);

        menu.Items.Add(new WinForms.ToolStripSeparator());
        var exit = new WinForms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exit);

        _notify.ContextMenuStrip = menu;
        _notify.MouseClick += OnMouseClick;
        _notify.DoubleClick += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMouseClick(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button == WinForms.MouseButtons.Left)
        {
            ToggleRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetActive(bool active)
    {
        if (_isActive == active) return;
        _isActive = active;
        var old = _notify.Icon;
        _notify.Icon = BuildIcon(filled: active);
        old?.Dispose();
        _notify.Text = active ? "blackground (active)" : "blackground";
    }

    public void ShowBalloon(string title, string text, WinForms.ToolTipIcon icon = WinForms.ToolTipIcon.Info)
    {
        try
        {
            _notify.BalloonTipTitle = title;
            _notify.BalloonTipText = text;
            _notify.BalloonTipIcon = icon;
            _notify.ShowBalloonTip(4000);
        }
        catch { /* swallow */ }
    }

    private static Icon BuildIcon(bool filled)
    {
        // Renders the embedded logo.png as a 32x32 tray icon. Active state inverts RGB
        // (black<->white) to preserve the same color-flip semantics the procedural icon had.
        const int size = 32;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            using var logo = LoadLogoBitmap();
            if (logo is not null)
            {
                if (filled)
                {
                    var matrix = new ColorMatrix(new float[][]
                    {
                        new[] { -1f,  0f,  0f, 0f, 0f },
                        new[] {  0f, -1f,  0f, 0f, 0f },
                        new[] {  0f,  0f, -1f, 0f, 0f },
                        new[] {  0f,  0f,  0f, 1f, 0f },
                        new[] {  1f,  1f,  1f, 0f, 1f },
                    });
                    using var attrs = new ImageAttributes();
                    attrs.SetColorMatrix(matrix);
                    g.DrawImage(logo, new Rectangle(0, 0, size, size),
                        0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, attrs);
                }
                else
                {
                    g.DrawImage(logo, 0, 0, size, size);
                }
            }
            else
            {
                // Fallback if logo failed to load — keeps the app usable.
                using var brush = new SolidBrush(filled ? Color.Black : Color.White);
                g.FillEllipse(brush, 2, 2, size - 4, size - 4);
            }
        }
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(hIcon);
            // Clone so we own the icon's resources independently of the bitmap.
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static Bitmap? LoadLogoBitmap()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/logo.png", UriKind.Absolute);
            var sri = System.Windows.Application.GetResourceStream(uri);
            if (sri is null) return null;
            using var stream = sri.Stream;
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
    }
}
