using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RiseModerCooller
{
    public class NeonControl : Control
    {
        protected bool Hover, Pressed;
        public NeonControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = false; Pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { Pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Pressed = false; Invalidate(); base.OnMouseUp(e); }
    }

    /// <summary>Botão com gradiente e brilho neon.</summary>
    public class NeonButton : NeonControl
    {
        public Color Accent = Theme.Red;
        public Color Accent2 = Theme.Orange;
        public bool Primary = true;
        public string Icon = "";

        public NeonButton() { Font = Theme.F(11f, FontStyle.Bold); Size = new Size(200, 48); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.HighQuality(g);
            g.Clear(BackColor);
            RectangleF r = new RectangleF(4, 4, Width - 9, Height - 9);
            if (Pressed) r.Inflate(-1, -1);
            using (GraphicsPath path = Theme.Round(r, 10))
            {
                if (Primary)
                {
                    if (Hover) Theme.Glow(g, path, Accent, 4, 9, 255);
                    using (LinearGradientBrush b = new LinearGradientBrush(r, Accent, Accent2, LinearGradientMode.Horizontal))
                        g.FillPath(b, path);
                    using (LinearGradientBrush shine = new LinearGradientBrush(r, Color.FromArgb(60, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical))
                        g.FillPath(shine, path);
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(Hover ? Theme.Mix(Theme.Input, Accent, 0.12f) : Theme.Input)) g.FillPath(b, path);
                    using (Pen p = new Pen(Hover ? Accent : Theme.Alpha(Accent, 150), 1.4f)) g.DrawPath(p, path);
                }
            }
            string text = string.IsNullOrEmpty(Icon) ? Text : Icon + "  " + Text;
            Color fc = Enabled ? (Primary ? Color.White : Theme.Mix(Theme.Text, Accent, Hover ? 0.4f : 0.15f)) : Theme.TextMuted;
            TextRenderer.DrawText(g, text, Font, Rectangle.Round(r), fc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>Seletor de opções estilo "abas" com indicador animado.</summary>
    public class SegmentedControl : NeonControl
    {
        private string[] _items = new string[0];
        private int _selected;
        private int _hoverIndex = -1;
        private float _anim; // posição animada do indicador
        private readonly Timer _timer = new Timer();
        public Color Accent = Theme.Red;
        public event EventHandler SelectedIndexChanged;

        public SegmentedControl()
        {
            Font = Theme.F(9.5f, FontStyle.Bold);
            Height = 42;
            _timer.Interval = 15;
            _timer.Tick += delegate
            {
                float diff = _selected - _anim;
                if (Math.Abs(diff) < 0.01f) { _anim = _selected; _timer.Stop(); }
                else _anim += diff * 0.28f;
                Invalidate();
            };
        }

        [Browsable(false)]
        public string[] Items { get { return _items; } set { _items = value ?? new string[0]; Invalidate(); } }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (value < 0 || value >= _items.Length || value == _selected) return;
                _selected = value;
                _timer.Start();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public void SetIndexSilently(int i) { _selected = i; _anim = i; Invalidate(); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = _items.Length == 0 ? -1 : Math.Min(_items.Length - 1, e.X * _items.Length / Math.Max(1, Width));
            if (idx != _hoverIndex) { _hoverIndex = idx; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { _hoverIndex = -1; base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (_items.Length > 0) SelectedIndex = Math.Min(_items.Length - 1, e.X * _items.Length / Math.Max(1, Width));
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.HighQuality(g);
            g.Clear(BackColor);
            RectangleF outer = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Theme.Round(outer, 9))
            {
                using (SolidBrush b = new SolidBrush(Theme.Input)) g.FillPath(b, p);
                using (Pen pen = new Pen(Theme.CardBorder)) g.DrawPath(pen, p);
            }
            if (_items.Length == 0) return;
            float w = (Width - 8f) / _items.Length;
            RectangleF sel = new RectangleF(4 + _anim * w, 4, w, Height - 9);
            using (GraphicsPath p = Theme.Round(sel, 7))
            {
                Theme.Glow(g, p, Accent, 3, 6, 200);
                using (LinearGradientBrush b = new LinearGradientBrush(sel, Accent, Theme.Mix(Accent, Theme.Orange, 0.55f), LinearGradientMode.Horizontal))
                    g.FillPath(b, p);
            }
            for (int i = 0; i < _items.Length; i++)
            {
                Rectangle r = Rectangle.Round(new RectangleF(4 + i * w, 4, w, Height - 9));
                Color c = i == _selected ? Color.White : (i == _hoverIndex ? Theme.Text : Theme.TextDim);
                TextRenderer.DrawText(g, _items[i], Font, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>Slider neon.</summary>
    public class NeonSlider : NeonControl
    {
        private int _value = 50;
        public int Minimum = 0, Maximum = 100;
        public Color Accent = Theme.Red;
        public event EventHandler ValueChanged;
        private bool _drag;

        public NeonSlider() { Height = 30; }

        public int Value
        {
            get { return _value; }
            set
            {
                int v = Math.Max(Minimum, Math.Min(Maximum, value));
                if (v == _value) return;
                _value = v; Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        private void SetFromX(int x)
        {
            float t = (x - 10f) / Math.Max(1, Width - 20f);
            Value = Minimum + (int)Math.Round(Math.Max(0, Math.Min(1, t)) * (Maximum - Minimum));
        }
        protected override void OnMouseDown(MouseEventArgs e) { _drag = true; SetFromX(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (_drag) SetFromX(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _drag = false; base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.HighQuality(g);
            g.Clear(BackColor);
            float cy = Height / 2f;
            float t = (float)(_value - Minimum) / Math.Max(1, Maximum - Minimum);
            float x = 10 + t * (Width - 20);
            RectangleF track = new RectangleF(10, cy - 3, Width - 20, 6);
            using (GraphicsPath p = Theme.Round(track, 3)) using (SolidBrush b = new SolidBrush(Theme.Input)) g.FillPath(b, p);
            if (x > 11)
            {
                RectangleF fill = new RectangleF(10, cy - 3, x - 10, 6);
                using (GraphicsPath p = Theme.Round(fill, 3))
                {
                    Theme.Glow(g, p, Accent, 3, 6, 160);
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(10, 0, Width - 20, 1), Theme.Orange, Accent, LinearGradientMode.Horizontal))
                        g.FillPath(b, p);
                }
            }
            float r = Hover || _drag ? 9 : 8;
            using (SolidBrush glow = new SolidBrush(Theme.Alpha(Accent, 70))) g.FillEllipse(glow, x - r - 4, cy - r - 4, (r + 4) * 2, (r + 4) * 2);
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, x - r, cy - r, r * 2, r * 2);
            using (SolidBrush b = new SolidBrush(Accent)) g.FillEllipse(b, x - 3.5f, cy - 3.5f, 7, 7);
        }
    }

    /// <summary>Chave liga/desliga.</summary>
    public class NeonToggle : NeonControl
    {
        private bool _checked;
        private float _anim;
        private readonly Timer _timer = new Timer();
        public Color Accent = Theme.Red;
        public event EventHandler CheckedChanged;

        public NeonToggle()
        {
            Font = Theme.F(10f);
            Height = 30;
            _timer.Interval = 15;
            _timer.Tick += delegate
            {
                float target = _checked ? 1 : 0;
                _anim += (target - _anim) * 0.3f;
                if (Math.Abs(target - _anim) < 0.01f) { _anim = target; _timer.Stop(); }
                Invalidate();
            };
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value; _timer.Start();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }
        public void SetSilently(bool v) { _checked = v; _anim = v ? 1 : 0; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e) { Checked = !Checked; base.OnMouseClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.HighQuality(g);
            g.Clear(BackColor);
            RectangleF sw = new RectangleF(1, (Height - 22) / 2f, 42, 22);
            using (GraphicsPath p = Theme.Round(sw, 11))
            {
                if (_anim > 0.05f) Theme.Glow(g, p, Accent, 3, 6, (int)(180 * _anim));
                using (SolidBrush b = new SolidBrush(Theme.Mix(Theme.Input, Accent, _anim))) g.FillPath(b, p);
                using (Pen pen = new Pen(Theme.Mix(Theme.CardBorder, Accent, _anim))) g.DrawPath(pen, p);
            }
            float kx = sw.X + 3 + _anim * (sw.Width - 22);
            using (SolidBrush b = new SolidBrush(Theme.Mix(Theme.TextDim, Color.White, _anim))) g.FillEllipse(b, kx, sw.Y + 3, 16, 16);
            Rectangle tr = new Rectangle(52, 0, Width - 52, Height);
            TextRenderer.DrawText(g, Text, Font, tr, Hover ? Theme.Text : Theme.Mix(Theme.TextDim, Theme.Text, 0.5f),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Cartão com título, usado para agrupar opções.</summary>
    public class Card : Panel
    {
        public string Title = "";
        public string Badge = "";
        public Color Accent = Theme.Red;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Card;
            Padding = new Padding(16, 38, 16, 14);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.HighQuality(g);
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Theme.Round(r, 12))
            {
                using (SolidBrush b = new SolidBrush(BackColor)) g.FillPath(b, p);
                using (Pen pen = new Pen(Theme.CardBorder)) g.DrawPath(pen, p);
            }
            // marcador neon do título
            using (SolidBrush b = new SolidBrush(Accent)) g.FillRectangle(b, 16, 15, 3, 13);
            using (Font f = Theme.F(9f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, Title.ToUpperInvariant(), f, new Point(24, 13), Theme.TextDim);
                if (!string.IsNullOrEmpty(Badge))
                {
                    Size s = TextRenderer.MeasureText(Badge, f);
                    TextRenderer.DrawText(g, Badge, f, new Point(Width - 16 - s.Width, 13), Theme.Mix(Theme.TextDim, Accent, 0.7f));
                }
            }
        }
    }

    /// <summary>Caixa de texto com borda neon quando focada.</summary>
    public class NeonTextBox : Panel
    {
        public readonly TextBox Box = new TextBox();
        public Color Accent = Theme.Red;

        public NeonTextBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Card;
            Height = 52;
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Theme.Input;
            Box.ForeColor = Theme.Text;
            Box.Font = Theme.F(17f, FontStyle.Bold);
            Box.CharacterCasing = CharacterCasing.Upper;
            Box.MaxLength = 40;
            Controls.Add(Box);
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            Cursor = Cursors.IBeam;
            Click += delegate { Box.Focus(); };
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Box.Location = new Point(16, (Height - Box.Height) / 2);
            Box.Width = Width - 32;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.HighQuality(g);
            g.Clear(BackColor);
            RectangleF r = new RectangleF(3, 3, Width - 7, Height - 7);
            using (GraphicsPath p = Theme.Round(r, 9))
            {
                if (Box.Focused) Theme.Glow(g, p, Accent, 3, 7, 200);
                using (SolidBrush b = new SolidBrush(Theme.Input)) g.FillPath(b, p);
                using (Pen pen = new Pen(Box.Focused ? Accent : Theme.CardBorder, 1.3f)) g.DrawPath(pen, p);
            }
        }
    }
}
