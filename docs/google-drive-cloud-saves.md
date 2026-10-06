# Google Drive cloud saves

## User behavior

Characters has a **Cloud Saves** section. Connect Google Drive, upload a .dnd5e
character, and open it from the cloud list. The regular Save command and the
**Save to Drive** button upload the open cloud character. The normal local
character library remains separate.

Drive is authoritative. Opening or explicitly reloading a cloud character reads
Drive and updates the managed local copy. If Drive changed after the character
was opened, Save loads the newer Drive character automatically instead of
overwriting it. A notice explains what happened. Displaced edits and their
session sidecar are retained in the recovery folder.

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

1. In the application's Google Cloud project, enable the Google Drive API.
2. Configure OAuth consent for Aurora and the drive.file scope. During Google
   testing mode, add the intended Google account as a test user.
3. Create an OAuth client of type **Desktop app** and download its JSON.
4. In Characters > Cloud Saves, choose **Set up Google Drive** and import that
   JSON, then connect in the system browser.

The installation keeps the desktop client configuration in
FileSystem.Current.AppDataDirectory/google-drive-client.json. Refresh tokens
are stored through MAUI SecureStorage, keyed by client ID; they are not written
to logs, character XML, Git, or the recovery manifest. A release-wide client
configuration/consent setup is still needed before this can be a zero-setup
feature for ordinary users. Do not commit an actual client JSON here.

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
desktop client. Keep the existing Drive API, External/Testing audience, test
users, and `https://www.googleapis.com/auth/drive.file` scope. Using one project
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

## References

- [Google desktop OAuth and loopback redirects](https://developers.google.com/identity/protocols/oauth2/native-app)
- [Android native authorization and token renewal](https://developer.android.com/identity/authorization)
- [Drive per-file authorization scope](https://developers.google.com/workspace/drive/api/guides/api-specific-auth)
- [Drive v2 file ETag](https://developers.google.com/workspace/drive/api/reference/rest/v2/files)
- [Drive v2 media updates](https://developers.google.com/workspace/drive/api/reference/rest/v2/files/update)
- [Chromium's conditional Drive upload implementation](https://github.com/chromium/chromium/blob/main/google_apis/drive/drive_api_requests.cc)
