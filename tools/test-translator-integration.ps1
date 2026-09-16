[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$TranslatorExecutable,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This external Translator integration requires Windows.'
}
if (-not (Test-Path -LiteralPath $TranslatorExecutable -PathType Leaf)) {
    throw "Publish or supply AuroraTranslator before running this integration: '$TranslatorExecutable'. See docs/translator-integration-tests.md."
}
$executable = (Resolve-Path -LiteralPath $TranslatorExecutable).Path
$project = Join-Path $PSScriptRoot '..\Aurora.Tests\Aurora.Tests.csproj'
$previous = $env:AURORA_TEST_TRANSLATOR
try {
    $env:AURORA_TEST_TRANSLATOR = $executable
    $testArguments = @('test', $project, '--configuration', $Configuration,
        '--filter', 'Category=TranslatorIntegration', '--verbosity', 'minimal')
    if ($NoRestore) { $testArguments += '--no-restore' }
    & dotnet @testArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Translator integration failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:AURORA_TEST_TRANSLATOR = $previous
}
