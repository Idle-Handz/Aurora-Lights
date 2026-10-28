# Windows code signing

Updated October 28, 2026. The maintainer has submitted the application to SignPath
Foundation's free open-source program for Aurora Lights. Approval and workflow integration
are pending. The public RC5 Windows x64 packages are unsigned, and stable package
preparation will resume after the signing route is approved.

## SignPath application details

| Field | Prepared answer |
| --- | --- |
| Project name | Aurora Lights |
| Category | Gaming |
| Repository | https://github.com/Idle-Handz/Aurora-Lights |
| Homepage | https://aurora.idlehandz.com/ |
| Download page | https://github.com/Idle-Handz/Aurora-Lights/releases |
| Privacy policy | https://aurora.idlehandz.com/privacy/ |
| Build system | GitHub Actions; signing integration is pending. |

Suggested tagline:

Open-source D&D 5e character creation and management for desktop and mobile.

Suggested description:

Aurora Lights is an open-source suite for creating and managing Dungeons &
Dragons fifth-edition characters. It brings together Aurora Reflections, the
modern desktop and mobile client, and Aurora Legacy, the maintained Windows
client. The project helps players build characters, manage their equipment and
abilities, track play sessions, and produce printable character sheets while
preserving compatibility with existing Aurora character files and custom content.

The [application form](https://signpath.org/apply.html) requests a short paragraph
about purpose rather than version-specific features or dependencies. Use public
project activity and release/download evidence for the separate reputation field;
do not attribute the original Aurora project's entire audience to this fork.

The application concerns Windows signing. Android retains its existing signing
identity. The released Windows RC5 artifacts were prepared from a verified local
snapshot and separately passed hosted CI; they must not be described as artifacts
produced by a SignPath-connected hosted pipeline.

## Licensing information for the application

Aurora's original code is MIT licensed. The PDF engine is
iTextSharp.LGPLv2.Core 3.8.6 under LGPL-2.0-only, with matching library source and
relink instructions. Other dependencies retain their own licenses. The bundle
also includes CC-BY-4.0 SRD content, separately licensed fonts, and legacy assets
covered by redistribution permission. See the [component and asset inventory](../third-party/README.md)
and [bundled-content boundary](../third-party/CONTENT-SCOPE.md).

SignPath's [published conditions](https://signpath.org/terms) require OSI-approved
open-source licensing for all components and exclude proprietary components.
Disclose the asset/content terms and ask SignPath to confirm this packaging is
eligible. Redistribution clearance alone does not establish program eligibility.

## Code signing policy

SignPath is the proposed provider; no Foundation signing service is active for
Aurora yet. Before the first signed release, publish the approved policy on the
homepage and download pages. Name the authors, reviewers, and signing approvers,
confirm their MFA, and link the privacy policy. Each signing request requires
manual approval. Foundation certificates identify **SignPath Foundation** as
publisher. Once approved, add the required attribution, "Free code signing
provided by SignPath.io, certificate by SignPath Foundation," with links to the
providers. These requirements come from the [Foundation conditions](https://signpath.org/terms).

Aurora's [privacy policy](https://aurora.idlehandz.com/privacy/) covers optional
Google Drive saves and account controls. The program also contacts release and
content services; a blanket statement that it never transfers information would
be inaccurate.

## Integration after approval

Use SignPath's [GitHub integration](https://docs.signpath.io/trusted-build-systems/github)
to build on GitHub-hosted runners, upload the unsigned artifact, and submit that
artifact for origin verification and signing. Configure the organization,
project, policy, approved artifact configuration, and secret token after access
is granted. Keep the token out of source, logs, and release archives.

Agree the file selection with SignPath before enabling it. Aurora-owned binaries
need consistent product/version metadata. Upstream DLLs and Velopack-generated
launchers/updaters need their own classification; including them does not grant
permission to sign them as Aurora's code. Preserve existing upstream signatures.

Signing only the outer installer would leave the portable and update payloads
outside that coverage. The integration must support the pinned Velopack packaging
flow, sign the approved application files before final payload generation, and
sign the installer at the appropriate packaging stage. Generate feed hashes and
checksums from the final signed artifacts. Changing signatures changes bytes;
the existing RC5 packages must remain untouched.

Before stable publication, verify signatures and source correspondence and run
the focused final package checks in the [release status](v1.0-release-status.md).
