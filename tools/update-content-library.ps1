# update-content-library.ps1
#
# Packs Aurora.Content and Aurora.Content.Contracts from the sibling AuroraTranslator checkout,
# vendors the packages into vendor/nuget, records their provenance in vendor/nuget/manifest.json,
# and pins that version in AuroraContent.props.
#
# The version comes from AuroraTranslator's Aurora.Content.props. Vendored versions are immutable:
# NuGet caches packages by version, so changed contents under an existing version would be
# silently ignored on machines that already restored it. Bump the version for every change.
#
# Usage (from any directory):
#   .\tools\update-content-library.ps1
#   .\tools\update-content-library.ps1 -TranslatorRepo D:\src\AuroraTranslator
[CmdletBinding()]
param(
    [string]$TranslatorRepo,
    [switch]$AllowDirty
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 leaves $PSScriptRoot empty inside param() defaults.
if (-not $TranslatorRepo) { $TranslatorRepo = Join-Path $PSScriptRoot '..\..\5eApiTranslator' }
$utf8 = [Text.UTF8Encoding]::new($false)
$repo = Split-Path -Parent $PSScriptRoot
$translator = (Resolve-Path -LiteralPath $TranslatorRepo).Path
if ((git -C $translator status --porcelain) -and -not $AllowDirty) {
    throw "AuroraTranslator has uncommitted changes. Commit them first so the vendored packages map to a real commit, or pass -AllowDirty for a local experiment."
}
$commit = (git -C $translator rev-parse HEAD).Trim()
[xml]$props = Get-Content -LiteralPath (Join-Path $translator 'Aurora.Content.props') -Raw
$version = $props.Project.PropertyGroup.Version
if (-not $version) { throw 'Aurora.Content.props does not declare a Version.' }

$feed = Join-Path $repo 'vendor\nuget'
New-Item -ItemType Directory -Force -Path $feed | Out-Null
$names = @('Aurora.Content.Contracts', 'Aurora.Content')
foreach ($name in $names) {
    if (Test-Path -LiteralPath (Join-Path $feed "$name.$version.nupkg")) {
        throw "$name $version is already vendored. Bump Version in Aurora.Content.props; vendored versions are immutable."
    }
}

$staging = Join-Path ([IO.Path]::GetTempPath()) ('aurora-content-pack-' + [guid]::NewGuid().ToString('N'))
try {
    foreach ($name in $names) {
        dotnet pack (Join-Path $translator "$name\$name.csproj") -c Release -o $staging --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for $name." }
    }
    $packages = foreach ($name in $names) {
        $file = Join-Path $staging "$name.$version.nupkg"
        if (-not (Test-Path -LiteralPath $file)) { throw "Expected package $name.$version.nupkg was not produced." }
        Copy-Item -LiteralPath $file -Destination $feed
        [ordered]@{ file = "$name.$version.nupkg"; sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
} finally {
    Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
}

$manifestPath = Join-Path $feed 'manifest.json'
$history = @()
if (Test-Path -LiteralPath $manifestPath) { $history = @((Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).versions) }
$entry = [ordered]@{
    version = $version
    sourceRepository = 'https://github.com/Xellarant/AuroraTranslator'
    sourceCommit = $commit
    dirtySource = [bool]$AllowDirty
    vendoredUtc = (Get-Date).ToUniversalTime().ToString('o')
    packages = @($packages)
}
$manifest = [ordered]@{ current = $version; versions = @($history) + @($entry) } | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText($manifestPath, $manifest, $utf8)

$pin = Join-Path $repo 'AuroraContent.props'
$text = Get-Content -LiteralPath $pin -Raw
$updated = [regex]::Replace($text, '<AuroraContentVersion>[^<]*</AuroraContentVersion>', "<AuroraContentVersion>$version</AuroraContentVersion>")
if ($updated -eq $text -and $text -notmatch "<AuroraContentVersion>$([regex]::Escape($version))</AuroraContentVersion>") {
    throw 'Could not update AuroraContentVersion in AuroraContent.props.'
}
[IO.File]::WriteAllText($pin, $updated, $utf8)

Write-Output "Vendored Aurora.Content $version from $($commit.Substring(0, 12)) and pinned it in AuroraContent.props."
