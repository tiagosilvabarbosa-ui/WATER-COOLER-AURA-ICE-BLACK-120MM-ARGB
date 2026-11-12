# Componentes de terceiros

O RiseModer Cooller distribui, sem modificações, as bibliotecas abaixo (pasta `lib\`),
usadas para ler a temperatura da CPU/GPU. Todas foram obtidas do [NuGet](https://www.nuget.org).

| Biblioteca | Versão | Licença | Projeto |
|------------|--------|---------|---------|
| LibreHardwareMonitorLib | 0.9.6 | MPL-2.0 | https://github.com/LibreHardwareMonitor/LibreHardwareMonitor |
| DiskInfoToolkit | 1.1.2 | MPL-2.0 | https://github.com/Blacktempel/DiskInfoToolkit |
| RAMSPDToolkit-NDD | 1.4.2 | MPL-2.0 | https://github.com/Blacktempel/RAMSPDToolkit |
| BlackSharp.Core | 1.0.7 | MPL-2.0 | https://github.com/Blacktempel/BlackSharp |
| HidSharp | 2.6.4 | Apache-2.0 | https://software.seekye.com/hidsharp |
| System.Buffers | 4.6.1 | MIT | https://github.com/dotnet/maintenance-packages |
| System.Memory | 4.6.3 | MIT | https://github.com/dotnet/maintenance-packages |
| System.Numerics.Vectors | 4.6.1 | MIT | https://github.com/dotnet/maintenance-packages |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | MIT | https://github.com/dotnet/maintenance-packages |
| System.CodeDom | 10.0.0 | MIT | https://github.com/dotnet/runtime |
| System.Security.AccessControl | 6.0.0 | MIT | https://github.com/dotnet/runtime |
| System.Security.Principal.Windows | 5.0.0 | MIT | https://github.com/dotnet/runtime |
| System.Threading.AccessControl | 10.0.3 | MIT | https://github.com/dotnet/runtime |

O código-fonte das bibliotecas MPL-2.0 está disponível nos links acima.
A leitura de sensores depende do driver [PawnIO](https://pawnio.eu), instalado separadamente.
