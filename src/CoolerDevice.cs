using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace RiseModerCooller
{
    /// <summary>
    /// Comunicação com o display do cooler Rise Mode Aura Ice (USB HID VID 0xAA88 / PID 0x8666).
    ///
    /// Protocolo:
    ///   Relatório de saída HID de 9 bytes, report ID 0x00, seguido de:
    ///     [1] temperatura CPU   [2] temperatura GPU   [3] uso CPU %
    ///     [4] uso memória %     [5] temperatura placa [6] uso GPU %
    ///     [7..8] = 0
    ///   O display mostra o valor em 2 dígitos (00-99) e tira a média dos últimos pacotes.
    /// </summary>
    public sealed class CoolerDevice : IDisposable
    {
        public const ushort VendorId = 0xAA88;
        public const ushort ProductId = 0x8666;

        private readonly object _lock = new object();
        private SafeFileHandle _handle;
        private FileStream _stream;
        private int _outLen = 11;
        private Thread _worker;
        private volatile bool _running;
        private byte[] _pending = new byte[6];
        private DateTime _lastSent = DateTime.MinValue;

        public event EventHandler ConnectionChanged;
        public bool IsConnected { get { return _stream != null; } }
        /// <summary>Quando pausado, nada é enviado (nem o keep-alive).</summary>
        public volatile bool Paused;
        /// <summary>Intervalo entre pacotes enviados continuamente.</summary>
        public const int SendIntervalMs = 4;
        public string LastError { get; private set; }
        public int OutputReportLength { get { return _outLen; } }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _worker = new Thread(WorkerLoop);
            _worker.IsBackground = true;
            _worker.Name = "CoolerDevice";
            _worker.Start();
        }

        /// <summary>Define o valor exibido (0..255). Todos os campos recebem o mesmo valor,
        /// assim o display mostra o número independentemente da fonte configurada.</summary>
        public void SetValue(byte value)
        {
            SetFields(new byte[] { value, value, value, value, value, value });
        }

        public void SetFields(byte[] six)
        {
            lock (_lock)
            {
                Array.Copy(six, _pending, 6);
            }
        }

        private void WorkerLoop()
        {
            while (_running)
            {
                try
                {
                    if (_stream == null)
                    {
                        if (TryOpen()) RaiseChanged();
                        else { Thread.Sleep(1500); continue; }
                    }

                    byte[] fields = null;
                    lock (_lock)
                    {
                        // O firmware tira a MÉDIA dos últimos pacotes (suaviza a temperatura).
                        // Para o número trocar na hora, enviamos o valor atual sem parar,
                        // assim a média fica "cheia" do valor novo quase instantaneamente.
                        if (!Paused)
                        {
                            fields = (byte[])_pending.Clone();
                        }
                    }
                    if (fields != null)
                    {
                        byte[] report = new byte[Math.Max(_outLen, 11)];
                        report[0] = 0x00;
                        Array.Copy(fields, 0, report, 1, 6);
                        _stream.Write(report, 0, _outLen);
                        _stream.Flush();
                        _lastSent = DateTime.UtcNow;
                        Thread.Sleep(SendIntervalMs);
                    }
                    else Thread.Sleep(30);
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    CloseInternal();
                    RaiseChanged();
                    Thread.Sleep(1000);
                }
            }
            CloseInternal();
        }

        private bool TryOpen()
        {
            foreach (string path in Native.FindHidPaths(VendorId, ProductId))
            {
                SafeFileHandle h = Native.CreateFile(path, Native.GENERIC_READ | Native.GENERIC_WRITE,
                    Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
                if (h.IsInvalid)
                {
                    int err = Marshal.GetLastWin32Error();
                    LastError = err == 32 || err == 5
                        ? "Cooler em uso por outro programa"
                        : "Falha ao abrir o cooler (erro " + err + ")";
                    continue;
                }
                IntPtr pre;
                int outLen = 11;
                if (Native.HidD_GetPreparsedData(h, out pre))
                {
                    Native.HIDP_CAPS caps;
                    if (Native.HidP_GetCaps(pre, out caps) == Native.HIDP_STATUS_SUCCESS && caps.OutputReportByteLength > 0)
                        outLen = caps.OutputReportByteLength;
                    Native.HidD_FreePreparsedData(pre);
                }
                _outLen = outLen;
                _handle = h;
                _stream = new FileStream(h, FileAccess.ReadWrite, 64, false);
                LastError = null;
                return true;
            }
            if (LastError == null) LastError = "Cooler não encontrado";
            return false;
        }

        private void CloseInternal()
        {
            try { if (_stream != null) _stream.Dispose(); } catch { }
            try { if (_handle != null) _handle.Dispose(); } catch { }
            _stream = null; _handle = null;
        }

        private void RaiseChanged()
        {
            EventHandler h = ConnectionChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            _running = false;
            if (_worker != null) _worker.Join(500);
            CloseInternal();
        }
    }

    internal static class Native
    {
        public const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3;
        public const int HIDP_STATUS_SUCCESS = 0x00110000;
        const int DIGCF_PRESENT = 0x02, DIGCF_DEVICEINTERFACE = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_CAPS
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
                NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
                NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

        [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("hid.dll")] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
        [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr data, out HIDP_CAPS caps);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr w, int flags);
        [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr d, ref Guid g, int idx, ref SP_DEVICE_INTERFACE_DATA data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA d, IntPtr detail, int size, out int required, IntPtr info);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint creation, uint flags, IntPtr template);

        public static List<string> FindHidPaths(ushort vid, ushort pid)
        {
            List<string> result = new List<string>();
            string needle = string.Format("vid_{0:x4}&pid_{1:x4}", vid, pid);
            Guid g; HidD_GetHidGuid(out g);
            IntPtr set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == IntPtr.Zero || set == new IntPtr(-1)) return result;
            try
            {
                for (int i = 0; i < 512; i++)
                {
                    SP_DEVICE_INTERFACE_DATA d = new SP_DEVICE_INTERFACE_DATA();
                    d.cbSize = Marshal.SizeOf(d);
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref d)) break;
                    int req;
                    SetupDiGetDeviceInterfaceDetail(set, ref d, IntPtr.Zero, 0, out req, IntPtr.Zero);
                    if (req <= 0) continue;
                    IntPtr buf = Marshal.AllocHGlobal(req);
                    try
                    {
                        Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                        if (SetupDiGetDeviceInterfaceDetail(set, ref d, buf, req, out req, IntPtr.Zero))
                        {
                            string path = Marshal.PtrToStringUni(new IntPtr(buf.ToInt64() + 4));
                            if (path != null && path.ToLowerInvariant().Contains(needle)) result.Add(path);
                        }
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return result;
        }
    }
}
