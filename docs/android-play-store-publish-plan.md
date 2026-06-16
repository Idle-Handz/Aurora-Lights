# Android Play Store Publish Plan

This is a parking-lot plan for publishing Aurora: Reflections on Google Play.

## Goal

Get the Android build accepted into Google Play and make it available through an appropriate testing or production track.

## Main Work Items

1. Confirm permanent app identity.
   - Current package id: `com.auroralights.app`
   - Package ids are effectively permanent after first Play upload.

2. Produce a signed Android App Bundle.
   - Google Play expects `.aab` uploads for new apps.
   - Keep the existing APK path for direct sideload/testing releases if still useful.

3. Set up Play App Signing.
   - Create or confirm the upload keystore.
   - Preserve the upload key securely.
   - Ensure future uploads are signed with the same upload key.

4. Verify Android target and store compatibility.
   - New Play apps and updates currently need to target Android 15 / API 35 or higher.
   - Confirm the produced bundle targets the required API level.
   - Confirm the bundle is not accidentally emulator-only or x64-only.
   - Verify supported devices through Play Console or bundle tooling.

5. Prepare Play Console listing.
   - App name
   - Short description
   - Full description
   - Screenshots
   - App icon and feature graphic
   - Contact email
   - Privacy policy URL

6. Complete app review declarations.
   - Data Safety form
   - Content rating questionnaire
   - Ads declaration
   - Target audience
   - Permissions declarations if flagged
   - Sign-in instructions if the app ever requires gated access

7. Check content and policy posture.
   - Avoid copyrighted third-party art/text in store listing assets.
   - Make sure Android does not self-update executable code outside Google Play.
   - Content/database downloads should stay clearly separate from app-code updates.

8. Run staged release flow.
   - Upload to internal testing first.
   - Verify install, signing continuity, content refresh, storage behavior, and app startup.
   - If required by the Play account, complete closed testing before production access.
   - Promote to production or staged rollout when ready.

## Account Caveat

Personal Google Play developer accounts created after November 13, 2023 may need a closed test with at least 12 opted-in testers for 14 continuous days before production access.

## Useful References

- Google Play app setup: https://support.google.com/googleplay/android-developer/answer/9859152
- Target API requirements: https://developer.android.com/google/play/requirements/target-sdk
- Android app signing: https://developer.android.com/studio/publish/app-signing
- Prepare app for review: https://support.google.com/googleplay/android-developer/answer/9859455
- Testing requirements: https://support.google.com/googleplay/android-developer/answer/14151465
- Prepare and roll out a release: https://support.google.com/googleplay/android-developer/answer/9859348
