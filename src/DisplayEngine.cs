using System;
using System.Collections.Generic;
using System.Threading;

namespace RiseModerCooller
{
    /// <summary>Decide, a cada instante, qual quadro de 2 caracteres o display deve mostrar.</summary>
    public sealed class DisplayEngine : IDisposable
    {
        private readonly AppSettings _s;
        private readonly Sensors _sensors = new Sensors();
        private Thread _sensorThread;
        private volatile bool _alive = true;

        private List<string> _frames = new List<string> { "00" };
        private int _index;
        private DateTime _stepAt = DateTime.UtcNow;
        private DateTime _phaseStart = DateTime.UtcNow;
        private bool _mixShowingTemp;
        private int _mixCycles;

        public string Frame { get; private set; }
        public string Caption { get; private set; }
        public Sensors Sensors { get { return _sensors; } }

        public DisplayEngine(AppSettings s)
        {
            _s = s;
            Frame = "00";
            Caption = "Temp/°C";
            Rebuild();
            _sensorThread = new Thread(SensorLoop);
            _sensorThread.IsBackground = true;
            _sensorThread.Start();
        }

        private void SensorLoop()
        {
            _sensors.Open();
            while (_alive)
            {
                _sensors.Update();
                for (int i = 0; i < 10 && _alive; i++) Thread.Sleep(100);
            }
            _sensors.Dispose();
        }

        /// <summary>Chamar quando texto/efeito/modo mudar.</summary>
        public void Rebuild()
        {
            _frames = DisplayText.BuildFrames(_s.Text, _s.Effect);
            _index = 0;
            _stepAt = DateTime.UtcNow;
            _phaseStart = DateTime.UtcNow;
            _mixShowingTemp = false;
            _mixCycles = 0;
        }

        public void Tick()
        {
            DateTime now = DateTime.UtcNow;
            switch (_s.Mode)
            {
                case DisplayMode.Name:
                    StepName(now);
                    Caption = _s.Text.Length > 12 ? _s.Text.Substring(0, 12) : _s.Text;
                    break;
                case DisplayMode.Temperature:
                    ShowTemp();
                    break;
                case DisplayMode.Clock:
                    ShowClock();
                    break;
                case DisplayMode.Mix:
                    if (_mixShowingTemp)
                    {
                        ShowTemp();
                        if ((now - _phaseStart).TotalSeconds >= _s.MixTempSeconds)
                        {
                            _mixShowingTemp = false; _index = 0; _stepAt = now; _mixCycles = 0;
                        }
                    }
                    else
                    {
                        if (StepName(now) && ++_mixCycles >= 1) { _mixShowingTemp = true; _phaseStart = now; }
                        Caption = _s.Text.Length > 12 ? _s.Text.Substring(0, 12) : _s.Text;
                    }
                    break;
            }
        }

        /// <summary>Avança a animação do nome. Retorna true quando completa um ciclo.</summary>
        private bool StepName(DateTime now)
        {
            bool wrapped = false;
            if ((now - _stepAt).TotalMilliseconds >= _s.StepMs)
            {
                _stepAt = now;
                _index++;
                if (_index >= _frames.Count) { _index = 0; wrapped = true; }
            }
            Frame = _frames[Math.Min(_index, _frames.Count - 1)];
            return wrapped;
        }

        private void ShowTemp()
        {
            float? t = _s.UseGpu ? (_sensors.GpuTemp ?? _sensors.CpuTemp) : _sensors.CpuTemp;
            Caption = _s.UseGpu ? "GPU °C" : "Temp/°C";
            if (t == null)
            {
                Frame = "00"; // sem sensor
                Caption = "sem sensor";
                return;
            }
            int v = Math.Max(0, Math.Min(99, (int)Math.Round(t.Value)));
            Frame = v.ToString("00");
        }

        private void ShowClock()
        {
            DateTime n = DateTime.Now;
            // o display não apaga, então alterna direto: hora (2 s) -> minutos (2 s)
            bool hour = ((int)(n.TimeOfDay.TotalMilliseconds / 2000) % 2) == 0;
            if (hour) { Frame = n.Hour.ToString("00"); Caption = "hora  " + n.ToString("HH:mm"); }
            else { Frame = n.Minute.ToString("00"); Caption = "min  " + n.ToString("HH:mm"); }
        }

        public void Dispose()
        {
            _alive = false;
            if (_sensorThread != null) _sensorThread.Join(1500);
        }
    }
}

