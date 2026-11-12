using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

namespace RiseModerCooller
{
    /// <summary>
    /// Detecta o software original do fabricante do cooler, que abre o display em modo exclusivo
    /// e impede o RiseModer Cooller de funcionar. Usado pelo instalador e pelo app.
    /// </summary>
    public static class RiseDetector
    {
        public sealed class Found
        {
            public string DisplayName;
            public string UninstallString;
            public bool ServiceOnly; // só restou o serviço, sem desinstalador
        }

        private const string ServiceName = "tifSer";

        public static Found Detect()
        {
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (RegistryKey hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey root = hk.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (root == null) continue;
                        foreach (string sub in root.GetSubKeyNames())
                        {
                            using (RegistryKey k = root.OpenSubKey(sub))
                            {
                                if (k == null) continue;
                                string name = k.GetValue("DisplayName") as string;
                                if (string.IsNullOrEmpty(name)) continue;
                                if (name.IndexOf("Rise Mode", StringComparison.OrdinalIgnoreCase) < 0) continue;
                                if (name.IndexOf("RiseModer", StringComparison.OrdinalIgnoreCase) >= 0) continue; // somos nós
                                Found f = new Found();
                                f.DisplayName = name;
                                f.UninstallString = k.GetValue("UninstallString") as string;
                                return f;
                            }
                        }
                    }
                }
                catch { }
            }
            if (ServiceExists())
            {
                Found f = new Found();
                f.DisplayName = "Serviço do software Rise Mode (" + ServiceName + ")";
                f.ServiceOnly = true;
                return f;
            }
            return null;
        }

        private static bool ServiceExists()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + ServiceName))
                    return k != null;
            }
            catch { return false; }
        }

        /// <summary>Executa o desinstalador do software Rise Mode e espera terminar.</summary>
        public static bool Uninstall(Found f)
        {
            // fecha os processos que seguram o cooler, para o desinstalador não reclamar
            Run("sc.exe", "stop " + ServiceName);
            foreach (string n in new[] { "SendTemp", "CPU Server", "Rise Mode Temp CPU Driver R2.2" })
                foreach (Process p in Process.GetProcessesByName(n))
                    try { p.Kill(); p.WaitForExit(3000); } catch { }

            if (f.ServiceOnly || string.IsNullOrEmpty(f.UninstallString))
            {
                Run("sc.exe", "delete " + ServiceName);
                return !ServiceExists();
            }

            string file, args;
            SplitCommand(f.UninstallString, out file, out args);
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = true;
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                }
                // o desinstalador do Inno Setup se copia para %TEMP% e continua rodando
                for (int i = 0; i < 600 && Detect() != null; i++)
                {
                    bool running = false;
                    foreach (Process p in Process.GetProcesses())
                    {
                        try { if (p.ProcessName.StartsWith("_iu", StringComparison.OrdinalIgnoreCase) || p.ProcessName.StartsWith("unins", StringComparison.OrdinalIgnoreCase)) running = true; }
                        catch { }
                    }
                    if (!running) break;
                    System.Threading.Thread.Sleep(500);
                }
            }
            catch { return false; }
            return Detect() == null;
        }

        private static void SplitCommand(string cmd, out string file, out string args)
        {
            cmd = cmd.Trim();
            if (cmd.StartsWith("\""))
            {
                int end = cmd.IndexOf('"', 1);
                file = cmd.Substring(1, end - 1);
                args = cmd.Substring(end + 1).Trim();
            }
            else
            {
                int exe = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (exe > 0) { file = cmd.Substring(0, exe + 4); args = cmd.Substring(exe + 4).Trim(); }
                else { file = cmd; args = ""; }
            }
        }

        public static int Run(string file, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                using (Process p = Process.Start(psi)) { p.WaitForExit(15000); return p.ExitCode; }
            }
            catch { return -1; }
        }
    }
}
