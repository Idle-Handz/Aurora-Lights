# publish-translator.ps1
#
# Snapshots the current state of 5eApiTranslator into Aurora.App/BundledTools/AuroraTranslator/.
# Aurora.App's "Sync Now" button will use this snapshot if present, falling back to the built-in
# importer when it is absent.
#
# Usage (from any directory):
#   .\tools\publish-translator.ps1
#
# Re-run whenever you want to update the snapshot after making translator changes.

$repoRoot       = Split-Path -Parent $PSScriptRoot
$translatorProj = Join-Path $repoRoot "..\5eApiTranslator\5eApiTranslator\AuroraTranslator.csproj"
$outputDir      = Join-Path $repoRoot "Aurora.App\BundledTools\AuroraTranslator"

if (-not (Test-Path $translatorProj)) {
    Write-Error "Could not find AuroraTranslator.csproj at: $translatorProj"
    Write-Error "Adjust the path at the top of this script if your repo layout differs."
    exit 1
}

Write-Host "Snapshotting AuroraTranslator -> $outputDir" -ForegroundColor Cyan

dotnet publish $translatorProj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $outputDir `
    --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed (exit code $LASTEXITCODE)."
    exit 1
}

Write-Host "Snapshot complete." -ForegroundColor Green
Write-Host "Build and run Aurora.App to activate the new snapshot." -ForegroundColor Green
