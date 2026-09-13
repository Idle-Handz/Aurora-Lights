# Installed content cleanup, 2026-09-13

Archive: `C:\Users\Ralla\Documents\Aurora Content Archive\2026-09-13-duplicate-cleanup`.
The archive is outside the active `custom` directory. All five moved files were
SHA-256 verified against their pre-move hashes. `manifest.json` records original
paths and reasons. The original local index is also backed up.

## Archived files and retained content

| Archived path relative to custom | Retained content |
| --- | --- |
| `supplements/the-book-of-xellarant/creatures.xml` | `the-book-of-xellarant/creatures.xml`, labeled The Book of Beasts |
| `unearthed-arcana/20200204.xml` (0.0.2) | `unearthed-arcana/20200206.xml` (0.0.5) |
| `unearthed-arcana/20170117.xml` (0.0.5) | `unearthed-arcana/20170116.xml` (0.0.6) |
| `the-book-of-xellarant/class-ranger-revised.xml` | Official PHB Ranger and Tasha optional class features |
| `the-book-of-xellarant/ranger-drakewarden.xml` | Official Fizban Drakewarden |

The entire custom Revised Ranger file is archived to avoid leaving a hybrid class
with only some of its copied definitions. Its 66 declarations include 42 IDs not
among the 24 PHB/Tasha collisions. Its custom class and those unique declarations
are therefore inactive and recoverable in the archive, not remapped or deleted.
The old creature copy also contained a separate Source declaration; the current
TBOX source.xml continues to provide Book of Xellarant and Book of Beasts sources.
The older 2017 UA copy had a distinct old Source ID; its seven gameplay element
IDs remain supplied by the newer file. No saved characters were migrated.

Tasha's optional-class-features.xml explicitly describes Deft Explorer as replacing
Natural Explorer, Favored Foe as replacing Favored Enemy, and Primal Awareness as
replacing Primeval Awareness. These optional feature definitions remain separate
from the PHB class. No official Tasha or PHB files were removed.

## Creature update URLs

Live HEAD checks returned:

- `https://raw.githubusercontent.com/Xellarant/the-book-of-xellarant/main/creatures.xml`: 404.
- `https://raw.githubusercontent.com/Xellarant/the-book-of-xellarant/master/the-book-of-xellarant/creatures.xml`: 200.

The local repository uses master, and commit
`3e208c56d3ead35a27cb1dec35be0f630145e128` (2026-04-27, "Add Book of Beasts creature
source") added the nested file with the Beasts label and current update URL.
No root creatures.xml history appeared in the available local refs. The stale
installed copy contains an invalid branch/path URL, not an interchangeable URL
for the same current file. The exact origin of that obsolete installed copy was
not established.

## Remaining collisions

Read-only re-scan excluding user/local: **12 duplicate-ID groups**, all with the
same source; **5 structurally identical**, **7 different**. All active XML parsed.
See [the full remaining inventory](duplicate-cleanup-after.json) for IDs and paths.

Different definitions:

- Staff of Flowers: DMG 2024 file versus Xanathar file.
- Adamantine Ammunition / Walloping Ammunition: same ID in the DMG file.
- Musketball (20) / Musketball: same ID in Arcane Artillery.
- Defender of Kin / Slayer of Foes: same ID in Devout Fighter.
- Cloudstep / Heartening Breath: same ID in Tatsumi.
- Cimbalom of Dazd / Kaba Gaida of Nevihta: same ID in Forgotten Secrets.
- Ancient Heart (Strength +2) / Ancient Heart (Strength +3): same ID in Forgotten Secrets.

Structurally identical repeats:

- Position of Privilege.
- Supreme Sneak: Stealth Attack Feature Replacement.
- Aspect of Ashura.
- Safe Haven.
- Crown of Disgust.

These mixed-file cases were left intact for review; deleting an entire file would
remove unrelated content. A differing pair with different names is not automatically
two revisions of one element and should not be fixed by choosing one to discard.

## Publication and runtime status

- Removed the two archived Ranger file entries from the local TBOX index only.
- The upstream/repository index is unchanged and still lists them. Refreshing that
  index from upstream can reintroduce the archived files; the local removal is not
  a permanent subscription exclusion.
- No repository content was committed or published.
- SQLite was not rebuilt, and no application reload was performed. The remaining
  counts describe installed XML, not the existing SQLite snapshot. Runtime review
  needs a refresh/reload after the user reviews the archived content choices.
- user/local was neither edited nor archived.
