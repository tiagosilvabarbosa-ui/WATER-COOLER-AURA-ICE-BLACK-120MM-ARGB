using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace RiseModerCooller
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(9, 10, 15);
        public static readonly Color Bg2 = Color.FromArgb(14, 16, 23);
        public static readonly Color Card = Color.FromArgb(18, 20, 29);
        public static readonly Color CardBorder = Color.FromArgb(38, 42, 58);
        public static readonly Color Input = Color.FromArgb(11, 12, 18);
        public static readonly Color Text = Color.FromArgb(236, 239, 247);
        public static readonly Color TextDim = Color.FromArgb(128, 135, 158);
        public static readonly Color TextMuted = Color.FromArgb(78, 84, 104);
        public static readonly Color Red = Color.FromArgb(255, 36, 66);
        public static readonly Color Orange = Color.FromArgb(255, 112, 40);
        public static readonly Color Cyan = Color.FromArgb(0, 224, 255);
        public static readonly Color Segment = Color.FromArgb(96, 170, 255);
        public static readonly Color Green = Color.FromArgb(40, 230, 140);

        private static string _family;
        public static string Family
        {
            get
            {
                if (_family != null) return _family;
                using (InstalledFontCollection fc = new InstalledFontCollection())
                {
                    foreach (FontFamily f in fc.Families)
                        if (f.Name == "Bahnschrift") { _family = "Bahnschrift"; return _family; }
                }
                _family = "Segoe UI";
                return _family;
            }
        }

        public static Font F(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(Family, size, style, GraphicsUnit.Point);
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void HighQuality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Alpha(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c); }

        /// <summary>Desenha um brilho neon (várias camadas translúcidas) em volta de um caminho.</summary>
        public static void Glow(Graphics g, GraphicsPath path, Color c, int layers, float maxWidth, int maxAlpha)
        {
            for (int i = layers; i >= 1; i--)
            {
                float w = maxWidth * i / layers;
                int a = maxAlpha * (layers - i + 1) / (layers * 3);
                using (Pen p = new Pen(Alpha(c, a), w)) { p.LineJoin = LineJoin.Round; g.DrawPath(p, path); }
            }
        }
    }
}
