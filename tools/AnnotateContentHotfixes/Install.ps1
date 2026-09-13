param([Parameter(Mandatory=$true)][string]$Evidence)
$ErrorActionPreference = 'Stop'
$report = Get-Content -LiteralPath $Evidence -Raw | ConvertFrom-Json
$allowedRoot = [IO.Path]::GetFullPath('C:\Users\Ralla\Documents\5e Character Builder\custom\user\local\id-hotfixes-20260913')
$backupRoot = [IO.Path]::GetFullPath('C:\Users\Ralla\Documents\Aurora Content Archive\2026-09-13-metadata-annotation')
if ($report.files.Count -ne 6 -or $report.operationCount -ne 10) { throw 'Unexpected annotation manifest.' }
if (Test-Path -LiteralPath $backupRoot) { throw 'Backup directory already exists; inspect previous deployment.' }
$destinations = @()
foreach ($entry in $report.files) {
    $destination = [IO.Path]::GetFullPath($entry.destination)
    if (-not $destination.StartsWith($allowedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Destination escaped the six-hotfix directory.' }
    if ($destinations -contains $destination) { throw 'Repeated destination.' }
    $destinations += $destination
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $entry.previousSha256) { throw "Local file changed: $destination" }
    if ((Get-FileHash -LiteralPath $entry.sourcePath -Algorithm SHA256).Hash -ne $entry.previousSha256) { throw "Authoritative input changed: $($entry.sourcePath)" }
    if ((Get-FileHash -LiteralPath $entry.stagedPath -Algorithm SHA256).Hash -ne $entry.annotatedSha256) { throw 'Staged annotation changed.' }
}
New-Item -ItemType Directory -Path $backupRoot | Out-Null
Copy-Item -LiteralPath $Evidence -Destination (Join-Path $backupRoot 'annotation-evidence.json')
$applied = @()
try {
    foreach ($entry in $report.files) {
        $backup = [IO.Path]::GetFullPath((Join-Path $backupRoot $entry.relativePath))
        if (-not $backup.StartsWith($backupRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Backup escaped archive.' }
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($backup)) -Force | Out-Null
        $pending = $entry.destination + '.annotation-' + [Guid]::NewGuid().ToString('N')
        try {
            Copy-Item -LiteralPath $entry.stagedPath -Destination $pending
            if ((Get-FileHash -LiteralPath $entry.destination -Algorithm SHA256).Hash -ne $entry.previousSha256) { throw 'Local file changed during deployment.' }
            [IO.File]::Replace($pending, $entry.destination, $backup)
            $applied += @{ Entry = $entry; Backup = $backup }
            if ((Get-FileHash -LiteralPath $entry.destination -Algorithm SHA256).Hash -ne $entry.annotatedSha256) { throw 'Installed hash mismatch.' }
        } finally { if (Test-Path -LiteralPath $pending) { Remove-Item -LiteralPath $pending } }
    }
} catch {
    foreach ($item in $applied) {
        if ((Get-FileHash -LiteralPath $item.Entry.destination -Algorithm SHA256).Hash -eq $item.Entry.annotatedSha256) {
            Copy-Item -LiteralPath $item.Backup -Destination $item.Entry.destination -Force
        }
    }
    throw
}
Write-Output "Installed six verified metadata annotations. Recoverable originals: $backupRoot"
