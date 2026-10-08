# Google Drive cloud saves

## User behavior

Characters has a **Cloud Saves** section. Connect Google Drive, upload a .dnd5e
character, and open it from the cloud list. The regular Save command and the
**Save to Drive** button upload the open cloud character. The normal local
character library remains separate.

To upload a character saved inside the app, open its **Overview** and choose
**Upload to Drive**. This saves current character edits and includes the session
state (HP, spell slots and resources), without opening a native file picker.
The local character remains on the device; open the new copy from **Cloud Saves**
to continue saving to Drive. **Upload external file** remains available for files
accessible through the system picker.

Drive is authoritative. Opening or explicitly reloading a cloud character reads
Drive and updates the managed local copy. If Drive changed after the character
was opened, Save loads the newer Drive character automatically instead of
overwriting it. A notice explains what happened. Displaced edits and their
session sidecar are retained in the recovery folder.

A partial reload reports the missing saved picks and does not mark the tab clean
or report a successful save. If the downloaded character cannot initialize at
all, editing and saving are blocked until a successful reload, so the previous
character model cannot overwrite the downloaded file. Recovery copies remain
available in both cases.

Normal editor autosaves can update the managed local working copy. The cloud
banner continues to say **Changes not yet saved to Drive** until an explicit
cloud save succeeds. Session's Save button also saves to Drive. A failed or
cancelled upload does not mark the cloud character saved. There is no background
two-way sync, offline cloud loading, or automatic force-overwrite.

Cloud payloads remain Aurora XML. An aurora-cloud-session node carries the
session JSON (HP, slots, resources, etc.). Download extracts that into the
existing session sidecar. Successful upload mirrors the exact confirmed XML
locally, unless another local edit arrived while the request was running.

## Desktop sign-in setup

Windows desktop uses browser sign-in; macOS desktop follows the same loopback
flow but still needs platform verification. Android uses native Google Play
services authorization, described below. The existing web host is unchanged.

Official desktop releases embed Aurora's Desktop OAuth client configuration.
On a fresh installation, choose Characters > Cloud Saves > **Connect Google
Drive** and authorize your own account in the system browser. Users do not need
a Google Cloud project or a downloaded configuration file.

An existing installation's imported configuration at
FileSystem.Current.AppDataDirectory/google-drive-client.json takes precedence.
This preserves its Google project and client-keyed credentials. Such installations
retain **Change Google Drive setup**; a fresh bundled installation does not show
that control. Developer builds without a bundle retain **Set up Google Drive**.
Manual import accepts a downloaded **Desktop app** OAuth JSON, not an Android,
web, or service-account credential.

Refresh tokens are stored through MAUI SecureStorage, keyed by client ID; they
are not written to logs, character XML, Git, or the recovery manifest. Bundled
configuration identifies the application; it contains no user's Google account
credentials, access tokens, or refresh tokens. Installed OAuth clients cannot
keep their client configuration confidential after distribution.

### Maintaining the release configuration

1. In Aurora's Google Cloud project, enable the Drive API, configure the
   `drive.file` scope, and create an OAuth client of type **Desktop app**.
   Keep this in the same project as the Android clients.
2. Store the complete downloaded JSON in the GitHub Actions repository secret
   **GOOGLE_DRIVE_DESKTOP_CLIENT_JSON**. Do not commit the downloaded JSON.
3. The Windows and macOS workflows run `tools/prepare-google-drive-client.ps1`.
   It validates the desktop download and writes only ClientId and ClientSecret
   to the ignored `Aurora.App/Configuration/google-drive-client.json`. The app
   embeds that file as `Aurora.GoogleDriveClient.json` on desktop only.
4. The release workflow requires this secret and fails if it is missing or
   invalid. Other desktop build workflows can omit it for source/fork builds
   with manual setup. Android continues to use native authorization.

For a local bundled build, run the following before building the desktop app:

```powershell
./tools/prepare-google-drive-client.ps1 -InputPath ./client_secret_DESKTOP.apps.googleusercontent.com.json -Required
```

The script accepts `GOOGLE_DRIVE_DESKTOP_CLIENT_JSON` as an environment variable
instead of `-InputPath`. `AuroraGoogleDriveClientConfiguration` can override the
path to the prepared file; `AuroraRequireGoogleDriveClientConfiguration=true`
makes desktop MSBuild fail if it is absent. The default prepared path is ignored
by Git. Keep real credentials out of test fixtures, source, and build logs.

Bundling does not change Google's audience or verification settings. While the
OAuth project is in Testing, users must be listed as test users. Public access
requires an External/In production audience and the applicable public website,
privacy disclosures, and branding setup in Google Auth Platform.

The drive.file grant lists files created by or explicitly authorized for this
app. It does not browse every arbitrary file in a user's Drive. Upload creates
a new cloud character without deleting the local original. Disconnect removes
the stored credential on this installation; close cloud tabs first. The user
can revoke the app's Google grant separately in Google account settings.
Reconnect renews authorization for the same account. Choosing a different
account is rejected before replacing the existing credential; disconnect first
to switch accounts.

## Android setup and cross-device saves

Create the Android OAuth client in the **same Google Cloud project** as the
desktop client. Keep the existing Drive API, External audience, and
`https://www.googleapis.com/auth/drive.file` scope. In Testing, add test users;
use In production for public access. Using one project
is important for both clients to access the app's Drive files.

1. Open Google Auth Platform > Clients > Create client > Android.
2. Enter package name **com.auroralights.app**.
3. Enter the SHA-1 of the certificate that signs the APK being installed. Local
   debug, release, and Google Play signing certificates can differ; register a
   separate Android client for each certificate you use. For a built APK, run
   `apksigner verify --print-certs path/to/app.apk`. For Google Play delivery,
   use the Play app-signing certificate rather than the upload certificate.
4. Install a build containing this Android integration on a device with Google
   Play services. Open Characters > Cloud Saves > Connect Google Drive, choose
   the same Google account used on desktop, and grant file access. Android does
   not import desktop client JSON or require a client secret or web backend.
5. On desktop, upload a disposable character and open it from Cloud Saves.
   Save it explicitly. On Android, Refresh the cloud list and Open that same
   file. Make a change and choose Save to Drive. Close/reopen or explicitly
   reload it on desktop and check the change, including HP and spell slots.
   Repeat in the opposite direction. Upload creates another file; do not
   upload the same local character on each device to try to link them.

Android asks Google Play services for access tokens as needed. It persists only
the verified Drive account ID/email in SecureStorage. Silent restoration or
renewal never opens consent UI; when consent is needed, choose Reconnect.
Every token is checked against the bound Drive account before a document request
can use it. Disconnect removes this installation's account binding and allows a
new account selection; it does not revoke permissions on other devices.

An authorization error with code 10 usually means the package/certificate
registration does not match this build. Missing Play services, cancelled sign-in,
and refused consent are reported without marking the connection or save successful.

Both platforms use the same authoritative Drive saves, explicit-save behavior,
session payload, and conflict recovery. This is not background synchronization;
reload a cloud character to pick up changes saved on another device.

## Save safety

- Cloud metadata carries the version opened by Aurora. Before writing, check
  the current version and obtain the v2 file ETag.
- Use PUT /upload/drive/v2/files/{id} with that strong ETag in If-Match.
  This protects the interval between the preflight read and upload. Missing,
  weak, or wildcard tags are refused; no unconditional fallback exists.
- Treat HTTP 412 as a concurrent change and reload the authoritative cloud
  version, archiving displaced local work first.
- Verify Drive's returned MD5 and byte count against uploaded/downloaded bytes.
  Downloads also recheck metadata afterward so content and version cannot come
  from different remote revisions.
- Validate character XML with DTDs disabled and limit character payloads to
  32 MiB. A download failure never replaces the current working character.
- Keep the exact upload candidate in character.dnd5e.pending. Advance the
  receipt only after a verified provider response. An uncertain network outcome
  remains unsaved locally; retry encounters a newer remote version if Drive
  actually accepted the previous upload.
- Isolate workspaces by account, provider and file ID. Hold an exclusive local
  lease while the character is open. Temporary files are replaced atomically;
  recovery copies have unique names and are not silently pruned.
- Keep newer local or in-memory edits dirty if they arrive during upload.
  Do not close a tab while its Save command is running.

Managed copies are under
FileSystem.Current.AppDataDirectory/Cloud Saves/<identity hash>/.
The cloud banner's **Recovery Files** button opens that character's recovery
folder on Windows. Recovery files can be opened through the existing local
Open File flow. They are not silently uploaded back over Drive.

## Validation and remaining live acceptance

Automated tests cover metadata/download consistency, conditional-write races,
missing ETags, checksum failures, interrupted saves, receipt advancement,
account isolation, simultaneous local edits, authoritative reload/recovery,
session-state transport, and OAuth callback state/redirect checks. An engine
integration test exercises the actual Save command's conflict reload. Desktop
sign-in tests use a real loopback connection with a simulated Google token
endpoint to verify PKCE and account validation before credential replacement.

Local validation on 2026-10-03: the full suite passed 832 tests before the final
two desktop sign-in cases were added. The final focused storage, sign-in and
file-write run passed all 43 cases. The Windows app build passed with two
existing nullable warnings in BuildService.CustomFeatures.cs.

Before release, use a configured test account and disposable characters:

1. Connect, upload, list, open, edit and save. Restart, reconnect, and load on
   another installation; verify the portrait, build choices, HP and spell slots.
2. Change the cloud file from another client after Aurora opens it. Saving in
   Aurora must load that Drive version and retain the displaced local edits.
3. Exercise an actual stale v2 If-Match upload against a disposable file and
   confirm HTTP 412 and unchanged remote bytes. Mocked tests alone do not prove
   Google's live conditional-write behavior.
4. Interrupt networking during upload. The UI must not say the cloud save
   succeeded; reconnect and load the authoritative version. Check recovery.
5. Deny/revoke consent and test reconnection, cancellation and local credential
   removal. Confirm no tokens or authorization codes appear in the console.

No live Google account round-trip has been performed in the initial implementation.
Android and desktop cross-device acceptance remains necessary with registered
clients and a real test account; compilation and simulated-provider tests do not
establish live Google sign-in or file visibility across clients.

Android implementation validation on 2026-10-05: 55 focused Drive authorization,
document-store, cloud-session, and cross-device tests passed. The new cases
cover native account restoration, account-switch refusal on renewal/reconnect,
disconnect, cancelled/refused authorization, and bidirectional character/session
transfer between separate device workspaces with stale-save recovery. Windows
and Android ARM64 debug builds passed with existing nullable warnings. The ARM64
APK's package and signing certificate were checked; it uses the local debug
certificate and is not an update for an installation signed with the release key.

## Content database compatibility

Build Drive-enabled installations from the current application baseline. The
Android release test build uses Aurora.Content 0.10.1 (data version 18), alongside
the current prepared-content loader and Android GC configuration. An older Drive
branch using Aurora.Content 0.9.0 (data version 17) cannot read an existing v18
database; its attempted XML fallback can then surface unrelated persisted
conflict exclusions as a startup error. Update the app rather than deleting or
downgrading the content database or bypassing those exclusions.

Validation on 2026-10-06: all 79 focused Drive, cloud-session, prepared-projection,
and alias-isolation tests passed. The integrated loader opened an unchanged copy
of the phone's v18 database and loaded 20,308 prepared elements without XML
fallback. The release-signed ARM64 update installed in place and launched; the
phone database SHA-256 remained unchanged. A separate local character rehearsal
reported six saved-summary choices that were not restored, so full character
and live cross-device acceptance is still pending.

## Android loading feedback verification (2026-10-06)

Release-signed ARM64 build 213490464 was installed as an in-place update on the
connected Galaxy S22 Ultra. All 33 focused character-refresh, companion,
load-compatibility, partial-load-report, cloud-session, and cross-device tests
passed. On-device opening of Testy McGee displayed an animated full-screen
spinner through download and character restoration, then returned to the
character page without opening the keyboard. Reopening the already-open Drive
character also showed the spinner and returned successfully. No new skipped-frame messages or
fatal errors were logged during that warm open; cold content initialization
still produces skipped-frame messages and is not covered by that observation.

The live Drive character reports 23 saved picks that could not be restored,
including weapon proficiencies. This was reproduced both before and after this
update and is separate from the resolved v17/v18 database compatibility error.
This update does not repair or suppress that warning. No save was uploaded to
Drive during these checks; full cross-device save/load acceptance remains open.

## Live desktop / Android round-trip (2026-10-08)

The installed 0.9.4 releases completed a live Google Drive round-trip using a
disposable character: Windows desktop (0.9.4-beta, commit `25e9476`) and Samsung
SM-S366V Android (version code 213499260). This validates those installed builds,
not the subsequent uncommitted navigation or client-configuration changes.

- Desktop Overview uploaded the character and full session sidecar with 137 gp,
  23 HP and 4 temporary HP. Android opened that cloud copy with the seeded
  conditions, inspiration, exhaustion, spell slots, resources and attack reminders.
- Android saved 139 gp, 17 HP and 2 temporary HP. Desktop's Load Drive Save
  retrieved those values; every other session field matched the seed exactly.
- Desktop saved 141 gp and 19 HP. Android retrieved 141 gp, 19 HP and 2 temporary
  HP, then retained those values after closing the tab, restarting the app and
  reopening the cloud character. Saved authorization was restored on restart.
- All 21 registered build-choice IDs, all 10 inventory rows, notes and embedded
  portrait content survived the return transfer. Character and session hashes
  matched the desktop cloud receipt.

Android reported 17 unresolved saved entries, including Claw, Chakram and
Kusarigama proficiencies. Desktop saving had added exactly 17 granted weapon
proficiencies from its Ryoko content; that content directory was absent on the
phone. These were granted entries rather than changes to the 21 registered
choices, and the returned XML retained their IDs. Explicit reload correctly
reported the partial load and kept the tab marked for attention. This content
compatibility warning remains separate from the successful transport check.

The desktop Open File importer copied only the selected XML, so the prepared
sidecar was placed beside the disposable imported copy and verified in Session
before upload. This run does not validate sidecar import through a native picker.
Live stale-write races/HTTP 412, network interruption, revoked consent and
recovery-file restoration were not exercised. Local evidence is retained under
`buildtmp/drive-roundtrip-20261008`; the distinctly named disposable Drive save is
left available for inspection.

## References

- [Google desktop OAuth and loopback redirects](https://developers.google.com/identity/protocols/oauth2/native-app)
- [Android native authorization and token renewal](https://developer.android.com/identity/authorization)
- [Drive per-file authorization scope](https://developers.google.com/workspace/drive/api/guides/api-specific-auth)
- [Drive v2 file ETag](https://developers.google.com/workspace/drive/api/reference/rest/v2/files)
- [Drive v2 media updates](https://developers.google.com/workspace/drive/api/reference/rest/v2/files/update)
- [Chromium's conditional Drive upload implementation](https://github.com/chromium/chromium/blob/main/google_apis/drive/drive_api_requests.cc)
