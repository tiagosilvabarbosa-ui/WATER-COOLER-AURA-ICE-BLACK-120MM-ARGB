using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace RiseModerCooller
{
    /// <summary>Logo do RiseModer Cooller (hexágono neon com "R"), usado pelo app e pelo instalador.</summary>
    public static class AppIcon
    {
        public static void DrawLogo(Graphics g, float x, float y, float s)
        {
            PointF[] hex = new PointF[6];
            for (int i = 0; i < 6; i++)
            {
                double a = Math.PI / 3 * i - Math.PI / 2;
                hex[i] = new PointF(x + s / 2 + (float)Math.Cos(a) * s / 2, y + s / 2 + (float)Math.Sin(a) * s / 2);
            }
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(hex);
                Theme.Glow(g, p, Theme.Red, 3, 8, 220);
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x, y, s, s), Theme.Red, Theme.Orange, 45f)) g.FillPath(b, p);
            }
            using (Font f = Theme.F(s * 0.42f, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("R", f, Brushes.White, new RectangleF(x, y + 1, s, s), sf);
        }

        public static Icon Create(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    Theme.HighQuality(g);
                    g.Clear(Color.Transparent);
                    DrawLogo(g, size * 0.06f, size * 0.06f, size * 0.88f);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }
}
