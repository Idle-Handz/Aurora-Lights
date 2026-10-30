# Manual Google Play internal publishing

**Publish Google Play internal release** builds an ARM64 signed AAB, verifies the
upload certificate, package/version and 16 KB bundle alignment, and optionally
submits it to `com.auroralights.app` on **internal**. Google authentication uses
GitHub OIDC; no publisher JSON key is needed. The reader workflow/MCP stay read-only.

Run the workflow from **main**. The separate `source_ref` input defaults to
`feature_public-v1.0-build-preparation`, the source branch of the accepted internal
build and importer fixes. Main still has the older `Aurora.App` project. Use the
release-preparation branch until `Aurora.Reflections` and its licensing/source
tools are merged into main. Select only trusted source: building executes its
MSBuild and packaging code with signing secrets. The resolved commit is recorded.

## 1. Google Cloud and Play permissions

Select Cloud project **aurora-reflections**, number **420856776647**. Under
**IAM & Admin > Service Accounts**, create **aurora-play-publisher**:

```text
aurora-play-publisher@aurora-reflections.iam.gserviceaccount.com
```

Skip Cloud project role grants and key creation. The IAM, IAM Service Account
Credentials, Security Token Service and Google Play Android Developer APIs were
already needed for the reader; keep them enabled.

In **Play Console > Users and permissions > Invite new users**, enter that email.
Add only Aurora under app permissions and grant:

- **View app information (read-only)**
- **Release apps to testing tracks**

Save/invite it; the service account does not accept an email invitation. Allow a
few minutes for propagation. Keep the existing reader permissions unchanged.

## 2. Separate publishing identity pool

In **IAM & Admin > Workload Identity Federation > Create pool**, use:

| Field | Value |
| --- | --- |
| Pool name | Aurora Play publishing |
| Pool ID | `aurora-play-publish` |
| Provider type | OpenID Connect (OIDC) |
| Provider name | GitHub main publisher |
| Provider ID | `github-main` |
| Issuer URL | `https://token.actions.githubusercontent.com` |
| Audiences | Default audience |

Add these attribute mappings:

| Google attribute | Expression |
| --- | --- |
| `google.subject` | `assertion.sub` |
| `attribute.repository_id` | `assertion.repository_id` |
| `attribute.repository_owner_id` | `assertion.repository_owner_id` |
| `attribute.ref` | `assertion.ref` |
| `attribute.workflow_ref` | `assertion.workflow_ref` |
| `attribute.event_name` | `assertion.event_name` |
| `attribute.environment` | `assertion.environment` |

Set the attribute condition:

```text
assertion.repository_id == '1184656614' &&
assertion.repository_owner_id == '273737674' &&
assertion.ref == 'refs/heads/main' &&
assertion.workflow_ref == 'Idle-Handz/Aurora-Lights/.github/workflows/play-store-publish.yml@refs/heads/main' &&
assertion.event_name == 'workflow_dispatch' &&
assertion.environment == 'google-play-internal'
```

Save. Use this dedicated pool solely for the publisher. Under **Grant access**:

1. Select **Grant access using Service Account impersonation**.
2. Select `aurora-play-publisher@aurora-reflections.iam.gserviceaccount.com`.
3. Choose **Only identities matching the filter**.
4. Attribute name: `repository_id`; value: `1184656614`.
5. Save. This assigns **Workload Identity User** on that service account.
6. Dismiss **Configure your application**; no OIDC token path or downloaded
   credential configuration is needed for GitHub Actions.

Equivalent IAM member:

```text
principalSet://iam.googleapis.com/projects/420856776647/locations/global/workloadIdentityPools/aurora-play-publish/attribute.repository_id/1184656614
```

Full provider resource name:

```text
projects/420856776647/locations/global/workloadIdentityPools/aurora-play-publish/providers/github-main
```

Service Account Token Creator and domain-wide delegation are unnecessary. The
condition admits the publishing workflow on main in the named GitHub environment;
it does not match the reader or depend on the app source branch.

## 3. GitHub environment and existing upload key

In **Idle-Handz/Aurora-Lights > Settings > Environments**, create
**google-play-internal**. Under deployment branches select **Selected branches and
tags**, and allow branch **main** only. Add a required reviewer if desired and
supported by the repository's GitHub plan.

Add these **environment variables** (public metadata):

| Name | Value |
| --- | --- |
| `GOOGLE_PLAY_PUBLISH_WIF_PROVIDER` | `projects/420856776647/locations/global/workloadIdentityPools/aurora-play-publish/providers/github-main` |
| `GOOGLE_PLAY_UPLOAD_CERT_SHA256` | `3af0620d0be0e10caba177d0acdc3ac9ddc4b29fe4dff85e56706b363017d66c` |

That fingerprint is the upload certificate of the accepted `1.0.0-internal.2`
AAB. Confirm it in **Play Console > Test and release > Setup > App signing**
(also accessible through **App integrity**) under **Upload key certificate**,
SHA-256. Use the upload certificate, distinct from Google's app signing
certificate. Colons and whitespace are accepted. After an intentional Play
upload-key reset, update the fingerprint and secrets together.

Add these **environment secrets**:

| Name | Contents |
| --- | --- |
| `ANDROID_UPLOAD_KEYSTORE_BASE64` | Base64 of the existing keystore used for the accepted Play AAB |
| `ANDROID_UPLOAD_KEY_ALIAS` | Its upload-key alias |
| `ANDROID_UPLOAD_KEY_PASSWORD` | Its key password |
| `ANDROID_UPLOAD_STORE_PASSWORD` | Its keystore password |

Reuse the upload key; do not generate a replacement. The older APK workflow's
`ANDROID_KEYSTORE_BASE64` is not assumed to match Play. If it is the same key,
copy its original Base64 file into the new secret; GitHub cannot reveal an
existing secret value. Do not paste keystores or passwords into chat.

To prepare a local Base64 file in PowerShell (adjust the keystore path if needed):

```powershell
$uploadKeyPath = 'C:\Users\Ralla\source\repos\Aurora-Lights\aurora-release.jks'
$encodedKeyPath = Join-Path $env:TEMP 'aurora-upload-key.b64'
[IO.File]::WriteAllText($encodedKeyPath, [Convert]::ToBase64String([IO.File]::ReadAllBytes($uploadKeyPath)))
```

Copy the file's contents into the secret field, then delete that temporary file.
The workflow refuses a mismatched certificate. Passwords use temporary files
rather than command-line values. Signing files are cleaned up after building and
excluded from artifacts.

## 4. First hosted run

After merging the workflow into main, open **Actions > Publish Google Play
internal release > Run workflow**:

1. Workflow branch: **main**.
2. Source ref: **feature_public-v1.0-build-preparation**, or a reviewed exact commit
   from it. Switch to main after the current app changes are merged there.
3. Version name: **1.0.0-internal.3**, or the next intended display version.
4. Operation: **build-only** for the first run.
5. Optional notes: English (US), at most 500 characters.

The signed AAB, matching source ZIP and build manifest appear in artifact
**google-play-internal-signed-bundle**. Version codes follow the repository's
seconds-since-2020 sequence. The manifest records source/workflow commits,
versions, upload certificate and bundle/source hashes. Build-only does not
authenticate to Play or submit a release.

When it passes, run again with **publish-internal**. This creates a fresh build
and version code. Authentication occurs after building and artifact preservation.
The uploader checks Google's returned version/digest, updates only internal,
validates and commits. It fails rather than canceling any existing changes in
review. Publishing runs are serialized.

Check the summary and **google-play-internal-publish-report**:

- `published`: the exact version is visible as published in Google's API.
- `submitted`: Google accepted the commit; processing/review or a delayed status
  read may remain. Check Console or run **Check Google Play releases** with the
  generated version code. Do not upload that same code again.
- `commit-unconfirmed`: the final response was lost and availability could not
  be established. Check that exact code in Play before retrying. Commit requests
  are never automatically repeated after a network/server failure.
- `failed-before-commit`: submission was not accepted. The tool attempts to
  discard its uncommitted edit; inspect the failed step and any cleanup warning.

Play may require app declarations, terms, review, or resolution of another draft.
Complete those in Console; the workflow does not bypass them. Hosted build success
does not establish device/runtime acceptance. Keep matching source accessible to
testers when distributing the binary; signed-build artifacts expire in 30 days.

## References

- [Google identity setup](https://docs.cloud.google.com/iam/docs/workload-identity-federation-with-deployment-pipelines)
- [Play app permissions](https://support.google.com/googleplay/android-developer/answer/9844686)
- [MAUI signed Android publishing](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-cli?view=net-maui-10.0)
- [Bundle upload API](https://developers.google.com/android-publisher/api-ref/rest/v3/edits.bundles/upload)
- [Commit behavior during review](https://developers.google.com/android-publisher/api-ref/rest/v3/edits/commit)
- [Read-only connection setup](google-play-github-actions.md)
