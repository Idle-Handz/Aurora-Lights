[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ApiCompatibility.ps1')

$legacy = @(
    'assembly Example 1.0.0.0'
    'type public class Example.First base=System.Object'
    '  ctor public ()'
    '  method public System.Void Run(System.String value)'
    '  method public T Create<T>() [constraint T : class,new()]'
    '  property System.String Name {get=public,set=public}'
    '  field public const System.Int32 Value = 1'
    '  event System.EventHandler Changed {add=public,remove=public}'
    'type public class Example.Second base=System.Object'
    '  ctor public ()'
    '  method public System.Void Run(System.String value)'
)
$script:checks = 0

function Assert-Comparison {
    param([string]$Name, [string[]]$Current, [int]$Missing, [int]$Added)
    $result = Compare-LegacyApi -OracleApi $legacy -RestoredApi $Current
    if ($result.Missing.Count -ne $Missing -or $result.Added.Count -ne $Added) {
        throw "${Name}: expected missing=$Missing added=$Added; got missing=$($result.Missing.Count) added=$($result.Added.Count)."
    }
    $script:checks++
}

Assert-Comparison 'Unchanged' $legacy 0 0
Assert-Comparison 'New type and its members' ($legacy + @('type public class Example.New base=System.Object', '  ctor public ()')) 0 2
$withOverload = @($legacy[0..3]) + '  method public System.Void Run(System.Int32 value)' + @($legacy[4..10])
Assert-Comparison 'New overload on a legacy type' $withOverload 0 1
Assert-Comparison 'Deleted type' @($legacy[0..7]) 3 0
# First.Run must not be satisfied by the identical Second.Run signature.
Assert-Comparison 'Deleted member with matching signature on another type' (@($legacy[0..2]) + @($legacy[4..10])) 1 0
foreach ($case in @(
    @('Parameter type', 3, '  method public System.Void Run(System.Int32 value)'),
    @('Return type', 3, '  method public System.Boolean Run(System.String value)'),
    @('Visibility', 3, '  method protected System.Void Run(System.String value)'),
    @('Case-sensitive member name', 3, '  method public System.Void run(System.String value)'),
    @('Generic constraint', 4, '  method public T Create<T>() [constraint T : struct]'),
    @('Property setter removed', 5, '  property System.String Name {get=public}'),
    @('Constant changed', 6, '  field public const System.Int32 Value = 2'),
    @('Event handler changed', 7, '  event System.Action Changed {add=public,remove=public}'),
    @('Assembly name', 0, 'assembly Renamed 1.0.0.0'),
    @('Assembly version', 0, 'assembly Example 2.0.0.0')
)) {
    $changed = $legacy.Clone()
    $changed[$case[1]] = $case[2]
    Assert-Comparison $case[0] $changed 1 1
}
$changed = $legacy.Clone()
$changed[1] = 'type public class Example.First base=Example.Other'
Assert-Comparison 'Changed base type' $changed 7 7

$rejectedMalformed = $false
try { Compare-LegacyApi -OracleApi @('  ctor public ()') -RestoredApi $legacy | Out-Null }
catch { $rejectedMalformed = $true }
if (-not $rejectedMalformed) { throw 'Malformed oracle must fail closed.' }
$script:checks++
Write-Output "Passed $script:checks API compatibility regression checks."
