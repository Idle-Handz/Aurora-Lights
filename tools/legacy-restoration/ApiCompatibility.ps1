# Compare the immutable legacy contract as a subset of the current API.
# Member lines must be qualified by their declaring type: an identical signature
# on another type must never satisfy a removed legacy member.
function ConvertTo-QualifiedApi {
    param([Parameter(Mandatory)][string[]]$Lines)

    $declaringType = $null
    foreach ($line in $Lines) {
        if ($line.StartsWith('assembly ', [StringComparison]::Ordinal)) {
            $declaringType = $null
            $line
        }
        elseif ($line.StartsWith('type ', [StringComparison]::Ordinal)) {
            $declaringType = $line
            $line
        }
        elseif ($line.StartsWith('  ', [StringComparison]::Ordinal) -and $declaringType) {
            "$declaringType :: $($line.TrimStart())"
        }
        else {
            throw "Unexpected API surface line: '$line'."
        }
    }
}

function Compare-LegacyApi {
    param(
        [Parameter(Mandatory)][string[]]$OracleApi,
        [Parameter(Mandatory)][string[]]$RestoredApi
    )

    if (@($OracleApi | Where-Object { $_.StartsWith('assembly ', [StringComparison]::Ordinal) }).Count -ne 1 -or
        @($RestoredApi | Where-Object { $_.StartsWith('assembly ', [StringComparison]::Ordinal) }).Count -ne 1) {
        throw 'Both API surfaces must contain exactly one assembly identity.'
    }

    $required = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $current = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in (ConvertTo-QualifiedApi $OracleApi)) { [void]$required.Add($line) }
    foreach ($line in (ConvertTo-QualifiedApi $RestoredApi)) { [void]$current.Add($line) }

    [pscustomobject]@{
        Missing = @($required | Where-Object { -not $current.Contains($_) } | Sort-Object -CaseSensitive)
        Added = @($current | Where-Object { -not $required.Contains($_) } | Sort-Object -CaseSensitive)
        RequiredCount = $required.Count
    }
}
