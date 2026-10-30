# Aurora Google Play connection

A local MCP connection and CLI for Aurora's Google Play release checks. Runs on
Node.js 24.5 or newer using its built-in HTTPS, RSA signing and JSON support.
No package installation or browser session is required.

The MCP/reader's only Play operation is GET applications/{package}/tracks/{track}/releases.
Authentication exchanges a signed service-account JWT for a short-lived Google
access token. The MCP exposes no publishing edits, uploads, updates, or commits.

A separate manual Actions workflow uses `build-internal.mjs` and
`publish-internal.mjs` for signed AABs and internal-track publishing with its own
keyless publisher identity. See [publishing setup](../../docs/google-play-publishing.md).
Its CLI requires a short-lived access token and a verified build manifest; it
does not fall back to the local reader key.

## Google setup

1. Select a Google Cloud project, or create one for Aurora.
2. Enable **Google Play Android Developer API** (androidpublisher.googleapis.com).
3. Under **IAM & Admin > Service Accounts**, create **aurora-play-reader**.
   Skip the optional Cloud project role and user-access grants.
4. Copy its email address. In **Play Console > Users and permissions > Invite new
   users**, enter that email and select only **Aurora: Reflections**
   (com.auroralights.app). Grant **View app information (read-only)**.
   This is a Play Console permission, not a Cloud IAM role.
5. In Google Cloud, select the account's **Keys** tab, then **Add key > Create new
   key > JSON**. Store the download outside the repository. Give the assistant
   only the path; never paste or upload its contents.

Google no longer requires linking the developer account to a Cloud project.
A service account needs no OAuth consent-screen setup.

For this installation, authentication and read access were verified on October
29, 2026 with project aurora-reflections and service account
aurora-play-reader@aurora-reflections.iam.gserviceaccount.com. The exact API track
identifier confirmed by the live response is **internal**. The older guide's qa
identifier returned 404 for this app; the client never silently switches tracks.

## Connect in the desktop app

The personal MCP server is named **aurora_play**. The credential file path is kept
in personal configuration. The key itself stays in the file outside the repo.

codex-config.example.toml shows the required fields. The bundled Node executable
can be used, or another Node.js 24.5+ installation. --use-system-ca uses the
operating system's trusted certificates; --use-env-proxy honors configured
network proxies. TLS verification remains enabled.

For manual registration: **Settings > MCP servers > Add server > STDIO**, enter the
command, arguments and environment fields, then **Restart**. A host may need its
MCP servers refreshed after configuration changes.

- play_check_connection: verify Google authentication and app read access.
- play_get_releases: read up to 20 non-obsolete track releases.
- play_find_version: find an exact version code and its lifecycle states.

GOOGLE_APPLICATION_CREDENTIALS selects the local service-account JSON. Its default
is ~/.config/aurora-play/reader.json. AURORA_PLAY_PACKAGE defaults to
com.auroralights.app. AURORA_PLAY_TRACK defaults to internal. None of these values
is accepted as an arbitrary filesystem path or URL in tool arguments.

## CLI and tests

Set the credential path in the current terminal's environment, then run from this
directory with the configured Node executable:

~~~powershell
node --use-system-ca --use-env-proxy server.mjs status
node --use-system-ca --use-env-proxy server.mjs releases
node --use-system-ca --use-env-proxy server.mjs verify --version-code 215469461
node --test tests/connection.test.mjs
~~~

Status exits 0 for authenticated read access, or 2 with a diagnostic.
Verify exits 0 if the exact code has a PUBLISHED release, 2 if absent/unpublished,
and 1 for an API or configuration failure. Tests use synthetic responses and a
synthetic RSA key, and include a real MCP subprocess handshake. They do not use the
Google key or contact Google.

## Evidence and limits

Google returns at most 20 non-obsolete releases: release names, active version
codes, and lifecycle states. It does not return publication timestamps, rollout
percentage, release notes, bundle digests, or source revisions. A higher version
code is not proof of a later publication time.

PUBLISHED includes partial rollouts and resumable halted releases. It does not
establish that every tester can download the version. To verify an importer fix,
retain the build source revision and bundle digest beside its version code, then
match that code to the live Google response. Release names are untrusted data.

## GitHub Actions

The manually dispatched **Check Google Play releases** workflow uses short-lived
Google access tokens through GOOGLE_OAUTH_ACCESS_TOKEN. Complete the one-time
[Google Cloud trust setup](../../docs/google-play-github-actions.md), then set
repository variable GOOGLE_PLAY_WIF_PROVIDER. The workflow runs from main and can
optionally verify an exact version code. Its scope is release-status checking.

## Official references

- [Google setup](https://developers.google.com/android-publisher/getting_started)
- [Play permissions](https://support.google.com/googleplay/android-developer/answer/9844686)
- [Service-account OAuth](https://developers.google.com/identity/protocols/oauth2/service-account)
- [Read releases](https://developers.google.com/android-publisher/api-ref/rest/v3/applications.tracks.releases/list)
- [Release states](https://developers.google.com/android-publisher/api-ref/rest/v3/applications.tracks.releases)
- [Google authentication action](https://github.com/google-github-actions/auth)
- [MCP configuration](https://learn.chatgpt.com/docs/extend/mcp)
