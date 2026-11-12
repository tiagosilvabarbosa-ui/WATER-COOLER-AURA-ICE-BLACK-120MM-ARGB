using System;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("RiseModer Cooller")]
[assembly: System.Reflection.AssemblyProduct("RiseModer Cooller")]
[assembly: System.Reflection.AssemblyDescription("Controle do display do cooler Rise Mode Aura Ice: mostre seu nome no lugar da temperatura")]
[assembly: System.Reflection.AssemblyVersion("1.5.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.5.0.0")]

namespace RiseModerCooller
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool created;
#if SCREENSHOT
            const string mutexName = "RiseModerCooller.Screenshot";   // build só para gerar o print do README
#else
            const string mutexName = "RiseModerCooller.SingleInstance";
#endif
            using (Mutex m = new Mutex(true, mutexName, out created))
            {
                if (!created)
                {
                    MessageBox.Show("O RiseModer Cooller já está aberto (veja na bandeja do Windows).", "RiseModer Cooller",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                bool tray = Array.IndexOf(args, "--tray") >= 0;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
#if SCREENSHOT
                Application.Run(new MainForm(new AppSettings(), false)); // configurações padrão, sem salvar nada do usuário
#else
                Application.Run(new MainForm(AppSettings.Load(), tray));
#endif
            }
        }
    }
}
