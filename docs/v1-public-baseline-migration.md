# Public 1.0 content baseline

The public build preparation branch uses the 2014-rules SRD 5.1 as its bundled
game-content baseline. Original application code stays MIT; SRD content and
other dependencies retain their separate licenses. Imported content remains
supported and keeps its own terms.

## Opening existing characters

This migration changes the application resources. It does not rewrite your
characters, imported content, portraits, accounts or installed app data.

Seventeen spells and eight magic items now use their SRD display names. Their
existing identifiers are retained, so a character saved with an older display
name can still resolve the same definition.

Definitions outside the SRD are no longer included by default. A character
using one of those definitions needs its corresponding imported content. When
content is missing, Aurora reports the unavailable selections. Loading alone
does not rewrite the saved character. Keep the original file and resolve the
missing content before editing and saving a replacement.

The 61 old Forgotten Realms/nonhuman deity definitions are replaced by the
SRD's 67 historical-pantheon entries, each with a new identifier. An old deity
is not silently changed into a different deity with a similar name. Import the
old definition if an existing character requires it.

Original starting-equipment adapters remain available for imported classes and
backgrounds. An adapter does not supply that class or background's rules.

## Export and distribution

Complete SRD passages replace ten truncated spell descriptions. Dense card
descriptions now fit their actual formatted layout, list items receive one
marker each, and long material requirements appear in the readable card body.
Spell cards paint their contents in both export modes; full character sheets
continue to support editable and flattened forms.

Offline attribution and notices are available from **About → Licenses and
notices**. Release downloads must be accompanied by the matching corresponding
source and LGPL rebuild/relink materials.

Windows x64 and Android ARM64 are the stable targets. Windows ARM64 and macOS
remain experimental; Web is deferred. This baseline is included in the accepted
RC5 packages and the published Windows x64 prerelease. See the
[current release status](v1.0-release-status.md) for the remaining stable steps.
