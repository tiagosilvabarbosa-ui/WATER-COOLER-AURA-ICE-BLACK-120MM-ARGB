# Publica o projeto no GitHub (repositório público) e cria a release com o instalador.
# Uso:  powershell -ExecutionPolicy Bypass -File publish.ps1
$ErrorActionPreference = 'Stop'
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
Set-Location $PSScriptRoot

$repo = 'WATER-COOLER-AURA-ICE-BLACK-120MM-ARGB'
$version = 'v1.5.0'
$setup = Join-Path $PSScriptRoot 'dist\RiseModerCooller-Setup.exe'
if (-not (Test-Path $setup)) { throw 'dist\RiseModerCooller-Setup.exe não encontrado. Rode build.ps1 antes.' }

$hasRemote = (git remote) -contains 'origin'
if (-not $hasRemote) {
    Write-Host "Criando repositório público $repo..." -ForegroundColor Cyan
    gh repo create $repo --public --source . --remote origin --push `
        --description 'RiseModer Cooller: mostre seu nome, a hora ou a temperatura no display do water cooler Rise Mode Aura Ice Black 120mm ARGB'
} else {
    git push -u origin main
}
if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar/enviar o repositório.' }

$notes = @'
**RiseModer Cooller 1.5** — software independente para o display do water cooler Rise Mode Aura Ice ARGB.

- Modos: Nome (rolagem, vai-e-volta, de 2 em 2, letra a letra), Temperatura (CPU/GPU), Relógio e Nome + Temp
- Instalador próprio que detecta e oferece desinstalar o software original (que trava o display)
- Envio contínuo para o display trocar o número na hora (o firmware tira média dos pacotes)

**Instalação:** baixe `RiseModerCooller-Setup.exe` abaixo e execute como administrador.
O modo Temperatura precisa do driver [PawnIO](https://pawnio.eu).
'@
Write-Host "Criando release $version..." -ForegroundColor Cyan
gh release create $version $setup --title "RiseModer Cooller 1.5" --notes $notes
if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar a release.' }

$url = gh repo view $repo --json url --jq .url
Write-Host "Pronto: $url" -ForegroundColor Green
