#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

echo "=========================================================="
echo " HWID DIAGNOSTIC PRO - BUILD, TEST & PUBLISH (WIN-X64)   "
echo "=========================================================="

echo "[1/6] Restaurando dependências..."
dotnet restore HWIDDiagnosticPro.sln

echo "[2/6] Compilando solução..."
dotnet build HWIDDiagnosticPro.sln -c Debug --no-restore

echo "[3/6] Executando testes automatizados (UnitTests e IntegrationTests)..."
dotnet test HWIDDiagnosticPro.sln -c Debug --no-build --verbosity normal

echo "[4/6] Gerando versão Release..."
dotnet build HWIDDiagnosticPro.sln -c Release

PUBLISH_DIR="$ROOT_DIR/build/win-x64-self-contained"
PORTABLE_DIR="$ROOT_DIR/build/HWIDDiagnosticPro-Portable-win-x64"

rm -rf "$PUBLISH_DIR" "$PORTABLE_DIR"
mkdir -p "$PUBLISH_DIR" "$PORTABLE_DIR"

echo "[5/6] Publicando executável autossuficiente para Windows x64..."
dotnet publish src/HWIDDiagnosticPro.App/HWIDDiagnosticPro.App.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -o "$PUBLISH_DIR"

echo "[6/6] Preparando distribuição portátil..."
cp -r "$PUBLISH_DIR/"* "$PORTABLE_DIR/"
[ -f "$ROOT_DIR/README.md" ] && cp "$ROOT_DIR/README.md" "$PORTABLE_DIR/"
[ -f "$ROOT_DIR/LICENSE" ] && cp "$ROOT_DIR/LICENSE" "$PORTABLE_DIR/"

echo "Concluído! Executável Windows x64 autossuficiente disponível em: $PUBLISH_DIR/HWIDDiagnosticPro.exe"
