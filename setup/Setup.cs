using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("RiseModer Cooller - Instalador")]
[assembly: AssemblyProduct("RiseModer Cooller")]
[assembly: AssemblyVersion("1.5.0.0")]
[assembly: AssemblyFileVersion("1.5.0.0")]

namespace RiseModerCooller.Setup
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool uninstall = Array.IndexOf(args, "--uninstall") >= 0;
            Application.Run(new SetupForm(uninstall));
        }
    }

    internal static class Installer
    {
        public const string AppName = "RiseModer Cooller";
        public const string Version = "1.5.0";
        public const string ExeName = "RiseModerCooller.exe";
        private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RiseModerCooller";
        private const string PayloadPrefix = "payload~";

        public static string InstallDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RiseModerCooller"); }
        }

        public static string ExePath { get { return Path.Combine(InstallDir, ExeName); } }

        private static string StartMenuShortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), AppName + ".lnk"); }
        }

        private static string DesktopShortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), AppName + ".lnk"); }
        }

        public static bool PawnIOInstalled()
        {
            return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"PawnIO\PawnIOLib.dll"))
                || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\PawnIO.sys"));
        }

        public static void KillApp()
        {
            foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName)))
                try { p.Kill(); p.WaitForExit(5000); } catch { }
        }

        public static void Install(bool desktop, bool autostart, Action<string> progress)
        {
            progress("Fechando o RiseModer Cooller...");
            KillApp();

            progress("Copiando arquivos...");
            Directory.CreateDirectory(InstallDir);
            Assembly asm = Assembly.GetExecutingAssembly();
            long total = 0;
            foreach (string res in asm.GetManifestResourceNames())
            {
                if (!res.StartsWith(PayloadPrefix)) continue;
                string rel = res.Substring(PayloadPrefix.Length).Replace('/', '\\');
                string dest = Path.Combine(InstallDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                using (Stream s = asm.GetManifestResourceStream(res))
                using (FileStream f = File.Create(dest))
                {
                    s.CopyTo(f);
                    total += f.Length;
                }
            }

            // o próprio instalador vira o desinstalador
            string self = Process.GetCurrentProcess().MainModule.FileName;
            string uninst = Path.Combine(InstallDir, "Uninstall.exe");
            if (!string.Equals(self, uninst, StringComparison.OrdinalIgnoreCase)) File.Copy(self, uninst, true);
            total += new FileInfo(uninst).Length;

            progress("Criando atalhos...");
            CreateShortcut(StartMenuShortcut, ExePath);
            if (desktop) CreateShortcut(DesktopShortcut, ExePath);
            else TryDelete(DesktopShortcut);

            progress("Registrando no Windows...");
            using (RegistryKey hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey k = hk.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", AppName);
                k.SetValue("DisplayVersion", Version);
                k.SetValue("Publisher", "RiseModer");
                k.SetValue("DisplayIcon", ExePath);
                k.SetValue("InstallLocation", InstallDir);
                k.SetValue("UninstallString", "\"" + uninst + "\" --uninstall");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(total / 1024), RegistryValueKind.DWord);
            }

            progress("Configurando inicialização...");
            SystemTools.SetAutoStart(autostart, ExePath);
        }

        public static void Uninstall(Action<string> progress)
        {
            progress("Fechando o RiseModer Cooller...");
            KillApp();
            progress("Removendo atalhos e registro...");
            SystemTools.SetAutoStart(false, ExePath);
            TryDelete(StartMenuShortcut);
            TryDelete(DesktopShortcut);
            try
            {
                using (RegistryKey hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    hk.DeleteSubKeyTree(UninstallKey, false);
            }
            catch { }

            progress("Removendo arquivos...");
            string self = Process.GetCurrentProcess().MainModule.FileName;
            foreach (string f in Directory.Exists(InstallDir) ? Directory.GetFiles(InstallDir, "*", SearchOption.AllDirectories) : new string[0])
                if (!string.Equals(f, self, StringComparison.OrdinalIgnoreCase)) TryDelete(f);

            // apaga a pasta (e o próprio Uninstall.exe) depois que este processo fechar
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
                "/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"" + InstallDir + "\"");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            try { Process.Start(psi); } catch { }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void CreateShortcut(string lnk, string target)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            try
            {
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type st = sc.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(target) });
                st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "Mostre seu nome no display do cooler" });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
                Marshal.FinalReleaseComObject(sc);
            }
            finally { Marshal.FinalReleaseComObject(shell); }
        }
    }

    internal class SetupForm : Form
    {
        private readonly bool _uninstall;
        private RiseDetector.Found _rise;
        private Card _riseCard;
        private NeonButton _btnRiseUninstall, _btnMain;
        private NeonToggle _tDesktop, _tAuto, _tOpen;
        private Label _status, _riseText;
        private Rectangle _rClose;
        private bool _hoverClose, _busy, _done;

        public SetupForm(bool uninstall)
        {
            _uninstall = uninstall;
            Text = Installer.AppName + (uninstall ? " - Desinstalar" : " - Instalador");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            Font = Theme.F(10f);
            Icon = AppIcon.Create(64);
            ClientSize = new Size(640, uninstall ? 300 : 520);
            BuildUi();
        }

        private void BuildUi()
        {
            int y = 84;
            if (!_uninstall)
            {
                _riseCard = new Card();
                _riseCard.Title = "Software Rise Mode detectado";
                _riseCard.Accent = Theme.Orange;
                _riseCard.SetBounds(24, y, 592, 132);
                _riseText = new Label();
                _riseText.BackColor = Theme.Card;
                _riseText.ForeColor = Theme.Text;
                _riseText.Font = Theme.F(9.5f);
                _riseText.SetBounds(18, 38, 360, 82);
                _riseCard.Controls.Add(_riseText);
                _btnRiseUninstall = new NeonButton();
                _btnRiseUninstall.Accent = Theme.Orange;
                _btnRiseUninstall.Accent2 = Theme.Red;
                _btnRiseUninstall.Text = "DESINSTALAR";
                _btnRiseUninstall.Font = Theme.F(10f, FontStyle.Bold);
                _btnRiseUninstall.SetBounds(392, 50, 184, 56);
                _btnRiseUninstall.Click += delegate { UninstallRise(); };
                _riseCard.Controls.Add(_btnRiseUninstall);
                Controls.Add(_riseCard);
                y += 144;

                Card opts = new Card();
                opts.Title = "Instalação";
                opts.Badge = "v" + Installer.Version;
                opts.SetBounds(24, y, 592, 176);
                Label dest = new Label();
                dest.BackColor = Theme.Card;
                dest.ForeColor = Theme.TextDim;
                dest.Font = Theme.F(9f);
                dest.Text = "Pasta: " + Installer.InstallDir;
                dest.SetBounds(18, 36, 556, 22);
                opts.Controls.Add(dest);
                _tDesktop = Toggle(opts, "Criar atalho na Área de Trabalho", 62);
                _tAuto = Toggle(opts, "Iniciar junto com o Windows", 96);
                _tOpen = Toggle(opts, "Abrir o RiseModer Cooller ao concluir", 130);
                Controls.Add(opts);
                y += 188;

                _riseCard.Tag = opts; // usado para reposicionar quando não houver conflito
            }

            _status = new Label();
            _status.BackColor = Theme.Bg;
            _status.ForeColor = Theme.TextDim;
            _status.Font = Theme.F(9.5f);
            _status.SetBounds(26, ClientSize.Height - 80, 380, 60);
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            Controls.Add(_status);

            _btnMain = new NeonButton();
            _btnMain.BackColor = Theme.Bg;
            _btnMain.Font = Theme.F(12f, FontStyle.Bold);
            _btnMain.Text = _uninstall ? "DESINSTALAR" : "INSTALAR";
            _btnMain.SetBounds(416, ClientSize.Height - 84, 204, 68);
            _btnMain.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            _btnMain.Click += delegate { MainAction(); };
            Controls.Add(_btnMain);

            if (_uninstall)
            {
                Label l = new Label();
                l.BackColor = Theme.Bg;
                l.ForeColor = Theme.Text;
                l.Font = Theme.F(11f);
                l.Text = "Remover o RiseModer Cooller deste computador?\nSuas configurações em %AppData% serão mantidas.";
                l.SetBounds(26, 96, 590, 60);
                Controls.Add(l);
            }
            else
            {
                if (!Installer.PawnIOInstalled())
                    _status.Text = "Dica: o modo Temperatura precisa do driver PawnIO (pawnio.eu).";
                RefreshRise();
            }
        }

        private NeonToggle Toggle(Card c, string text, int y)
        {
            NeonToggle t = new NeonToggle();
            t.Text = text;
            t.SetBounds(16, y, 556, 30);
            t.SetSilently(true);
            c.Controls.Add(t);
            return t;
        }

        private void RefreshRise()
        {
            _rise = RiseDetector.Detect();
            Card opts = (Card)_riseCard.Tag;
            ClientSize = new Size(640, _rise == null ? 376 : 520);
            if (_rise == null)
            {
                _riseCard.Visible = false;
                opts.Top = 84;
            }
            else
            {
                _riseCard.Visible = true;
                opts.Top = 84 + 144;
                _riseText.Text = "\"" + _rise.DisplayName + "\" está instalado. Ele trava o display do cooler e " +
                                 "impede o RiseModer Cooller de funcionar. Recomendado desinstalar antes de continuar.";
            }
        }

        private void UninstallRise()
        {
            if (_busy || _rise == null) return;
            SetBusy(true, "Desinstalando o software Rise Mode... siga as telas do desinstalador.");
            RiseDetector.Found f = _rise;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = RiseDetector.Uninstall(f);
                BeginInvoke((MethodInvoker)delegate
                {
                    SetBusy(false, ok ? "Software Rise Mode removido. Pode instalar." : "A desinstalação não foi concluída.");
                    RefreshRise();
                });
            });
        }

        private void MainAction()
        {
            if (_busy) return;
            if (_done) { Close(); return; }
            if (_uninstall)
            {
                SetBusy(true, "Desinstalando...");
                ThreadPool.QueueUserWorkItem(delegate
                {
                    string err = null;
                    try { Installer.Uninstall(Progress); } catch (Exception ex) { err = ex.Message; }
                    BeginInvoke((MethodInvoker)delegate { Finish(err == null ? "RiseModer Cooller removido." : "Erro: " + err); });
                });
                return;
            }

            if (_rise != null && MessageBox.Show(this,
                    "O software Rise Mode ainda está instalado e vai impedir o cooler de funcionar.\n\nInstalar mesmo assim?",
                    Installer.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            bool desktop = _tDesktop.Checked, auto = _tAuto.Checked, open = _tOpen.Checked;
            SetBusy(true, "Instalando...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string err = null;
                try { Installer.Install(desktop, auto, Progress); } catch (Exception ex) { err = ex.Message; }
                BeginInvoke((MethodInvoker)delegate
                {
                    if (err == null && open)
                    {
                        try { Process.Start(new ProcessStartInfo(Installer.ExePath) { WorkingDirectory = Installer.InstallDir }); } catch { }
                    }
                    Finish(err == null ? "Instalado com sucesso!" : "Erro: " + err);
                });
            });
        }

        private void Progress(string msg)
        {
            try { BeginInvoke((MethodInvoker)delegate { _status.Text = msg; }); } catch { }
        }

        private void SetBusy(bool busy, string msg)
        {
            _busy = busy;
            _status.Text = msg;
            _status.ForeColor = busy ? Theme.Cyan : Theme.TextDim;
            _btnMain.Enabled = !busy;
            if (_btnRiseUninstall != null) _btnRiseUninstall.Enabled = !busy;
            _btnMain.Invalidate();
        }

        private void Finish(string msg)
        {
            _busy = false;
            _done = true;
            _status.Text = msg;
            _status.ForeColor = msg.StartsWith("Erro") ? Theme.Red : Theme.Green;
            _btnMain.Enabled = true;
            _btnMain.Text = "CONCLUIR";
            _btnMain.Icon = "✓";
            _btnMain.Accent = Theme.Green;
            _btnMain.Accent2 = Theme.Cyan;
            _btnMain.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.HighQuality(g);
            using (GraphicsPath gp = new GraphicsPath())
            {
                RectangleF glow = new RectangleF(-200, -260, 700, 560);
                gp.AddEllipse(glow);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterColor = Theme.Alpha(Theme.Red, 40);
                    pb.SurroundColors = new[] { Theme.Alpha(Theme.Red, 0) };
                    g.FillEllipse(pb, glow);
                }
            }
            using (Pen p = new Pen(Color.FromArgb(9, 255, 255, 255)))
                for (int x = -Height; x < Width; x += 22) g.DrawLine(p, x, Height, x + Height, 0);
            using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 63, Width, 2), Theme.Red, Theme.Alpha(Theme.Orange, 0), LinearGradientMode.Horizontal))
                g.FillRectangle(b, 0, 63, Width, 2);

            AppIcon.DrawLogo(g, 22, 14, 36);
            using (Font f1 = Theme.F(16f, FontStyle.Bold))
            using (Font f2 = Theme.F(8f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, "RISEMODER", f1, new Point(66, 10), Theme.Text);
                Size s1 = TextRenderer.MeasureText("RISEMODER", f1);
                TextRenderer.DrawText(g, "COOLLER", f1, new Point(66 + s1.Width - 10, 10), Theme.Red);
                TextRenderer.DrawText(g, _uninstall ? "DESINSTALADOR" : "INSTALADOR  •  v" + Installer.Version, f2, new Point(69, 38), Theme.TextMuted);
            }
            _rClose = new Rectangle(Width - 54, 16, 36, 32);
            if (_hoverClose) using (SolidBrush b = new SolidBrush(Theme.Red)) g.FillRectangle(b, _rClose);
            using (Pen p = new Pen(Theme.Text, 1.5f))
            {
                g.DrawLine(p, _rClose.X + 12, _rClose.Y + 10, _rClose.Right - 12, _rClose.Bottom - 10);
                g.DrawLine(p, _rClose.Right - 12, _rClose.Y + 10, _rClose.X + 12, _rClose.Bottom - 10);
            }
        }

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int round = 2; DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { }
        }

        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool h = _rClose.Contains(e.Location);
            if (h != _hoverClose) { _hoverClose = h; Invalidate(_rClose); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (_rClose.Contains(e.Location)) { if (!_busy) Close(); return; }
            if (e.Y < 64) { ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); }
            base.OnMouseDown(e);
        }
    }
}
