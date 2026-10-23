# v1.0 RC1 source freeze and package validation

Candidate: **1.0.0-rc.1**. Scope: Windows x64 and Android intended stable; Windows ARM64 and macOS experimental; Web deferred.

The candidate is built from a local Git revision containing the accumulated readiness fixes and their tests. A separate, hashed source snapshot includes tracked build inputs, test fixtures, dependency packages, workflows and validation tools. Existing desktop OAuth configuration and Android signing material remain outside the source snapshot and public evidence.

Validation is in progress. This document does not claim CI success, public distribution, Windows signing, a successful final-package upgrade or stable release approval. No release tag or public release is being created by this local validation run.

The existing phone installation will only be updated in place after its signing identity and a higher version code are verified, its accessible files are backed up, and it has no running Aurora process. Existing characters, preferences and accounts must be retained. Tests do not clear application data, uninstall Aurora, alter host drivers or firmware, reset devices, or reproduce the earlier host crashes.

Evidence and private build outputs are collected under `artifacts/v1-rc1-2026-10-22/`.
