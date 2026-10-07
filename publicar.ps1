# Genera la versión lista para instalar en la laptop de la ferretería.
# No requiere instalar .NET en esa laptop (el ejecutable lo incluye).
#
#   .\publicar.ps1                  → carpeta .\publicar
#   .\publicar.ps1 -Salida D:\USB   → directo a una memoria USB
#
# En la laptop: copie la carpeta completa (por ejemplo a C:\Facturacion) y cree un
# acceso directo a Facturacion.exe en el escritorio. Los datos se guardan en
# %LOCALAPPDATA%\FacturacionFerreteria (no dentro de la carpeta del programa).

param(
    [string]$Salida = (Join-Path $PSScriptRoot "publicar")
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "1/2 Ejecutando pruebas..." -ForegroundColor Cyan
dotnet test tests/Facturacion.Tests
if ($LASTEXITCODE -ne 0) { throw "Las pruebas fallaron: no se publica." }

Write-Host "2/2 Publicando en $Salida ..." -ForegroundColor Cyan
dotnet publish src/Facturacion.Escritorio `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $Salida
if ($LASTEXITCODE -ne 0) { throw "La publicación falló." }

Write-Host "Listo: $Salida\Facturacion.exe" -ForegroundColor Green
