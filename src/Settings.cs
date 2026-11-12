using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace RiseModerCooller
{
    public enum DisplayMode { Name = 0, Temperature = 1, Clock = 2, Mix = 3 }

    public sealed class AppSettings
    {
        public string Text = "RISEMODER";
        public DisplayMode Mode = DisplayMode.Name;
        public TextEffect Effect = TextEffect.Scroll;
        public int StepMs = 350;
        public bool UseGpu = false;
        public int MixTempSeconds = 5;
        public bool StartMinimized = false;

        private static string FilePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RiseModerCooller");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.ini");
            }
        }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                Dictionary<string, string> kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) kv[line.Substring(0, i).Trim()] = line.Substring(i + 1);
                }
                string v;
                if (kv.TryGetValue("Text", out v)) s.Text = v;
                if (kv.TryGetValue("Mode", out v)) s.Mode = (DisplayMode)Int(v, 0);
                if (kv.TryGetValue("Effect", out v)) s.Effect = (TextEffect)Int(v, 0);
                if (kv.TryGetValue("StepMs", out v)) s.StepMs = Math.Max(80, Math.Min(1500, Int(v, 350)));
                if (kv.TryGetValue("UseGpu", out v)) s.UseGpu = v == "1";
                if (kv.TryGetValue("MixTempSeconds", out v)) s.MixTempSeconds = Math.Max(2, Math.Min(30, Int(v, 5)));
                if (kv.TryGetValue("StartMinimized", out v)) s.StartMinimized = v == "1";
            }
            catch { }
            return s;
        }

        public void Save()
        {
#if SCREENSHOT
            return; // o build de print nunca sobrescreve as configurações do usuário
#endif
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Text=" + (Text ?? "").Replace("\r", "").Replace("\n", " "));
                sb.AppendLine("Mode=" + (int)Mode);
                sb.AppendLine("Effect=" + (int)Effect);
                sb.AppendLine("StepMs=" + StepMs);
                sb.AppendLine("UseGpu=" + (UseGpu ? "1" : "0"));
                sb.AppendLine("MixTempSeconds=" + MixTempSeconds);
                sb.AppendLine("StartMinimized=" + (StartMinimized ? "1" : "0"));
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static int Int(string s, int def)
        {
            int r; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) ? r : def;
        }
    }

    /// <summary>Inicialização com o Windows (Agendador de Tarefas, com privilégio elevado).</summary>
    public static class SystemTools
    {
        private const string TaskName = "RiseModerCooller";

        public static bool IsAutoStartEnabled()
        {
            return RunHidden("schtasks.exe", "/Query /TN \"" + TaskName + "\"") == 0;
        }

        public static bool SetAutoStart(bool enable)
        {
            return SetAutoStart(enable, Process.GetCurrentProcess().MainModule.FileName);
        }

        public static bool SetAutoStart(bool enable, string exe)
        {
            if (!enable) return RunHidden("schtasks.exe", "/Delete /F /TN \"" + TaskName + "\"") == 0;
            string args = "/Create /F /SC ONLOGON /RL HIGHEST /TN \"" + TaskName + "\" /TR \"\\\"" + exe + "\\\" --tray\"";
            return RunHidden("schtasks.exe", args) == 0;
        }

        private static int RunHidden(string file, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(15000);
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }
    }
}
