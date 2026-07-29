[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$libraryRoot = Join-Path $repositoryRoot 'lib'
$oracleRoot = Join-Path $repositoryRoot 'tests\LegacyOracles'

$loadPaths = @(
    (Join-Path $oracleRoot 'Builder.Core.dll'),
    (Join-Path $libraryRoot 'DynamicExpresso.Core.dll'),
    (Join-Path $libraryRoot 'itextsharp.dll'),
    (Join-Path $libraryRoot 'System.Windows.Interactivity.dll'),
    (Join-Path $oracleRoot 'Builder.Data.dll'),
    (Join-Path $oracleRoot 'Aurora.Documents.dll'),
    (Join-Path $oracleRoot 'Aurora.Presentation.dll')
)

$loadedAssemblies = @{}
foreach ($path in $loadPaths) {
    if (Test-Path -LiteralPath $path) {
        $fileName = Split-Path -Leaf $path
        $loadedAssemblies[$fileName] = [System.Reflection.Assembly]::LoadFrom($path)
    }
}

$firstPartyBinaries = @(
    'Builder.Core.dll',
    'Builder.Data.dll',
    'Aurora.Documents.dll',
    'Aurora.Presentation.dll'
)

$records = foreach ($fileName in $firstPartyBinaries) {
    $path = Join-Path $oracleRoot $fileName
    $assembly = $loadedAssemblies[$fileName]
    $exportedTypes = @($assembly.GetExportedTypes())

    $resources = foreach ($resourceName in $assembly.GetManifestResourceNames() | Sort-Object) {
        $stream = $assembly.GetManifestResourceStream($resourceName)
        try {
            $sha256 = [System.Security.Cryptography.SHA256]::Create()
            try {
                $resourceHash = ([BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
            }
            finally {
                $sha256.Dispose()
            }

            [ordered]@{
                name = $resourceName
                size = $stream.Length
                sha256 = $resourceHash
            }
        }
        finally {
            if ($null -ne $stream) {
                $stream.Dispose()
            }
        }
    }

    $targetFrameworkAttribute = $assembly.GetCustomAttributesData() |
        Where-Object { $_.AttributeType.FullName -eq 'System.Runtime.Versioning.TargetFrameworkAttribute' } |
        Select-Object -First 1

    [ordered]@{
        path = "tests/LegacyOracles/$fileName"
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        size = (Get-Item -LiteralPath $path).Length
        assemblyName = $assembly.GetName().Name
        assemblyVersion = $assembly.GetName().Version.ToString()
        fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion
        targetFramework = $targetFrameworkAttribute.ConstructorArguments[0].Value
        moduleVersionId = $assembly.ManifestModule.ModuleVersionId.ToString()
        publicTypeCount = $exportedTypes.Count
        resources = @($resources)
    }
}

[ordered]@{
    schemaVersion = 1
    scope = 'Test-only production first-party oracle baseline. Aurora Studio is intentionally excluded.'
    binaries = @($records)
} | ConvertTo-Json -Depth 8
