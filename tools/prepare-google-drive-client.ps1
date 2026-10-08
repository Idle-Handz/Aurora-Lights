param(
    [string]$InputPath,
    [string]$OutputPath = (Join-Path $PSScriptRoot '../Aurora.App/Configuration/google-drive-client.json'),
    [switch]$Required
)

$ErrorActionPreference = 'Stop'
$json = if ($InputPath) { Get-Content -LiteralPath $InputPath -Raw } else { $env:GOOGLE_DRIVE_DESKTOP_CLIENT_JSON }
if ([string]::IsNullOrWhiteSpace($json)) {
    if ($Required) { throw 'GOOGLE_DRIVE_DESKTOP_CLIENT_JSON must contain the downloaded Desktop app OAuth JSON.' }
    Write-Host 'No bundled Google Drive client supplied; desktop will offer manual setup.'
    exit 0
}

# Reject Android, web and service-account downloads. Only embed the two client fields,
# never arbitrary properties, tokens or personal account data from the input file.
try {
    $download = ConvertFrom-Json -InputObject $json
    $client = $download.installed
    $loopback = @($client.redirect_uris | Where-Object {
        $uri = $null
        $_ -is [string] -and [Uri]::TryCreate($_, [UriKind]::Absolute, [ref]$uri) -and $uri.Scheme -eq 'http' -and $uri.IsLoopback
    }).Count -gt 0
    if ($client.client_id -isnot [string] -or $client.client_id -cnotmatch '^.+\.apps\.googleusercontent\.com$' -or
        $client.client_secret -isnot [string] -or [string]::IsNullOrWhiteSpace($client.client_secret) -or !$loopback) {
        throw 'Invalid desktop client'
    }
} catch {
    # Parsing errors must not echo the supplied credential JSON into CI logs.
    throw 'Expected a Google Desktop app OAuth download with a client ID, client secret and HTTP loopback redirect.'
}

$output = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
$options = [ordered]@{ ClientId = $client.client_id; ClientSecret = $client.client_secret } | ConvertTo-Json
[IO.File]::WriteAllText($output, $options, [Text.UTF8Encoding]::new($false))
Write-Host 'Prepared bundled desktop Google Drive configuration.'
