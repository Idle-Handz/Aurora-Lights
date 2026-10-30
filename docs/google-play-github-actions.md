# GitHub Actions access to Google Play

The **Check Google Play releases** workflow reads Aurora's release status through
the existing connection. It is manually dispatched from main. Optionally supply
a version code and the workflow fails unless that exact code has a PUBLISHED
release. The report preserves Google's lifecycle states and availability limits.

Google setup is a one-time owner action. The workflow uses short-lived OAuth
access tokens through Workload Identity Federation and the existing Play reader.
No service account JSON key is uploaded to GitHub.

## Confirmed values

| Field | Value |
| --- | --- |
| Google Cloud project ID | aurora-reflections |
| Reader account | aurora-play-reader@aurora-reflections.iam.gserviceaccount.com |
| Play package | com.auroralights.app |
| GitHub repository | Idle-Handz/Aurora-Lights |
| Numeric repository ID | 1184656614 |
| Numeric owner ID | 273737674 |
| Approved ref | refs/heads/main |
| Workflow | .github/workflows/play-store-status.yml |
| Identity pool ID to create | aurora-play |
| Identity provider ID to create | github-main |

The numeric GitHub IDs and default branch were verified against GitHub's API on
October 29, 2026. The Google project **number** is a different value from its ID;
Google will include that number in the provider resource name.

## 1. Enable Google APIs

Select **aurora-reflections** in Google Cloud. Under **APIs & Services > Library**,
enable these APIs if needed:

- Identity and Access Management (IAM) API: iam.googleapis.com
- IAM Service Account Credentials API: iamcredentials.googleapis.com
- Security Token Service API: sts.googleapis.com
- Cloud Resource Manager API: cloudresourcemanager.googleapis.com

Google Play Android Developer API is already enabled for the verified connection.
The reader's existing app-level **View app information (read-only)** permission in
Play Console is sufficient; no additional Play permissions are required.

## 2. Create the dedicated identity pool and provider

Open **IAM & Admin > Workload Identity Federation** and choose **Create pool**.

- Pool name: Aurora Play checks
- Pool ID: aurora-play
- Pool state: Enabled

Continue to provider setup:

- Provider type: OpenID Connect (OIDC)
- Provider name: GitHub main
- Provider ID: github-main
- Issuer URL: https://token.actions.githubusercontent.com
- Audiences: Default audience

Use this dedicated pool for this connection. The IAM grant below is scoped to
the pool, so another provider should not be added without reviewing its trust rule.

## 3. Map the GitHub claims and restrict access

Add these mappings. The left column is the Google attribute; the right column
is its value/expression.

| Google attribute | Expression |
| --- | --- |
| google.subject | assertion.sub |
| attribute.repository_id | assertion.repository_id |
| attribute.repository_owner_id | assertion.repository_owner_id |
| attribute.ref | assertion.ref |
| attribute.workflow_ref | assertion.workflow_ref |
| attribute.event_name | assertion.event_name |

Set the **Attribute condition** to:

~~~text
assertion.repository_id == '1184656614' &&
assertion.repository_owner_id == '273737674' &&
assertion.ref == 'refs/heads/main' &&
assertion.workflow_ref == 'Idle-Handz/Aurora-Lights/.github/workflows/play-store-status.yml@refs/heads/main' &&
assertion.event_name == 'workflow_dispatch'
~~~

Save the pool/provider. This admits only manual runs of this workflow from main
in the verified repository. Tags, pull requests, forks, other workflows and other
branches do not match this rule.

## 4. Allow the GitHub identity to use the reader

In the pool, choose **Grant access**:

1. Choose **Grant access using Service Account impersonation**.
2. Select aurora-play-reader@aurora-reflections.iam.gserviceaccount.com.
3. Choose **Only identities matching the filter**.
4. Attribute name: repository_id.
5. Attribute value: 1184656614.
6. Save.

This assigns **Workload Identity User** (roles/iam.workloadIdentityUser) on that
service account. The equivalent principal is:

~~~text
principalSet://iam.googleapis.com/projects/PROJECT_NUMBER/locations/global/workloadIdentityPools/aurora-play/attribute.repository_id/1184656614
~~~

PROJECT_NUMBER is the number of the Cloud project containing the pool. Do not use
aurora-reflections or the service account client ID in its place. Domain-wide
delegation and Service Account Token Creator are not needed for this workflow.

## 5. Supply the provider resource name

Open the provider details and copy its full resource name:

~~~text
projects/PROJECT_NUMBER/locations/global/workloadIdentityPools/aurora-play/providers/github-main
~~~

Send this value to the assistant. It is configuration metadata, not a secret.
The assistant can set repository Actions variable **GOOGLE_PLAY_WIF_PROVIDER**.
Alternatively, add it in GitHub under **Settings > Secrets and variables >
Actions > Variables**. A secret or JSON credential is not needed.

Allow a few minutes for the new pool and IAM binding to propagate.

## 6. First hosted run

The workflow and tools/play-console source must be committed and merged into main
before the manual workflow is available. The Cloud trust rule requires main.

In GitHub, open **Actions > Check Google Play releases > Run workflow**:

- Branch: main
- Track: internal
- Expected version code: 215469461 for the currently verified internal release,
  or leave this empty to inspect the current track without asserting a version.

The job runs focused offline connection tests, authenticates with Google, reads
the releases and writes a job summary plus a JSON artifact. The access token is
passed only to the read step. Artifact upload is limited to that JSON report.

PUBLISHED can include partial and resumable halted rollouts. The API does not
provide publication times, release notes, artifact hashes or source commits.
Keep the build's source/digest record when verifying which fixes a version contains.

## Later publishing

For signed AAB builds and manual internal-track uploads, use the separate
[publishing workflow and setup guide](google-play-publishing.md). It uses its own
publisher identity and GitHub environment; this check stays read-only.

## References

- [Google setup and claim mappings](https://docs.cloud.google.com/iam/docs/workload-identity-federation-with-deployment-pipelines)
- [Google authentication action](https://github.com/google-github-actions/auth)
- [GitHub OIDC claim definitions](https://docs.github.com/en/actions/reference/security/oidc)
- [Release-check implementation](../tools/play-console/README.md)
