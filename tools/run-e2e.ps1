param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $PlaywrightArgs = @()
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$npmCommand = Get-Command npm.cmd -ErrorAction SilentlyContinue

if (-not $npmCommand) {
    $npmCommand = Get-ChildItem -LiteralPath (Join-Path $env:LOCALAPPDATA 'nvm') -Recurse -Filter npm.cmd -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
}

if (-not $npmCommand) {
    throw 'npm.cmd was not found. Install Node.js/npm or make the nvm Node directory available on PATH.'
}

$npmPath = if ($npmCommand.Source) { $npmCommand.Source } else { $npmCommand.FullName }
$nodeDirectory = Split-Path -Parent $npmPath
$env:Path = "$nodeDirectory;$env:Path"

if ([string]::IsNullOrWhiteSpace($env:NODE_OPTIONS)) {
    $env:NODE_OPTIONS = '--use-system-ca'
}
elseif ($env:NODE_OPTIONS -notmatch '(^|\s)--use-system-ca(\s|$)') {
    $env:NODE_OPTIONS = "$env:NODE_OPTIONS --use-system-ca"
}

Push-Location $repoRoot
try {
    & $npmPath run e2e:visual -- @PlaywrightArgs
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
