# Google Play build and first-upload checklist

The Play build is separate from the existing GitHub APK release. Preparing it does
not publish an app, create a Play release, or require a D-U-N-S number. Account
verification and the first Play upload remain manual steps.

## Build identity

- App: **Aurora: Reflections**
- Permanent package: **com.auroralights.app** (same as the existing Android APK)
- Format: release-signed **Android App Bundle (.aab)**
- Devices: **ARM64**, Android 7.0 / API 24 and later. This first Play bundle does
  not include 32-bit ARM or x86/x64 devices.
- Target: checked against **API 36 or later**, the phone/tablet submission
  requirement as of October 2026.
- Native packaging: 16 KB alignment requested in the bundle configuration;
  actual native ELF segments and generated APK ZIP alignment are verified.
- Version code: UTC seconds since 2020-01-01, matching the APK workflows' scheme.
  Every Play upload needs a new code greater than any previously uploaded code.
  Do not rebuild/re-upload a different binary with the same code.

`AuroraGooglePlayBuild=true` selects AAB packaging and compiles the Play update
behavior. Settings > Updates offers **Open Google Play**; manual and startup
GitHub binary checks are disabled even if an older installation enabled them.
Content-source refresh is still available. Ordinary APK and desktop builds keep
their existing update behavior.

## Build in GitHub Actions

After this workflow is on the default branch:

1. Open Actions > **Build Google Play bundle** > Run workflow.
2. Select the source branch and enter its display version, for example
   `0.9.6-beta`. This does not create a Git tag or GitHub release.
3. Download **google-play-arm64** after the job succeeds.
4. The only Play upload is **com.auroralights.app-Signed.aab**. Keep its
   `play-validation.json`, `play-manifest.xml`, and `play-bundle-config.json`
   alongside it as build evidence.
5. The separate **google-play-arm64-device-test** artifact contains an `.apks`
   archive for bundletool/adb testing. It is not a Play upload.

The workflow uses the existing `ANDROID_KEYSTORE_BASE64` secret and `aurora`
alias. Its password defaults match the existing Android workflows. Optional
`ANDROID_SIGNING_STORE_PASSWORD` and `ANDROID_SIGNING_KEY_PASSWORD` secrets
override those defaults; coordinate any key/password changes with the APK
workflows. Never commit a keystore or password file.

## Build locally

Prerequisites: .NET 10 with MAUI Android support, Android SDK platform 36+ and
build-tools 35+, JDK 21, Python 3.10+, and PowerShell. The script uses bundletool
from the installed .NET Android workload.

Supply the existing signing passwords through `ANDROID_SIGNING_STORE_PASSWORD`
and `ANDROID_SIGNING_KEY_PASSWORD` in the process environment, then run:

```powershell
./tools/publish-google-play.ps1 `
    -VersionName '0.9.6-beta' `
    -KeyStore ./aurora-release.jks
```

Output defaults to `publish/google-play/<versionCode>/`. `-OutputDirectory` can
select another output folder. `-VersionCode` is available for controlled release
numbering; it must remain greater than previous uploads. The script writes
temporary password files for AAB signing and removes them in `finally`.

The validator checks:

- Bundle signature against the supplied upload key, including unsigned entries.
- Bundletool structural validation, package name, exact version, minimum/target
  SDK, and absence of debug/test-only flags or APK-install permission.
- ARM64 native binaries, 16 KB LOAD alignment, and separation of rounded RELRO
  protection from writable data.
- The bundle's 16 KB ZIP alignment configuration.
- Bundletool-generated APK signatures and 16 KB ZIP alignment.

The report describes packaging checks, not runtime or Play review approval.
Installing the `.apks` archive requires bundletool:

```text
java -jar <bundletool.jar> install-apks --apks=<output>/play-device-test.apks
```

## Signing and Drive authorization before the first Play test

1. Complete the Google Play organization account once D&B issues the D-U-N-S
   number, then create the app and choose an internal testing track.
2. Decide Play App Signing before the first release. The existing release key
   signs the upload bundle. If Play uses a different app-signing key, its delivered
   APKs cannot update existing sideloaded APKs signed by the old key. To preserve
   that update path, use Play's process for supplying the existing app-signing key.
   Otherwise, export/save local characters before changing installations.
3. In Play Console's App integrity/App signing page, obtain the **app-signing
   certificate SHA-1**. The report's upload-certificate SHA-1 is not necessarily
   the same certificate.
4. Add an Android OAuth client for `com.auroralights.app` and that SHA-1 in the
   **same Google Cloud project** as the existing desktop and Android clients.
   No Android client-secret JSON needs to be bundled. See
   [Google Drive setup](google-drive-cloud-saves.md#android-setup-and-cross-device-saves).
5. Test an installation delivered through Play's **internal testing track**.
   Internal app sharing can use a different signing certificate; it is not a
   substitute for verifying the production app-signing/OAuth registration.

## Runtime acceptance before broader release

- Cold start, content setup/refresh, opening a character, and responsive loading.
- Local save/load and session data, file picker/import, export and PDF generation.
- Drive notice and policy links before consent; connect with a non-test Google
  account; open, edit, save, disconnect/reconnect and cross-load on Windows.
- Session sidecars and conflict/recovery behavior survive cross-device saves.
- Settings > Updates opens Play and does not offer the GitHub APK.
- Upgrade from the previous internal test without losing local characters.
- Exercise a 16 KB page-size device/emulator as well as a normal device. Passing
  static alignment checks alone does not demonstrate runtime compatibility.

## Play Console material still to complete

- Listing title, short/full descriptions, current phone screenshots, app icon and
  feature graphic. Use assets/content that may be redistributed publicly.
- Support email: `idlehandzonline@gmail.com`.
- Homepage: `https://aurora.idlehandz.com/reflections/`.
- Privacy policy: `https://aurora.idlehandz.com/privacy/`.
- Terms: `https://aurora.idlehandz.com/terms/`.
- Data Safety answers based on the actual local/Drive data flow, content rating,
  target audience, ads declaration, and reviewer instructions for optional Drive
  access. Do not infer these declarations from a successful build.
- Upload the signed AAB to internal testing, inspect device compatibility and the
  pre-launch report, resolve Play Console findings, then choose a broader release.

## Official references

- [Target API requirements](https://support.google.com/googleplay/android-developer/answer/11926878)
- [16 KB compatibility checks](https://developer.android.com/guide/practices/page-sizes)
- [Play App Signing](https://developer.android.com/studio/publish/app-signing)
- [.NET MAUI Android publishing](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-cli?view=net-maui-10.0)
- [Create a Play app](https://support.google.com/googleplay/android-developer/answer/9859152)
- [Prepare for review](https://support.google.com/googleplay/android-developer/answer/9859455)
