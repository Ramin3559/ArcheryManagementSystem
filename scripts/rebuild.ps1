# Rebuild helper — kilidlənmiş DLL xətalarının qarşısını alır.
$ErrorActionPreference = "SilentlyContinue"

Write-Host "Dayandirilir: EShooting.Web..." -ForegroundColor Yellow
Get-Process -Name "EShooting.Web" | Stop-Process -Force

# IIS publish eyni qovluğa gedirsə, App Pool-u da dayandırın:
# iisreset /stop   (admin lazımdır)

Write-Host "Build edilir..." -ForegroundColor Cyan
$root = Split-Path -Parent $PSScriptRoot
dotnet build "$root\EShooting.sln" -nologo
exit $LASTEXITCODE
