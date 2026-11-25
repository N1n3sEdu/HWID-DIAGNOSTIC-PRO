<#
.SYNOPSIS
    Script completo de compilação, testes, publicação autossuficiente (win-x64),
    geração de pacote portátil e instalador opcional do HWID Diagnostic Pro.
#>
param(
    [switch]$SkipTests = $false,
    [switch]$BuildInstaller = $false
)

$ErrorActionPreference = "Stop"
$RootDir = Split-Path -Parent $PSScriptRoot
Set-Location $RootDir

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " HWID DIAGNOSTIC PRO - BUILD & DISTRIBUIÇÃO (WINDOWS x64)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Restaurar dependências
Write-Host "[1/6] Restaurando dependências NuGet..." -ForegroundColor Yellow
dotnet restore "$RootDir\HWIDDiagnosticPro.sln"

# 2. Compilar o projeto
Write-Host "[2/6] Compilando solução..." -ForegroundColor Yellow
dotnet build "$RootDir\HWIDDiagnosticPro.sln" -c Debug --no-restore

# 3. Executar testes automatizados
if (-not $SkipTests) {
    Write-Host "[3/6] Executando testes unitários e de integração..." -ForegroundColor Yellow
    dotnet test "$RootDir\HWIDDiagnosticPro.sln" -c Debug --no-build --verbosity normal
}

# 4. Gerar a versão Release
Write-Host "[4/6] Compilando versão Release..." -ForegroundColor Yellow
dotnet build "$RootDir\HWIDDiagnosticPro.sln" -c Release

# 5. Publicar executável autossuficiente para Windows x64
$PublishSingleDir = "$RootDir\build\win-x64-self-contained"
$PortableDir = "$RootDir\build\HWIDDiagnosticPro-Portable-win-x64"
$PortableZip = "$RootDir\build\HWIDDiagnosticPro-Portable-win-x64.zip"

if (Test-Path $PublishSingleDir) { Remove-Item $PublishSingleDir -Recurse -Force }
if (Test-Path $PortableDir) { Remove-Item $PortableDir -Recurse -Force }
if (Test-Path $PortableZip) { Remove-Item $PortableZip -Force }

Write-Host "[5/6] Publicando executável autossuficiente (Windows x64)..." -ForegroundColor Yellow
dotnet publish "$RootDir\src\HWIDDiagnosticPro.App\HWIDDiagnosticPro.App.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $PublishSingleDir

# 6. Criar versão portátil (.zip)
Write-Host "[6/6] Gerando pacote portátil..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $PortableDir -Force | Out-Null
Copy-Item "$PublishSingleDir\*" -Destination $PortableDir -Recurse -Force
Copy-Item "$RootDir\README.md" -Destination $PortableDir -Force
Copy-Item "$RootDir\LICENSE" -Destination $PortableDir -Force
Compress-Archive -Path "$PortableDir\*" -DestinationPath $PortableZip -Force

# 7. Gerar instalador opcional via Inno Setup (ISCC.exe), se disponível e solicitado
if ($BuildInstaller) {
    $IsccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (Test-Path $IsccPath) {
        Write-Host "[Opcional] Compilando instalador Windows com Inno Setup..." -ForegroundColor Yellow
        & $IsccPath "$PSScriptRoot\installer.iss"
    } else {
        Write-Host "[Aviso] Inno Setup 6 não encontrado em '$IsccPath'. Use scripts\installer.iss para gerar o instalador." -ForegroundColor DarkYellow
    }
}

Write-Host "Build e publicação concluídos com sucesso em: $RootDir\build" -ForegroundColor Green
