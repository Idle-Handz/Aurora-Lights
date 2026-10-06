# Manual UI pass — phases 4 to 6

This lists the remaining integrated UI, character workflow, and installed-app
review for phases 4 to 6. Many underlying rules already have automated coverage;
the checklist is not a claim that every remaining item requires a person. Data
round trips, PDF values, and installed-runtime smoke checks can also be automated.
Visual presentation and interaction still need review in the rendered app. It
covers the shared library, source restrictions, Builder.Data cleanup, the 0.7.0
conflict and correction policy, and the rebuilt sources editor.

**Aurora.Content 0.10.1, data version 18 (schema 1), preparation contract 2.**
Existing data-17 databases need an XML refresh before content loads. The refresh
recomposes append effects using Legacy ordering and description behavior; changing
version metadata alone is insufficient. If an unreadable append supplier prevents
safe recomposition, the refresh preserves the previous database and reports what
needs repair. Check 1 therefore needs an older-format database and cannot be
repeated on the same database after it has been refreshed.

The library now follows legacy declaration order for readable same-ID collisions
and records superseded declarations separately from active mechanics. Explicit
content-authored and curated aliases forward absent old IDs to live definitions;
the host resolves those aliases when restoring saved references. They do not
merge unrelated definitions merely because their names match.

Earlier shared-library work reached `main` (fast-forwarded through `6b2f7e8` on
2026-09-24). That handoff records the manual UI pass as cleared; retain this
guide for future regression reviews. The 0.9.0 uptake reached `main` in
`a066867`. Its automated
acceptance evidence is in `translator-library-split-plan.md`; it does not claim
a new manual UI pass.

Record each check as pass or fail with a note. A failure is worth more than a
completed list — stop and write down what you saw rather than working around it.

## Before you start

**This pass touches your real content, characters and settings.** Two checks
change data on purpose: the one-time migration rewrites app settings, and
restricting a source rewrites the character you test it on.

Take these copies first:

| What | Where | Why |
| --- | --- | --- |
| Characters | `%USERPROFILE%\Documents\5e Character Builder\*.dnd5e` | Check 5 clears picks and saves the file |
| Content database | `...\5e Character Builder\custom\aurora-elements.sqlite` | Needed to repeat check 1 |
| App settings | `%APPDATA%\AuroraLights\settings.json` | The migration edits it, once |

```bash
robocopy "%USERPROFILE%\Documents\5e Character Builder" "%USERPROFILE%\Documents\5e Character Builder backup" /E
```

**Check 1 has already happened on this machine and cannot be repeated.** The
migration reads the switched-off packages recorded in the database, and it ran
before the first refresh: `settings.json` has `"SourcePreferencesSeeded": true`.
Restoring an old database no longer brings it back either, because a data-12
database is not readable by the current data-18 reader — it has to be refreshed first,
and the refresh is what retires those flags. Check 1 is kept below as a record of
what it was meant to show, and its lasting half (what the Sources panel holds) is
still worth reading.

Build and run:

```bash
dotnet build Aurora.App/Aurora.App.csproj -f net10.0-windows10.0.19041.0
```

## 1. First launch after the upgrade

The old per-package switches become source restrictions, once. Aurora Legacy
Essentials was also switched off but is builder infrastructure, so it must stay
enabled.

This has already run here, so read it rather than perform it.
`DefaultSourceRestrictions` in `settings.json` now lists **20 sources** — Ryoko's
Guide, D&D Wiki, the Reddit and Kibbles material, the Book of Xellarant and the
rest — which is the migration's result plus whatever has been switched off in
Settings since. The original note that this machine would migrate exactly one
source was written before that.

1. Launch the app and let content finish loading.
2. Go to **Settings → Content → Sources** (the first sub-tab).

Expect:
- A **Default source restrictions** section — the same tree as a character's
  Sources tab. The old package list with its search box is gone.
- **Ryoko's Guide to the Yokai Realms** among the sources switched off there.
- There is no apply-defaults toggle: new characters and saved characters whose
  files have no `<sources>` node use these defaults. Empty defaults mean unrestricted.
- **Required builder sources** shows Aurora Legacy Essentials and the
  Internal/Core infrastructure labels present in the loaded catalog (including
  Aurora Essentials when present). Every row is checked, labeled **Always
  enabled**, and cannot be toggled; the group's toggle is also disabled. Old
  restrictions and broad category toggles cannot switch these off.
  Rulebook sources such as the PHB, DMG and Monster Manual remain selectable,
  including when they are grouped under Core. Switching a whole category off
  cannot switch these off either.
- Everything else you had switched on is still on.

Then confirm it only happens once: close the app, launch again, and check the
Sources panel is unchanged.

**Also confirm the panel lists anything at all.** A character's Manage → Sources
was empty for a whole session because the engine's source list is a snapshot
taken when the character manager is first touched, which happened before content
finished loading. It is rebuilt after every load now, and the console says
`sources: listing N source(s) from M element(s)` on each one. N of 0 means the
rebuild is not reaching that path.

Why it matters: this is the only moment the old preferences can be read. If it
does not happen, a source you had switched off silently comes back for every new
character.

## 2. Content refresh, in-process

The 0.7.0 first-import, blocked-refresh and repair/restart behavior has passed
[automated lifecycle rehearsals](content-conflict-policy-2026-09-24.md#automated-lifecycle-rehearsal--september-24-follow-up)
in both skip modes. Those functional checks no longer need manual repetition.
The following UI presentation and interaction checks remain separate.

Refresh no longer runs a bundled Translator executable; the shared library does
the import inside the app.

1. **Settings → Content → Data**, press **Refresh Database**.
2. Watch the progress bar through the run.

Expect:
- Progress distinguishes scanning, reading, comparing, and writing. Writing
  shows completed/total element counts and the current file, even when the
  rounded percentage has not changed yet. An activity spinner remains animated
  between callbacks; percentages describe work phases, not estimated time.
- Resolving relationships and validating/activating use an animated bar because
  they do not report a measurable total. Loading the refreshed content also
  keeps the animated indicator until completion.
- No console window appears at any point.
- It finishes with "Database refreshed."
- Afterwards the app still lists your content: open **Compendium** and search for
  something from a third-party book.

Cancellation is a service-level check; Settings currently has no Cancel control.
There is no manual cancellation step in this screen. Do not close or kill the app
as a substitute for cooperative cancellation.

Why it matters: on Windows this replaces a child process. It is also the first
time refresh works at all in a release build, on Mac, or on Android, because
those never shipped the executable.

## 3. Default restrictions editor

The editor in Settings edits **defaults for new characters**. It must never
change the character you have open.

1. Open a character, note a source it uses on its **Manage → Sources** tab.
2. In **Settings → Content → Sources** (now the first sub-tab), work the tree:
   expand a category, expand a publisher under it, and switch things off and on at
   each level — a single book, a publisher, a whole category. Check that a
   half-switched category shows the dash rather than a tick, and that typing in the
   filter box opens only what it matches.
3. Go back to the character's **Manage → Sources**.

Expect:
- The character's own restrictions are untouched by anything you did in Settings.
- The infrastructure sources stay locked.
- Close and reopen the app: your default selections are still there.

## 4. New character follows the defaults

A character that has not chosen its own restrictions follows the defaults. There
is no toggle for this any more: to have new characters start unrestricted, clear
the defaults in Settings.

1. With some sources switched off in Settings, create a new character.
2. Open its **Manage → Sources**.

Expect: the sources you switched off in Settings are off for this character, and
content from them is absent from its choice lists.

Then clear the defaults in Settings, create another new character, and confirm it
starts with everything enabled.

3. Load a character that restricts a book, then create a new character without
   restarting the app.

Expect: the new character shows the defaults, not the restrictions of the one you
just had open.

## 4b. A content file that cannot be imported

This needs a throwaway file, not your real content. Put it in the built-in custom
directory named in **Settings → Content** and delete it when you are done.

1. Create `zz-broken.xml` there containing `<elements xmlns="http://example.com">
   </elements>`.
2. **Settings → Content → Refresh Database** with **Skip content files that
   can't be imported** on (the default).

Expect: the refresh finishes, its summary says one file was skipped and needs
attention, and the file is listed underneath with what is wrong with it. Your
other content is present as usual.

3. Close and reopen the app, and look at **Settings → Content → Data** again.

Expect: the file is still listed. The list comes from the database, not from the
last refresh.

4. Delete the file and refresh again.

Expect: the list is empty.

5. Switch the setting off, put the file back, and refresh.

Expect: the refresh fails with an error naming the file, and the database you had
is untouched — check that characters still load.

## 5. Restricting a source on an existing character

This is the behavior you asked for: a restricted source is not merely hidden, its
content leaves the character and the choices it filled come back as choices to
make again.

Use a character you do not mind changing — a copy is fine. On this machine,
**Michelle Character 2** is a good subject: it has a chosen Ryoko option
(`RGTTYR Character`) as well as Ryoko weapon proficiencies.

1. Open it and note what it has from Ryoko's Guide on the **Build** page.
2. **Manage → Sources**, switch **Ryoko's Guide to the Yokai Realms** off.

Expect:
- A message naming how many choices need picking again.
- The **Build** page shows those choices as unresolved, highlighted the same way
  any unmade choice is.
- The granted Ryoko weapon proficiencies are gone from the character, not merely
  hidden from lists.
- The character file on disk records the restriction, and the cleared picks are
  saved rather than sitting only in memory.

Now switch Ryoko back on. Expect:
- The granted proficiencies return by themselves.
- The cleared picks stay empty and still need picking. That is intended: a grant
  is automatic, a choice is yours to make again.

Repeat once with **Load Default** on the Manage tab instead of a single toggle;
it should behave the same way.

A second worthwhile subject is any character that uses D&D Wiki, Ancient World or
DM's Guild weapons: restricting those books should remove Scythe, Katana, Chakram
or Lasso proficiencies that those books hand out through ordinary weapon
proficiency grants.

## 6. Compendium

Deliberately unchanged: it filters by the open character's restrictions and by
nothing else. Default restrictions do not apply to it.

1. Open a character that restricts a source, then open **Compendium**.
2. Close all characters and open **Compendium** again.

Expect: filtered to what that character may use in the first case; everything in
the second.

## 7. Characters that used to complain

Loading all content fixed cases where a character referenced content from a
switched-off book. Open these and read the load message, which is now one short
line — "3 saved picks could not be restored — Arcana, Weapon Proficiency (Rifle),
and 1 more" — with the ids and their saved paths in the Console rather than in
the toast:

| Character | Before | Expect now |
| --- | --- | --- |
| Seraphine | 17 elements could not be restored | loads clean |
| Crow | 1 element could not be restored | loads clean |
| Honesty | 1 element could not be restored | loads clean |
| Michelle Character 2 | 22 elements could not be restored | 3 remain |

The three that remain on Michelle Character 2 are separate pre-existing content
problems, not this work — all three are firearm proficiencies, the same family as
`LASTER_PISTOL` below. Michelle Character 1 also keeps 3, which are ASI options;
that character is not in the table above but behaves the same way.

## 8. Aurora Legacy still works

Legacy keeps applying local corrections, and a save from either app now preserves
what the other stores.

1. Build and run Legacy: `dotnet build Aurora.Lights/Aurora.Legacy.csproj`.
2. Open a character Reflections saved.
3. Check a corrected item appears once, not twice. The installed content now
   carries three managed correction files — the Farmer background, the Stoneheart
   sorcerer, and the 2024 equipment packs — and none of them renames an ID any
   more, so the older instruction to look for seven renamed IDs no longer applies.
   A duplicate, or a pack whose cost setter is back, would mean corrections were
   not applied.
4. Open **Fresh E**, which has three custom features added in Reflections, save it
   in Legacy, then reopen it in Reflections.

Expect: the custom features survive the trip. Legacy will not apply them while you
are in Legacy — that is Reflections-only behavior — but saving must not discard
them.

## 8b. One element of a name

A character may hold only one element of a name, for spells, classes, subclasses,
feats, races and backgrounds. Both printings stay in the picker — taking the 2014
or the 2024 version is the player's call, and mixing them across different spells
is fine — but the twin of something already held is not selectable.

1. Open a caster and add a spell that exists in both Player's Handbooks. Bane,
   Aid and Alarm all do; 358 spell names are in both.
2. Open the same picker again and look for the other printing of that spell.

Expect: it is listed and unavailable, not missing. Every other spell is unaffected.

3. Open the row holding that spell and swap it for the other printing.

Expect: allowed. Editing a pick can always swap it for its twin, because that
replaces it rather than adding a second copy.

## 9. Ordinary use

A short pass over the things most likely to notice a regression: level up a
character, change a class choice, edit inventory, generate a character sheet, run
a PDF import, and switch between two open character tabs.

## Expected observations, not bugs

- **Characters gained weapon proficiencies** (Claw, Chakram, magitech firearms and
  similar) unless they restrict the books that grant them. Those books were
  switched off entirely before, so their grants never fired.
- **New warnings mentioning Ryoko content** in the console: unresolved append
  targets and generic parsing notes. They belong to that content and were
  invisible only because it was switched off.
- **Stale references to renamed content** in older saves.
  `LASTER_PISTOL` is a typo the content has since fixed to `LASER_PISTOL`;
  `MODERN_FIREARMS_RIFLE` was split into `RIFLE_AUTOMATIC` and `RIFLE_HUNTING`;
  and a skill proficiency re-attributed from the PHB to Aurora Legacy Essentials
  changes the id of the proxy item that grants it. The 0.9.0 alias map restores
  references with an explicit forwarding entry. References without one still
  require a content repair or re-picking the appropriate choice and saving.
- **An older-format database reads as out of date on first launch** after taking
  this branch, because the content format moved to data version 18. Refresh once;
  a database already at v18 does not need this migration again.

## If something looks wrong

The app writes a startup log; its path is reported in the log itself at launch,
under the app data directory. The in-app Console surfaces engine warnings as they
happen — worth having open during checks 2 and 5.

To get back to where you started: close the app, restore the backup folder over
`5e Character Builder`, and restore `settings.json`.
