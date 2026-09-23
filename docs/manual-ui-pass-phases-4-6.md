# Manual UI pass — phases 4 to 6

Everything in phases 4 to 6 is verified by automated gates except what a person
has to look at. This is that list. It covers the shared content library going
in-process, source restrictions replacing the old per-package switches, and the
Builder.Data cleanup.

Work on branch `feature_shared-content-library`. Nothing here has merged to `main`.

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

**Check 1 must come before any database refresh.** The migration reads the
switched-off packages recorded in the current database. A refresh rebuilds that
database from your XML, and the rebuild does not carry those flags — they are the
thing being retired. Refresh first and there is nothing left to migrate.

To repeat check 1 later: restore the database copy, and set
`"SourcePreferencesSeeded": false` in `settings.json` with the app closed.

Build and run:

```bash
dotnet build Aurora.App/Aurora.App.csproj -f net10.0-windows10.0.19041.0
```

## 1. First launch after the upgrade

The old per-package switches become source restrictions, once. On this machine
that is one source: **Ryoko's Guide to the Yokai Realms**. Aurora Legacy
Essentials was also switched off but is builder infrastructure, so it must stay
enabled.

1. Launch the app and let content finish loading.
2. Go to **Settings → Content → Sources**.

Expect:
- A **Default source restrictions** section — the same tree as a character's
  Sources tab. The old package list with its search box is gone.
- **Ryoko's Guide to the Yokai Realms** switched off there.
- **Apply these defaults to new characters** switched on. The migration turns it
  on only because it migrated something.
- Aurora Legacy Essentials, Internal and Core still enabled and not switchable.
- Everything else you had switched on is still on.

Then confirm it only happens once: close the app, launch again, and check the
Sources panel is unchanged.

Why it matters: this is the only moment the old preferences can be read. If it
does not happen, a source you had switched off silently comes back for every new
character.

## 2. Content refresh, in-process

Refresh no longer runs a bundled Translator executable; the shared library does
the import inside the app.

1. **Settings → Content → Data**, press **Refresh Database**.
2. Watch the progress bar through the run.

Expect:
- Progress moves through scanning, importing and resolving, in that order, and
  reaches 100%. It should not sit at 0% or jump straight to done.
- No console window appears at any point.
- It finishes with "Database refreshed."
- Afterwards the app still lists your content: open **Compendium** and search for
  something from a third-party book.

Also try cancelling a refresh midway. The app should return to idle with your
existing database intact and content still loaded.

Why it matters: on Windows this replaces a child process. It is also the first
time refresh works at all in a release build, on Mac, or on Android, because
those never shipped the executable.

## 3. Default restrictions editor

The editor in Settings edits **defaults for new characters**. It must never
change the character you have open.

1. Open a character, note a source it uses on its **Manage → Sources** tab.
2. In **Settings → Content → Sources**, switch some sources off and on: a single
   book, a whole group, and one of the broad category buttons.
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

3. Close and reopen the app, and look at **Settings → Content** again.

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
switched-off book. Open these and read the load message:

| Character | Before | Expect now |
| --- | --- | --- |
| Seraphine | 17 elements could not be restored | loads clean |
| Crow | 1 element could not be restored | loads clean |
| Honesty | 1 element could not be restored | loads clean |
| Michelle Character 2 | 22 elements could not be restored | 3 remain |

The three that remain on Michelle Character 2 are separate pre-existing content
problems, not this work.

## 8. Aurora Legacy still works

Legacy keeps applying local corrections, and a save from either app now preserves
what the other stores.

1. Build and run Legacy: `dotnet build Aurora.Lights/Aurora.Legacy.csproj`.
2. Open a character Reflections saved.
3. Check a corrected item appears once, not twice — the hotfix files rename seven
   IDs, so a duplicate would mean corrections were not applied.
4. Open **Fresh E**, which has three custom features added in Reflections, save it
   in Legacy, then reopen it in Reflections.

Expect: the custom features survive the trip. Legacy will not apply them while you
are in Legacy — that is Reflections-only behavior — but saving must not discard
them.

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
- `LASTER_PISTOL` still missing on characters that saved it: a typo in a content
  ID, pre-existing and tracked separately.

## If something looks wrong

The app writes a startup log; its path is reported in the log itself at launch,
under the app data directory. The in-app Console surfaces engine warnings as they
happen — worth having open during checks 2 and 5.

To get back to where you started: close the app, restore the backup folder over
`5e Character Builder`, and restore `settings.json`.
