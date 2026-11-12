using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace RiseModerCooller
{
    public class MainForm : Form
    {
        private const string Version = "v1.5";
        private readonly AppSettings _s;
        private readonly CoolerDevice _device = new CoolerDevice();
        private readonly DisplayEngine _engine;
        private readonly System.Windows.Forms.Timer _tick = new System.Windows.Forms.Timer();
        private readonly NotifyIcon _tray = new NotifyIcon();
        private bool _active = true;
        private bool _reallyClose;
        private bool _trayHintShown;
        private float _pulse;
        private int _slowTick;

        private CoolerPreview _preview;
        private SegmentedControl _mode, _effect;
        private NeonTextBox _text;
        private NeonSlider _speed;
        private NeonToggle _tGpu, _tAuto, _tMin;
        private NeonButton _btnSend, _btnTray;
        private Card _cardText, _cardEffect, _cardInfo;
        private Label _speedLabel;

        private Rectangle _rClose, _rMin;
        private int _hoverTitleBtn; // 0 nada, 1 min, 2 close
        private readonly bool _startHidden;

        public MainForm(AppSettings settings, bool startHidden)
        {
            _s = settings;
            _startHidden = startHidden || settings.StartMinimized;
            _engine = new DisplayEngine(_s);

            Text = "RiseModer Cooller";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 700);
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            Font = Theme.F(10f);
            Icon = AppIcon.Create(64);
            SetStyle(ControlStyles.ResizeRedraw, true);

            BuildUi();
            BuildTray();

            _device.ConnectionChanged += delegate { try { BeginInvoke((MethodInvoker)delegate { Invalidate(); }); } catch { } };
#if !SCREENSHOT
            _device.Start();
#endif

            _tick.Interval = 40;
            _tick.Tick += OnTick;
            _tick.Start();
        }

        // ---------------------------------------------------------------- UI
        private void BuildUi()
        {
            _preview = new CoolerPreview();
            _preview.SetBounds(24, 76, 430, 430);
            Controls.Add(_preview);

            _cardInfo = new Card();
            _cardInfo.Title = "No display";
            _cardInfo.Badge = "7 SEGMENTOS";
            _cardInfo.Accent = Theme.Cyan;
            _cardInfo.SetBounds(24, 518, 430, 110);
            _cardInfo.Paint += PaintInfoCard;
            Controls.Add(_cardInfo);

            // MODO
            Card cardMode = new Card();
            cardMode.Title = "Modo";
            cardMode.SetBounds(478, 76, 498, 92);
            _mode = new SegmentedControl();
            _mode.Items = new[] { "NOME", "TEMPERATURA", "RELÓGIO", "NOME + TEMP" };
            _mode.SetBounds(16, 38, 466, 42);
            _mode.SetIndexSilently((int)_s.Mode);
            _mode.SelectedIndexChanged += delegate { _s.Mode = (DisplayMode)_mode.SelectedIndex; _engine.Rebuild(); SaveAndRefresh(); };
            cardMode.Controls.Add(_mode);
            Controls.Add(cardMode);

            // TEXTO
            _cardText = new Card();
            _cardText.Title = "Seu nome / texto";
            _cardText.SetBounds(478, 180, 498, 140);
            _text = new NeonTextBox();
            _text.SetBounds(16, 36, 466, 54);
            _text.Box.Text = _s.Text;
            _text.Box.TextChanged += delegate
            {
                _s.Text = _text.Box.Text;
                _engine.Rebuild();
                _cardText.Badge = _s.Text.Length + "/40";
                SaveAndRefresh();
            };
            _cardText.Badge = _s.Text.Length + "/40";
            _cardText.Controls.Add(_text);
            Label hint = new Label();
            hint.AutoSize = false;
            hint.SetBounds(18, 96, 466, 36);
            hint.ForeColor = Theme.TextDim;
            hint.BackColor = Theme.Card;
            hint.Font = Theme.F(9f);
            hint.Text = "O display só tem 2 dígitos numéricos, então cada letra vira o número mais parecido\n(A→4  E→3  S→5  T→7  O→0  B→8  G→6  I→1  Z→2), sempre 2 dígitos por vez.";
            _cardText.Controls.Add(hint);
            Controls.Add(_cardText);

            // EFEITO + VELOCIDADE
            _cardEffect = new Card();
            _cardEffect.Title = "Efeito";
            _cardEffect.SetBounds(478, 332, 498, 150);
            _effect = new SegmentedControl();
            _effect.Items = new[] { "ROLAGEM", "VAI-E-VOLTA", "DE 2 EM 2", "LETRA A LETRA" };
            _effect.SetBounds(16, 38, 466, 42);
            _effect.SetIndexSilently((int)_s.Effect);
            _effect.SelectedIndexChanged += delegate { _s.Effect = (TextEffect)_effect.SelectedIndex; _engine.Rebuild(); SaveAndRefresh(); };
            _cardEffect.Controls.Add(_effect);
            Label lblSpeed = new Label();
            lblSpeed.Text = "VELOCIDADE";
            lblSpeed.Font = Theme.F(8.5f, FontStyle.Bold);
            lblSpeed.ForeColor = Theme.TextDim;
            lblSpeed.BackColor = Theme.Card;
            lblSpeed.SetBounds(18, 98, 90, 30);
            lblSpeed.TextAlign = ContentAlignment.MiddleLeft;
            _cardEffect.Controls.Add(lblSpeed);
            _speed = new NeonSlider();
            _speed.Minimum = 0; _speed.Maximum = 100;
            _speed.SetBounds(104, 98, 300, 30);
            _speed.Value = SpeedFromMs(_s.StepMs);
            _speed.ValueChanged += delegate { _s.StepMs = MsFromSpeed(_speed.Value); UpdateSpeedLabel(); SaveAndRefresh(); };
            _cardEffect.Controls.Add(_speed);
            _speedLabel = new Label();
            _speedLabel.Font = Theme.F(9.5f, FontStyle.Bold);
            _speedLabel.ForeColor = Theme.Text;
            _speedLabel.BackColor = Theme.Card;
            _speedLabel.TextAlign = ContentAlignment.MiddleRight;
            _speedLabel.SetBounds(404, 98, 78, 30);
            _cardEffect.Controls.Add(_speedLabel);
            UpdateSpeedLabel();
            Controls.Add(_cardEffect);

            // OPÇÕES
            Card cardOpt = new Card();
            cardOpt.Title = "Opções";
            cardOpt.SetBounds(478, 494, 300, 134);
            _tGpu = MakeToggle(cardOpt, "Usar temperatura da GPU", 34, _s.UseGpu);
            _tGpu.CheckedChanged += delegate { _s.UseGpu = _tGpu.Checked; SaveAndRefresh(); };
            _tAuto = MakeToggle(cardOpt, "Iniciar com o Windows", 66, false);
            _tAuto.CheckedChanged += delegate { ThreadPool.QueueUserWorkItem(delegate { SystemTools.SetAutoStart(_tAuto.Checked); }); };
            _tMin = MakeToggle(cardOpt, "Iniciar minimizado na bandeja", 98, _s.StartMinimized);
            _tMin.CheckedChanged += delegate { _s.StartMinimized = _tMin.Checked; SaveAndRefresh(); };
            Controls.Add(cardOpt);
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool on = SystemTools.IsAutoStartEnabled();
                try { BeginInvoke((MethodInvoker)delegate { _tAuto.SetSilently(on); }); } catch { }
            });

            // AÇÕES
            _btnTray = new NeonButton();
            _btnTray.Primary = false;
            _btnTray.Accent = Theme.Cyan;
            _btnTray.Text = "OCULTAR NA BANDEJA";
            _btnTray.Font = Theme.F(9.5f, FontStyle.Bold);
            _btnTray.BackColor = Theme.Bg;
            _btnTray.SetBounds(786, 494, 194, 56);
            _btnTray.Click += delegate { HideToTray(); };
            Controls.Add(_btnTray);

            _btnSend = new NeonButton();
            _btnSend.BackColor = Theme.Bg;
            _btnSend.Font = Theme.F(12.5f, FontStyle.Bold);
            _btnSend.SetBounds(786, 556, 194, 76);
            _btnSend.Click += delegate { _active = !_active; UpdateSendButton(); Invalidate(); };
            Controls.Add(_btnSend);
            UpdateSendButton();
        }

        private NeonToggle MakeToggle(Card card, string text, int y, bool value)
        {
            NeonToggle t = new NeonToggle();
            t.Text = text;
            t.SetBounds(16, y, card.Width - 28, 30);
            t.SetSilently(value);
            card.Controls.Add(t);
            return t;
        }

        private static int MsFromSpeed(int v) { return 900 - v * 8; }          // 0 -> 900 ms, 100 -> 100 ms
        private static int SpeedFromMs(int ms) { return Math.Max(0, Math.Min(100, (900 - ms) / 8)); }
        private void UpdateSpeedLabel() { _speedLabel.Text = _s.StepMs + " ms"; }

        private void UpdateSendButton()
        {
            _btnSend.Text = _active ? "PAUSAR" : "ENVIAR";
            _btnSend.Icon = _active ? "❚❚" : "▶";
            _btnSend.Accent = _active ? Theme.Red : Theme.Green;
            _btnSend.Accent2 = _active ? Theme.Orange : Theme.Cyan;
            _btnSend.Invalidate();
            if (_trayPause != null) _trayPause.Text = _active ? "Pausar" : "Retomar";
        }

        /// <summary>Se o software do fabricante ainda estiver instalado, ele trava o cooler: oferece desinstalar.</summary>
        private void CheckConflictingSoftware()
        {
            RiseDetector.Found f = RiseDetector.Detect();
            if (f == null) return;
            if (MessageBox.Show(this,
                "Encontrei \"" + f.DisplayName + "\" instalado.\n\n" +
                "Ele usa o display do cooler em modo exclusivo e impede o RiseModer Cooller de funcionar.\n\n" +
                "Deseja desinstalá-lo agora?",
                "RiseModer Cooller", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = RiseDetector.Uninstall(f);
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (!ok) MessageBox.Show(this, "Não foi possível concluir a desinstalação. Tente pelo Painel de Controle > Programas.",
                            "RiseModer Cooller", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });
                }
                catch { }
            });
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            BeginInvoke((MethodInvoker)CheckConflictingSoftware);
        }

        private void SaveAndRefresh() { _s.Save(); _cardText.Invalidate(); _cardInfo.Invalidate(); }

        // ---------------------------------------------------------------- loop
        private void OnTick(object sender, EventArgs e)
        {
            _engine.Tick();
            string frame = _engine.Frame;
            byte value = DisplayText.FrameToValue(frame);
            _preview.Frame = DisplayText.ValueToFrame(value); // exatamente o que o cooler mostra
            _preview.Caption = _engine.Caption;
            _device.Paused = !_active;
            if (_active) _device.SetValue(value);

            _pulse += 0.08f;
            if (++_slowTick % 12 == 0)
            {
                Invalidate(new Rectangle(0, 0, Width, 64));
                Invalidate(new Rectangle(0, Height - 52, Width, 52));
            }
        }

        // ---------------------------------------------------------------- pintura
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.HighQuality(g);

            // fundo com brilho e linhas diagonais
            using (GraphicsPath gp = new GraphicsPath())
            {
                RectangleF glow = new RectangleF(-260, -300, 900, 760);
                gp.AddEllipse(glow);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterColor = Theme.Alpha(Theme.Red, 38);
                    pb.SurroundColors = new[] { Theme.Alpha(Theme.Red, 0) };
                    g.FillEllipse(pb, glow);
                }
            }
            using (Pen p = new Pen(Color.FromArgb(9, 255, 255, 255)))
                for (int x = -Height; x < Width; x += 22) g.DrawLine(p, x, Height, x + Height, 0);

            // barra de título
            using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, Width, 64), Theme.Bg2, Theme.Bg, LinearGradientMode.Vertical))
                g.FillRectangle(b, 0, 0, Width, 64);
            using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 63, Width, 2), Theme.Red, Theme.Alpha(Theme.Orange, 0), LinearGradientMode.Horizontal))
                g.FillRectangle(b, 0, 63, Width, 2);

            DrawLogo(g, 22, 14, 36);
            using (Font f1 = Theme.F(17f, FontStyle.Bold))
            using (Font f2 = Theme.F(8f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, "RISEMODER", f1, new Point(66, 9), Theme.Text);
                Size s1 = TextRenderer.MeasureText("RISEMODER", f1);
                TextRenderer.DrawText(g, "COOLLER", f1, new Point(66 + s1.Width - 10, 9), Theme.Red);
                TextRenderer.DrawText(g, "AURA ICE  •  DISPLAY CONTROL  •  " + Version, f2, new Point(69, 38), Theme.TextMuted);
            }

            // pílula de status
            bool on = _device.IsConnected;
            string st = on ? "COOLER CONECTADO" : "COOLER DESCONECTADO";
            Color sc = on ? Theme.Green : Theme.Red;
            using (Font f = Theme.F(8.5f, FontStyle.Bold))
            {
                Size ts = TextRenderer.MeasureText(st, f);
                RectangleF pill = new RectangleF(Width - 120 - ts.Width - 34, 18, ts.Width + 34, 28);
                using (GraphicsPath p = Theme.Round(pill, 14))
                {
                    using (SolidBrush b = new SolidBrush(Theme.Alpha(sc, 26))) g.FillPath(b, p);
                    using (Pen pen = new Pen(Theme.Alpha(sc, 120))) g.DrawPath(pen, p);
                }
                float pr = 4 + (float)(Math.Sin(_pulse) + 1) * 1.2f;
                using (SolidBrush b = new SolidBrush(Theme.Alpha(sc, 60))) g.FillEllipse(b, pill.X + 14 - pr - 2, pill.Y + 14 - pr - 2, (pr + 2) * 2, (pr + 2) * 2);
                using (SolidBrush b = new SolidBrush(sc)) g.FillEllipse(b, pill.X + 10, pill.Y + 10, 8, 8);
                TextRenderer.DrawText(g, st, f, new Point((int)pill.X + 24, (int)pill.Y + 6), sc);
            }

            // botões da janela
            _rMin = new Rectangle(Width - 96, 16, 36, 32);
            _rClose = new Rectangle(Width - 54, 16, 36, 32);
            if (_hoverTitleBtn == 1) using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 255, 255, 255))) g.FillRectangle(b, _rMin);
            if (_hoverTitleBtn == 2) using (SolidBrush b = new SolidBrush(Theme.Red)) g.FillRectangle(b, _rClose);
            using (Pen p = new Pen(Theme.Text, 1.5f))
            {
                g.DrawLine(p, _rMin.X + 12, _rMin.Y + 16, _rMin.Right - 12, _rMin.Y + 16);
                g.DrawLine(p, _rClose.X + 12, _rClose.Y + 10, _rClose.Right - 12, _rClose.Bottom - 10);
                g.DrawLine(p, _rClose.Right - 12, _rClose.Y + 10, _rClose.X + 12, _rClose.Bottom - 10);
            }

            // barra de status inferior
            using (SolidBrush b = new SolidBrush(Theme.Bg2)) g.FillRectangle(b, 0, Height - 52, Width, 52);
            using (Pen p = new Pen(Theme.CardBorder)) g.DrawLine(p, 0, Height - 52, Width, Height - 52);
            using (Font f = Theme.F(9f))
            {
                string left;
                if (on) left = "HID AA88:8666  •  relatório " + _device.OutputReportLength + " bytes  •  " + (_active ? "transmitindo valor " + DisplayText.FrameToValue(_engine.Frame) : "pausado");
                else left = _device.LastError ?? "Procurando cooler...";
                TextRenderer.DrawText(g, left, f, new Point(24, Height - 34), on ? Theme.TextDim : Theme.Orange);
                string right = _engine.Sensors.Status ?? "Carregando sensores...";
                Size rs = TextRenderer.MeasureText(right, f);
                TextRenderer.DrawText(g, right, f, new Point(Width - 24 - rs.Width, Height - 34), Theme.TextMuted);
            }
        }

        private void PaintInfoCard(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.HighQuality(g);
            string glyphs = DisplayText.ToGlyphs(_s.Text);
            using (Font fSmall = Theme.F(9f))
            using (Font fBig = new Font("Consolas", 22f, FontStyle.Bold))
            {
                string txt = string.IsNullOrEmpty(_s.Text) ? "—" : _s.Text;
                TextRenderer.DrawText(g, txt, fSmall, new Rectangle(18, 40, 394, 18), Theme.TextDim, TextFormatFlags.EndEllipsis);
                string shown = glyphs.Length == 0 ? "--" : glyphs.Replace(" ", "");
                TextRenderer.DrawText(g, shown, fBig, new Rectangle(14, 58, 400, 40), Theme.Segment, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            }
        }

        public static void DrawLogo(Graphics g, float x, float y, float s) { AppIcon.DrawLogo(g, x, y, s); }

        // ---------------------------------------------------------------- janela
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int round = 2; DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { }
            try { int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, 4); } catch { }
        }

        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.Style |= 0x20000; /* WS_MINIMIZEBOX */ cp.ClassStyle |= 0x20000; /* sombra */ return cp; }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = _rMin.Contains(e.Location) ? 1 : _rClose.Contains(e.Location) ? 2 : 0;
            if (h != _hoverTitleBtn) { _hoverTitleBtn = h; Invalidate(new Rectangle(Width - 110, 0, 110, 64)); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hoverTitleBtn = 0; Invalidate(new Rectangle(Width - 110, 0, 110, 64)); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (_rClose.Contains(e.Location)) { HideToTray(); return; }
            if (_rMin.Contains(e.Location)) { WindowState = FormWindowState.Minimized; return; }
            if (e.Y < 64) { ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); }
            base.OnMouseDown(e);
        }

        // ---------------------------------------------------------------- bandeja
        private ToolStripMenuItem _trayPause;

        private void BuildTray()
        {
            _tray.Icon = AppIcon.Create(32);
            _tray.Text = "RiseModer Cooller";
            _tray.Visible = true;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Abrir RiseModer Cooller", null, delegate { ShowFromTray(); });
            _trayPause = new ToolStripMenuItem("Pausar", null, delegate { _active = !_active; UpdateSendButton(); });
            menu.Items.Add(_trayPause);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Sair", null, delegate { _reallyClose = true; Close(); });
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { ShowFromTray(); };
        }

        private void HideToTray()
        {
            Hide();
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _tray.ShowBalloonTip(2500, "RiseModer Cooller", "Continuo rodando aqui na bandeja. Clique com o botão direito para sair.", ToolTipIcon.Info);
            }
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (_startHidden && !IsHandleCreated) { CreateHandle(); value = false; }
            base.SetVisibleCore(value);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_reallyClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; HideToTray(); return; }
            _tick.Stop();
            _s.Save();
            _tray.Visible = false;
            _tray.Dispose();
            _device.Dispose();
            _engine.Dispose();
            base.OnFormClosing(e);
        }
    }
}
