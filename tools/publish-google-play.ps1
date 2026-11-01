param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')][string]$VersionName,
    [Parameter(Mandatory)][string]$KeyStore,
    [string]$KeyAlias = 'aurora',
    [long]$VersionCode = 0,
    [string]$OutputDirectory
)

# Passwords are supplied through ANDROID_SIGNING_STORE_PASSWORD and
# ANDROID_SIGNING_KEY_PASSWORD, never command-line arguments or committed files.
$ErrorActionPreference = 'Stop'
if (!$env:ANDROID_SIGNING_STORE_PASSWORD -or !$env:ANDROID_SIGNING_KEY_PASSWORD) {
    throw 'Set ANDROID_SIGNING_STORE_PASSWORD and ANDROID_SIGNING_KEY_PASSWORD before publishing.'
}
$keyStorePath = (Resolve-Path -LiteralPath $KeyStore).Path
if ($VersionCode -eq 0) {
    $VersionCode = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() - 1577836800
}
if ($VersionCode -le 1 -or $VersionCode -gt 2100000000) {
    throw 'VersionCode must be between 2 and 2100000000 and exceed every previous Play upload.'
}
$repoRoot = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repoRoot "publish/google-play/$VersionCode" }
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($outputPath) | Out-Null
Remove-Item -LiteralPath (Join-Path $outputPath 'play-validation.json') -Force -ErrorAction SilentlyContinue
$storePassFile = [IO.Path]::GetTempFileName()
$keyPassFile = [IO.Path]::GetTempFileName()
$utf8 = [Text.UTF8Encoding]::new($false)

Push-Location $repoRoot
try {
    [IO.File]::WriteAllText($storePassFile, $env:ANDROID_SIGNING_STORE_PASSWORD, $utf8)
    [IO.File]::WriteAllText($keyPassFile, $env:ANDROID_SIGNING_KEY_PASSWORD, $utf8)
    $assemblyVersion = ($VersionName -split '-')[0]
    $properties = @(
        '-p:AuroraGooglePlayBuild=true',
        '-p:AndroidKeyStore=true',
        "-p:AndroidSigningKeyStore=$keyStorePath",
        "-p:AndroidSigningKeyAlias=$KeyAlias",
        "-p:AndroidSigningStorePass=file:$storePassFile",
        "-p:AndroidSigningKeyPass=file:$keyPassFile",
        "-p:ApplicationVersion=$VersionCode",
        "-p:ApplicationDisplayVersion=$VersionName",
        "-p:Version=$assemblyVersion",
        "-p:AssemblyVersion=$assemblyVersion.0",
        "-p:FileVersion=$assemblyVersion.0",
        "-p:InformationalVersion=$VersionName"
    )
    & dotnet publish Aurora.Reflections/Aurora.Reflections.csproj -f net10.0-android -r android-arm64 `
        -c Release -m:1 -o $outputPath @properties
    if ($LASTEXITCODE -ne 0) { throw "Android publish failed ($LASTEXITCODE)." }

    # Ask the workload that built the app for its tools; do not download a second,
    # potentially incompatible bundletool or guess a machine-specific SDK path.
    $sdkJson = & dotnet msbuild Aurora.Reflections/Aurora.Reflections.csproj -nologo `
        -p:TargetFramework=net10.0-android -p:RuntimeIdentifier=android-arm64 `
        -t:_ResolveSdks -getProperty:JavaSdkDirectory,AndroidSdkDirectory,MonoAndroidToolsDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the Android SDK tools.' }
    $sdk = ($sdkJson -join "`n" | ConvertFrom-Json).Properties
    $javaName = if ($IsWindows -or $env:OS -eq 'Windows_NT') { 'java.exe' } else { 'java' }
    $java = Join-Path $sdk.JavaSdkDirectory "bin/$javaName"
    $bundletool = Join-Path $sdk.MonoAndroidToolsDirectory 'bundletool.jar'
    $bundle = Join-Path $outputPath 'com.auroralights.app-Signed.aab'
    if (!(Test-Path -LiteralPath $bundle)) { throw 'The signed AAB was not produced.' }
    & python (Join-Path $PSScriptRoot 'verify-google-play-bundle.py') $bundle `
        --java $java --bundletool $bundletool --android-sdk $sdk.AndroidSdkDirectory `
        --keystore $keyStorePath --alias $KeyAlias --store-pass-file $storePassFile `
        --key-pass-file $keyPassFile --version-code $VersionCode --version-name $VersionName
    if ($LASTEXITCODE -ne 0) { throw 'Google Play bundle validation failed.' }
    Write-Host "Validated Google Play upload: $bundle"
} finally {
    Remove-Item -LiteralPath $storePassFile, $keyPassFile -Force -ErrorAction SilentlyContinue
    Pop-Location
}
