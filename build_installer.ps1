# build_installer.ps1
$projectDir = "NixPackTrace"
$publishDir = "$projectDir\bin\Release\net10.0-windows\win-x64\publish"
$isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$issFile = "$projectDir\nixpacktrace_setup.iss"

Write-Host "--- Starting Build Process ---" -ForegroundColor Cyan

# 1. Publish the project
Write-Host "Publishing project..." -ForegroundColor Yellow
dotnet publish $projectDir\NixPackTrace.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

# 2. Run Inno Setup Compiler
Write-Host "Compiling installer..." -ForegroundColor Yellow
if (Test-Path $isccPath) {
    & $isccPath $issFile
} else {
    Write-Host "ISCC.exe not found at $isccPath. Please check Inno Setup installation." -ForegroundColor Red
    exit 1
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "Installer compilation failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "--- Success! Setup EXE created in the Publish folder ---" -ForegroundColor Green
