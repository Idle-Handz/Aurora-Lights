[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $PSScriptRoot '..\Aurora.App\BundledTools\AuroraTranslator'),
    [string]$ManifestPath = (Join-Path $PSScriptRoot 'translator-artifacts\manifest.json'),
    [switch]$VerifyOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$manifestFile = (Resolve-Path -LiteralPath $ManifestPath).Path
$manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
if ($manifest.manifestVersion -ne 1 -or $manifest.runtimeIdentifier -ne 'win-x64') {
    throw 'Unsupported Translator artifact manifest or runtime.'
}
if ([IO.Path]::GetFileName($manifest.archive) -cne $manifest.archive) { throw 'Archive must be beside its manifest.' }
$archive = Join-Path (Split-Path -Parent $manifestFile) $manifest.archive
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $manifest.sha256) {
    throw 'Pinned Translator archive SHA-256 mismatch.'
}
$target = [IO.Path]::GetFullPath($Destination)
$targetPrefix = $target.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $manifest.files) {
    $path = [IO.Path]::GetFullPath((Join-Path $target $file.path))
    if (-not $path.StartsWith($targetPrefix, [StringComparison]::OrdinalIgnoreCase) -or -not $expected.Add($file.path)) {
        throw "Invalid or duplicate artifact path: $($file.path)"
    }
}
if ($expected.Count -eq 0) { throw 'Empty artifact manifest.' }
if (-not $VerifyOnly -and (-not (Test-Path -LiteralPath $target) -or @(Get-ChildItem -LiteralPath $target -Force).Count -eq 0)) {
    [void][IO.Directory]::CreateDirectory($target)
    $stream = [IO.File]::OpenRead($archive)
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read)
    try {
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $zip.Entries) {
            if (-not $expected.Contains($entry.FullName) -or -not $seen.Add($entry.FullName)) { throw "Unexpected ZIP entry: $($entry.FullName)" }
        }
        if ($seen.Count -ne $expected.Count) { throw 'Archive is missing manifest files.' }
        foreach ($entry in $zip.Entries) {
            $path = [IO.Path]::GetFullPath((Join-Path $target $entry.FullName))
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
            $inputStream = $entry.Open()
            $outputStream = [IO.File]::Create($path)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose(); $inputStream.Dispose() }
        }
    } finally { $zip.Dispose(); $stream.Dispose() }
}
# Never overwrite a developer's different local snapshot. Restore into a fresh
# directory, or deliberately archive that snapshot before restoring the pin.
foreach ($file in $manifest.files) {
    $path = Join-Path $target $file.path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) {
        throw "Pinned Translator file missing or changed: '$path'. Use an empty destination to restore it."
    }
}
foreach ($file in (Get-ChildItem -LiteralPath $target -Recurse -File)) {
    $relative = [IO.Path]::GetRelativePath($target, $file.FullName).Replace('\', '/')
    if (-not $expected.Contains($relative)) { throw "Unexpected file in pinned Translator directory: '$relative'." }
}
Write-Output "Verified Translator $($manifest.productVersion), $($manifest.runtimeIdentifier), $($expected.Count) files at $target."
