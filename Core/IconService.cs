using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace WebToAppStudio.Core;

/// <summary>Renders launcher icons from the user's image (or a generated letter icon).</summary>
[SupportedOSPlatform("windows")]
public static class IconService
{
    public static void SavePng(AppConfig c, string dest, int size, IconKind kind)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        using var bmp = Render(c, size, kind);
        bmp.Save(dest, ImageFormat.Png);
    }

    public static Bitmap Render(AppConfig c, int size, IconKind kind)
    {
        var format = kind == IconKind.Opaque ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb;
        var bmp = new Bitmap(size, size, format);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        Color theme;
        try { theme = ColorTranslator.FromHtml(c.ThemeColor); } catch { theme = Color.FromArgb(10, 132, 255); }

        g.Clear(kind == IconKind.AdaptiveForeground ? Color.Transparent : theme);

        var area = kind == IconKind.AdaptiveForeground
            ? new RectangleF(size * 0.17f, size * 0.17f, size * 0.66f, size * 0.66f)
            : new RectangleF(0, 0, size, size);

        Image? src = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(c.IconPath) && File.Exists(c.IconPath))
                src = Image.FromFile(c.IconPath);
        }
        catch { src = null; }

        try
        {
            if (src != null)
            {
                float s = Math.Min(src.Width, src.Height);
                var crop = new RectangleF((src.Width - s) / 2f, (src.Height - s) / 2f, s, s);
                g.DrawImage(src, area, crop, GraphicsUnit.Pixel);
            }
            else
            {
                var letter = (c.AppName.Trim().Length > 0 ? c.AppName.Trim()[0].ToString() : "A").ToUpperInvariant();
                using var font = new Font("Segoe UI", area.Height * 0.55f, FontStyle.Bold, GraphicsUnit.Pixel);
                using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(letter, font, Brushes.White, area, fmt);
            }
        }
        finally { src?.Dispose(); }

        return bmp;
    }
}
