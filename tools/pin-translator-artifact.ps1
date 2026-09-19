[CmdletBinding()]
param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot '..\Aurora.App\BundledTools\AuroraTranslator'),
    [string]$ArtifactDirectory = (Join-Path $PSScriptRoot 'translator-artifacts')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
$deps = Get-Content -LiteralPath (Join-Path $source 'AuroraTranslator.deps.json') -Raw | ConvertFrom-Json
$runtime = Get-Content -LiteralPath (Join-Path $source 'AuroraTranslator.runtimeconfig.json') -Raw | ConvertFrom-Json
$rid = ($deps.runtimeTarget.name -split '/')[-1]
if ($rid -ne 'win-x64') { throw "This transitional pin supports win-x64, not '$rid'." }
foreach ($required in @('AuroraTranslator.exe', 'AuroraTranslator.dll', 'e_sqlite3.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $required) -PathType Leaf)) { throw "Missing $required" }
}
$artifactRoot = [IO.Path]::GetFullPath($ArtifactDirectory)
[void][IO.Directory]::CreateDirectory($artifactRoot)
$files = @(Get-ChildItem -LiteralPath $source -File -Recurse | Where-Object { $_.Name -notlike 'SNAPSHOT-*' } | Sort-Object FullName)
$inventory = @()
$buffer = [IO.MemoryStream]::new()
$zip = [IO.Compression.ZipArchive]::new($buffer, [IO.Compression.ZipArchiveMode]::Create, $true)
try {
    foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($source, $file.FullName).Replace('\', '/')
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        $inventory += [ordered]@{ path = $relative; bytes = $bytes.Length; sha256 = $hash }
        $entry = $zip.CreateEntry($relative, [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
        $stream = $entry.Open()
        try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
    }
} finally { $zip.Dispose() }
$archiveBytes = $buffer.ToArray()
$buffer.Dispose()
$archiveHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($archiveBytes)).ToLowerInvariant()
$archiveName = "aurora-translator-$rid-$($archiveHash.Substring(0, 16)).zip"
$archivePath = Join-Path $artifactRoot $archiveName
if (Test-Path -LiteralPath $archivePath) {
    if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $archiveHash) { throw "Existing artifact hash mismatch: $archivePath" }
} else { [IO.File]::WriteAllBytes($archivePath, $archiveBytes) }
$frameworkProperty = $runtime.runtimeOptions.PSObject.Properties['framework']
$manifest = [ordered]@{
    manifestVersion = 1
    archive = $archiveName
    sha256 = $archiveHash
    bytes = $archiveBytes.Length
    runtimeIdentifier = $rid
    productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $source 'AuroraTranslator.dll')).ProductVersion
    provenance = 'Captured from the installed local publish folder; productVersion is embedded build metadata, not proof of a clean source checkout.'
    selfContained = ($null -eq $frameworkProperty)
    requiredFramework = $(if ($frameworkProperty) { $frameworkProperty.Value } else { $null })
    excluded = @('SNAPSHOT-* (historical metadata; regenerated inventory is authoritative for this artifact)')
    files = $inventory
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $artifactRoot 'manifest.json') -Encoding utf8
Write-Output "Pinned $archiveName ($($archiveBytes.Length) bytes, $($inventory.Count) files)."
