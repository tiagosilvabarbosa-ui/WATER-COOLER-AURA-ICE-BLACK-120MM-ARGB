using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RiseModerCooller
{
    /// <summary>Réplica visual do display do cooler: anel ARGB girando + dois dígitos de 7 segmentos.</summary>
    public class CoolerPreview : Control
    {
        private readonly Timer _timer = new Timer();
        private float _angle;
        private string _frame = "  ";
        public string Caption = "Temp/°C";
        public bool Live = true;

        // segmentos: a b c d e f g
        private static readonly string[] DigitSegs =
        {
            "abcdef", "bc", "abdeg", "abcdg", "bcfg", "acdfg", "acdefg", "abc", "abcdefg", "abcdfg"
        };

        public CoolerPreview()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Bg;
            _timer.Interval = 33;
            _timer.Tick += delegate { _angle = (_angle + 2.2f) % 360; Invalidate(); };
            _timer.Start();
        }

        public string Frame
        {
            get { return _frame; }
            set { string v = (value ?? "  ").PadLeft(2); if (v.Length > 2) v = v.Substring(v.Length - 2); if (v != _frame) { _frame = v; Invalidate(); } }
        }

        protected override void Dispose(bool disposing) { if (disposing) _timer.Dispose(); base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.HighQuality(g);
            g.Clear(BackColor);

            float size = Math.Min(Width, Height) - 40;
            float cx = Width / 2f, cy = Height / 2f;
            RectangleF ring = new RectangleF(cx - size / 2, cy - size / 2, size, size);

            // brilho ambiente vermelho atrás do cooler
            using (GraphicsPath gp = new GraphicsPath())
            {
                RectangleF amb = ring; amb.Inflate(size * 0.12f, size * 0.12f);
                gp.AddEllipse(amb);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterColor = Theme.Alpha(Theme.Red, 90);
                    pb.SurroundColors = new[] { Theme.Alpha(Theme.Red, 0) };
                    g.FillEllipse(pb, amb);
                }
            }

            // anel ARGB: gradiente cônico simulado com fatias
            float thick = size * 0.075f;
            RectangleF ringMid = ring; ringMid.Inflate(-thick / 2, -thick / 2);
            int slices = 90;
            for (int i = 0; i < slices; i++)
            {
                float a0 = _angle + i * 360f / slices;
                float t = (float)(0.5 + 0.5 * Math.Sin((i / (float)slices) * Math.PI * 2));
                Color c = Theme.Mix(Theme.Red, Theme.Orange, t);
                using (Pen p = new Pen(c, thick)) g.DrawArc(p, ringMid, a0, 360f / slices + 0.8f);
            }
            // marcações do anel (estilo "engrenagem")
            using (Pen tick = new Pen(Color.FromArgb(90, 255, 220, 200), 1.2f))
            {
                for (int i = 0; i < 120; i++)
                {
                    double a = (i * 3 + _angle * 0.5) * Math.PI / 180;
                    float r1 = size / 2 - thick * 0.15f, r2 = size / 2 - thick * 0.55f;
                    g.DrawLine(tick, cx + (float)Math.Cos(a) * r1, cy + (float)Math.Sin(a) * r1, cx + (float)Math.Cos(a) * r2, cy + (float)Math.Sin(a) * r2);
                }
            }
            // borda interna brilhante
            RectangleF inner = ring; inner.Inflate(-thick, -thick);
            using (Pen p = new Pen(Color.FromArgb(200, 255, 90, 70), 2f)) g.DrawEllipse(p, inner);

            // vidro preto
            RectangleF glass = inner; glass.Inflate(-3, -3);
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(glass);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterPoint = new PointF(cx - glass.Width * 0.15f, cy - glass.Height * 0.2f);
                    pb.CenterColor = Color.FromArgb(24, 26, 36);
                    pb.SurroundColors = new[] { Color.FromArgb(4, 4, 7) };
                    g.FillEllipse(pb, glass);
                }
            }
            // reflexo
            using (GraphicsPath gp = new GraphicsPath())
            {
                RectangleF refl = new RectangleF(glass.X + glass.Width * 0.12f, glass.Y + glass.Height * 0.05f, glass.Width * 0.76f, glass.Height * 0.45f);
                gp.AddEllipse(refl);
                using (LinearGradientBrush lb = new LinearGradientBrush(refl, Color.FromArgb(22, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical))
                    g.FillPath(lb, gp);
            }

            // dígitos
            float dh = glass.Height * 0.36f, dw = dh * 0.52f, gap = dw * 0.32f;
            float totalW = dw * 2 + gap;
            float dx = cx - totalW / 2, dy = cy - dh / 2 - glass.Height * 0.03f;
            DrawDigit(g, _frame[0], dx, dy, dw, dh);
            DrawDigit(g, _frame[1], dx + dw + gap, dy, dw, dh);

            // gota (ícone do display original)
            float drop = glass.Width * 0.06f;
            float gx = dx + totalW + drop * 0.3f, gy = dy - drop * 0.6f;
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddArc(gx, gy + drop * 0.35f, drop, drop, 0, 180);
                gp.AddLine(gx, gy + drop * 0.85f, gx + drop / 2, gy - drop * 0.3f);
                gp.CloseFigure();
                using (Pen p = new Pen(Theme.Segment, 1.6f)) g.DrawPath(p, gp);
            }
            using (Font f = Theme.F(Math.Max(7f, glass.Width * 0.035f)))
            {
                SizeF s = g.MeasureString(Caption, f);
                using (SolidBrush b = new SolidBrush(Theme.Segment))
                    g.DrawString(Caption, f, b, cx - s.Width / 2 + dw * 0.1f, dy + dh + glass.Height * 0.04f);
            }
        }

        private void DrawDigit(Graphics g, char c, float x, float y, float w, float h)
        {
            string on = c >= '0' && c <= '9' ? DigitSegs[c - '0'] : "";
            float t = w * 0.17f;            // espessura
            float skew = w * 0.12f;         // inclinação itálica
            float hh = h / 2;
            // cada segmento: ponto inicial/final em coordenadas normalizadas antes da inclinação
            DrawSeg(g, on.IndexOf('a') >= 0, x, y, w, h, skew, t, 0, 0, w, 0, true);
            DrawSeg(g, on.IndexOf('g') >= 0, x, y, w, h, skew, t, 0, hh, w, hh, true);
            DrawSeg(g, on.IndexOf('d') >= 0, x, y, w, h, skew, t, 0, h, w, h, true);
            DrawSeg(g, on.IndexOf('f') >= 0, x, y, w, h, skew, t, 0, 0, 0, hh, false);
            DrawSeg(g, on.IndexOf('b') >= 0, x, y, w, h, skew, t, w, 0, w, hh, false);
            DrawSeg(g, on.IndexOf('e') >= 0, x, y, w, h, skew, t, 0, hh, 0, h, false);
            DrawSeg(g, on.IndexOf('c') >= 0, x, y, w, h, skew, t, w, hh, w, h, false);
        }

        private void DrawSeg(Graphics g, bool lit, float ox, float oy, float w, float h, float skew, float t,
                             float x1, float y1, float x2, float y2, bool horizontal)
        {
            float m = t * 0.62f; // recuo nas pontas
            PointF[] pts;
            if (horizontal)
            {
                pts = new[]
                {
                    new PointF(x1 + m, y1), new PointF(x1 + m + t / 2, y1 - t / 2), new PointF(x2 - m - t / 2, y2 - t / 2),
                    new PointF(x2 - m, y2), new PointF(x2 - m - t / 2, y2 + t / 2), new PointF(x1 + m + t / 2, y1 + t / 2)
                };
            }
            else
            {
                pts = new[]
                {
                    new PointF(x1, y1 + m), new PointF(x1 + t / 2, y1 + m + t / 2), new PointF(x2 + t / 2, y2 - m - t / 2),
                    new PointF(x2, y2 - m), new PointF(x2 - t / 2, y2 - m - t / 2), new PointF(x1 - t / 2, y1 + m + t / 2)
                };
            }
            for (int i = 0; i < pts.Length; i++)
            {
                float k = 1 - pts[i].Y / h; // inclina mais em cima
                pts[i] = new PointF(ox + pts[i].X + skew * k, oy + pts[i].Y);
            }
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(pts);
                if (lit)
                {
                    Theme.Glow(g, p, Theme.Segment, 3, t * 0.9f, 170);
                    using (SolidBrush b = new SolidBrush(Theme.Segment)) g.FillPath(b, p);
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(14, 120, 170, 255))) g.FillPath(b, p);
                }
            }
        }
    }
}
