AURORA LIGHTS CONTENT CORRECTIONS - 2026-10-06

This bundle contains the reviewed local repairs collected so far: 11 source
files with 46 correction records. Some records repair more than one reference.
It includes the ten reference fixes and the seven source-integrity findings,
plus the earlier equipment-pack and Tatsumi repairs. See CORRECTIONS.txt and
manifest.json for the full inventory, source paths, fingerprints and reasons.

Compatibility
-------------
These are managed Aurora Lights overrides using correction-metadata version 1.
They were validated with Aurora.Content 0.10.1. Use an Aurora Lights build that
supports that correction format. Other Aurora programs have not been tested.
Keep the matching original source collections installed. Overrides are not a
standalone content database and do not include the referenced spell libraries.

Two source layouts are supplied:
  direct:    core/, reddit/, third-party/, ryokos-guide-to-the-yokai-realms/
  aggregate: aurora-sources/AuroraLegacy/core/,
             aurora-sources/reddit/, aurora-sources/aurorabuilder-third-party/
The installer selects the installed layout. Do not copy both variants into an
active content folder. Tatsumi has only a direct variant; it is skipped when
that source is absent. The aggregate layout has 10 repairs / 44 records.

Installation on a computer
--------------------------
1. Extract the whole ZIP into a folder outside Aurora's custom content folder.
2. Close Aurora while installing, and keep a backup of custom/user/local.
3. Open a terminal in the extracted folder. Python 3.9 or newer is required;
   no additional Python packages or network access are used.
4. Preview the plan (replace the example path with your actual custom folder):

     py -3 .\install.py "C:\path\to\custom"

   On a system where Python is named python3, use:

     python3 ./install.py "/path/to/custom"

5. Review the output, then run the same command with --apply:

     py -3 .\install.py "C:\path\to\custom" --apply

6. Reopen Aurora and refresh/rebuild its content database to activate repairs.

The default command is read-only. The installer checks exact source and
template SHA-256 hashes before writing. A changed source version, ambiguous
layout, or invalid template stops installation. Missing sources are skipped.
Refresh source content and review/rebase a repair if its source hash differs;
do not bypass the check by editing the manifest.

Existing managed overrides are preserved. Identical overrides are reported
as ALREADY MANAGED. A different override for the same source is skipped with
its path and a manual-merge message; no second override is installed. That
message can reflect metadata/notes differences as well as content differences.
Review it rather than copying another file over the existing override.

New files go under custom/user/local/aurora-corrections-20261006/. No upstream
files, databases, characters, settings, or content indexes are changed.
Atomic publication requires a filesystem that supports hard links (for example
NTFS or ext4). Filesystems without that support fail safely; FAT/exFAT and
Android emulated storage are not supported for direct installer writes.

Android installation
--------------------
Use a complete copy of your phone's custom content on a computer filesystem
that supports hard links. Run the preview/install steps on that copy. Transfer
only the newly installed user/local/aurora-corrections-20261006 folder back to
the same relative location in the phone's custom folder. Keep original sources
and existing local overrides unchanged; if the installer reported an existing
override, review that file separately. Rebuild the database in the phone app.
The matching source files must not change between copying and installation.
This ZIP does not run ADB or choose a device automatically.

Optional Farmer preference
--------------------------
Farmer's feat-selection presentation is an intentional personal preference,
not a source error. It is excluded from the default installation. To include
its two correction records, add --include-personal-preferences to BOTH your
preview and --apply commands. It preserves the canonical Farmer identity and
presents the Tough feat through a background feature wrapper.

What 'review-pending' means
--------------------------
The repair records deliberately remain review-pending: the corrections are
active local overrides, not a claim that their authors accepted them upstream.
After rebuilding, the seven source-integrity manual-review findings should
clear on the validated sources. A local-override review/pinned status may
remain because the original upstream source still needs that correction.
Do not choose Accept Upstream merely to dismiss that status; it can retire a
correction. Review upstream changes before accepting or retiring an override.

Contents and limits
-------------------
The retired Staff of Flowers override is excluded because it was previously
retired after the upstream fix. Unrelated homebrew files are excluded.
The bundle does not repair every content warning. The independent local
Bender Improved Extra Attack -> Bender Extra Attack replacement reference
remains unresolved; no unreviewed replacement is included for it.

VALIDATION.txt records the checked outcomes. SHA256SUMS.txt covers every other
payload file; the separate ZIP checksum verifies the archive itself.
The XML templates retain source metadata and embedded baselines so the app can
detect later upstream changes. CORRECTIONS.txt is a readable catalog and
manifest.json is the machine-readable installation plan.

Rollback
--------
Move only files newly installed by this bundle out of custom/user/local into
a backup folder OUTSIDE custom, then rebuild the content database. Never
remove an existing override that the installer reported as already managed
or skipped. The installer does not replace existing files.
