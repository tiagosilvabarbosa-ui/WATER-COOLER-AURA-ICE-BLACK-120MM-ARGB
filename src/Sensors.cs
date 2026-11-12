using System;
using System.Collections;
using System.IO;
using System.Reflection;

namespace RiseModerCooller
{
    /// <summary>
    /// Lê a temperatura da CPU/GPU usando a LibreHardwareMonitorLib.dll (carregada dinamicamente).
    /// A DLL vem junto com o app, na pasta "lib". Precisa rodar como administrador
    /// e do driver PawnIO instalado (https://pawnio.eu).
    /// </summary>
    public sealed class Sensors : IDisposable
    {
        private object _computer;
        private Type _sensorTypeEnum;
        public string Status { get; private set; }
        public bool Available { get { return _computer != null; } }
        public float? CpuTemp { get; private set; }
        public float? GpuTemp { get; private set; }

        private static readonly string[] SearchDirs =
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib")
        };

        public void Open()
        {
            try
            {
                string dll = null;
                foreach (string d in SearchDirs)
                {
                    string p = Path.Combine(d, "LibreHardwareMonitorLib.dll");
                    if (File.Exists(p)) { dll = p; break; }
                }
                if (dll == null) { Status = "LibreHardwareMonitorLib.dll não encontrada"; return; }

                string dir = Path.GetDirectoryName(dll);
                AppDomain.CurrentDomain.AssemblyResolve += delegate (object s, ResolveEventArgs e)
                {
                    string f = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
                    return File.Exists(f) ? Assembly.LoadFrom(f) : null;
                };
                Assembly asm = Assembly.LoadFrom(dll);
                Type computerType = asm.GetType("LibreHardwareMonitor.Hardware.Computer", true);
                _sensorTypeEnum = asm.GetType("LibreHardwareMonitor.Hardware.SensorType", true);
                object c = Activator.CreateInstance(computerType);
                computerType.GetProperty("IsCpuEnabled").SetValue(c, true, null);
                computerType.GetProperty("IsGpuEnabled").SetValue(c, true, null);
                computerType.GetMethod("Open", Type.EmptyTypes).Invoke(c, null);
                _computer = c;
                Status = "Sensores OK";
            }
            catch (Exception ex)
            {
                Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                Status = "Sensores indisponíveis: " + inner.Message;
                _computer = null;
            }
        }

        public void Update()
        {
            if (_computer == null) return;
            try
            {
                float? cpu = null, cpuFallback = null, gpu = null;
                IEnumerable hws = (IEnumerable)_computer.GetType().GetProperty("Hardware").GetValue(_computer, null);
                foreach (object hw in hws)
                {
                    Type ht = hw.GetType();
                    ht.GetMethod("Update").Invoke(hw, null);
                    string hwType = ht.GetProperty("HardwareType").GetValue(hw, null).ToString();
                    IEnumerable sensors = (IEnumerable)ht.GetProperty("Sensors").GetValue(hw, null);
                    foreach (object s in sensors)
                    {
                        Type st = s.GetType();
                        if (st.GetProperty("SensorType").GetValue(s, null).ToString() != "Temperature") continue;
                        object val = st.GetProperty("Value").GetValue(s, null);
                        if (val == null) continue;
                        float v = (float)val;
                        if (v <= 0 || v > 125) continue;
                        string name = (string)st.GetProperty("Name").GetValue(s, null);
                        if (hwType == "Cpu")
                        {
                            if (name.Contains("Package") || name.Contains("Tctl") || name.Contains("Tdie")) cpu = v;
                            else if (cpuFallback == null || v > cpuFallback) cpuFallback = v;
                        }
                        else if (hwType.StartsWith("Gpu") && (gpu == null || name.Contains("Core")))
                        {
                            if (gpu == null || name == "GPU Core") gpu = v;
                        }
                    }
                }
                CpuTemp = cpu ?? cpuFallback;
                GpuTemp = gpu;
            }
            catch (Exception ex)
            {
                Status = "Erro ao ler sensores: " + ex.Message;
            }
        }

        public void Dispose()
        {
            if (_computer == null) return;
            try { _computer.GetType().GetMethod("Close").Invoke(_computer, null); } catch { }
            _computer = null;
        }
    }
}
