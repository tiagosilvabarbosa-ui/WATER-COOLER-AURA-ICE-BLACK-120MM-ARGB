# Compila o RiseModer Cooller e o instalador usando o compilador C# que já vem no Windows (.NET Framework 4.x).
# Uso:  powershell -ExecutionPolicy Bypass -File build.ps1
# Saída:
#   bin\RiseModerCooller.exe        (app + pasta bin\lib com as DLLs de sensores)
#   dist\RiseModerCooller-Setup.exe (instalador único, com tudo embutido)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'csc.exe (.NET Framework 4) não encontrado.' }

$icon = Join-Path $root 'assets\icon.ico'
if (-not (Test-Path $icon)) { & (Join-Path $root 'assets\make-icon.ps1') }
$src = Join-Path $root 'src'
$common = '/nologo', '/codepage:65001', '/target:winexe', '/optimize+', "/win32icon:$icon",
          "/win32manifest:$(Join-Path $src 'app.manifest')", '/r:System.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll'

# ---- app
$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null
$appSources = Get-ChildItem $src -Filter *.cs | ForEach-Object { $_.FullName }
& $csc @common /platform:x64 "/out:$bin\RiseModerCooller.exe" $appSources
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o app ($LASTEXITCODE)" }

# bibliotecas de sensores (LibreHardwareMonitor) vão em bin\lib
$lib = Join-Path $root 'lib'
$libFiles = @()
if (Test-Path $lib) {
    New-Item -ItemType Directory -Force (Join-Path $bin 'lib') | Out-Null
    $libFiles = Get-ChildItem $lib -Filter *.dll
    $libFiles | Copy-Item -Destination (Join-Path $bin 'lib') -Force
} else {
    Write-Warning 'Pasta lib\ não encontrada: o modo Temperatura ficará sem sensores.'
}
Write-Host "OK -> $bin\RiseModerCooller.exe" -ForegroundColor Green

# ---- instalador (app + lib embutidos como recursos "payload~caminho")
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$shared = 'Theme.cs', 'Controls.cs', 'AppIcon.cs', 'RiseDetector.cs', 'Settings.cs', 'DisplayText.cs' | ForEach-Object { Join-Path $src $_ }
$setupSources = @(Join-Path $root 'setup\Setup.cs') + $shared
$resources = @("/resource:$bin\RiseModerCooller.exe,payload~RiseModerCooller.exe")
foreach ($f in $libFiles) { $resources += "/resource:$($f.FullName),payload~lib/$($f.Name)" }
foreach ($f in Get-ChildItem $root -Filter 'THIRD-PARTY-NOTICES.md') { $resources += "/resource:$($f.FullName),payload~$($f.Name)" }
& $csc @common /platform:anycpu "/out:$dist\RiseModerCooller-Setup.exe" $resources $setupSources
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o instalador ($LASTEXITCODE)" }
Write-Host "OK -> $dist\RiseModerCooller-Setup.exe" -ForegroundColor Green
