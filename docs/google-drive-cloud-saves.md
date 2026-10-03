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

This first implementation supports Windows desktop; macOS desktop follows the
same loopback flow but still needs platform verification. Android sign-in is
not enabled. The existing web host is unchanged.

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

## References

- [Google desktop OAuth and loopback redirects](https://developers.google.com/identity/protocols/oauth2/native-app)
- [Drive per-file authorization scope](https://developers.google.com/workspace/drive/api/guides/api-specific-auth)
- [Drive v2 file ETag](https://developers.google.com/workspace/drive/api/reference/rest/v2/files)
- [Drive v2 media updates](https://developers.google.com/workspace/drive/api/reference/rest/v2/files/update)
- [Chromium's conditional Drive upload implementation](https://github.com/chromium/chromium/blob/main/google_apis/drive/drive_api_requests.cc)
