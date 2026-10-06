# Android data loss, Android ANR/crash, and macOS slowness, October 3, 2026

This is the initial desktop-only investigation. Subsequent Android phone and emulator results,
including the GC mitigation and XML worker validation, are recorded in
[the Android investigation](android-gc-investigation-2026-10-03.txt). Read the hypotheses and
proposed tests below as historical context for that later evidence.

Three field reports investigated from the code, git history, and measurements taken on this
machine. **No Android device or Mac was available.** Everything labelled *measured* was measured
here against the shipped content database on desktop .NET; anything about device behaviour is a
hypothesis with a named test. An earlier revision of this document concluded that all three reports
shared one cause, the loaded catalog's memory footprint. The evidence below contradicts that and the
conclusion has been withdrawn.

## 1. Android characters lost on reinstall: explained

**The data was never stored anywhere that survives an uninstall.**

- `MauiProgram.cs` defaults Android's data root to `Context.GetExternalFilesDir(null)`, which is
  `/storage/emulated/0/Android/data/<package>/files`. That is **app-specific** external storage.
  Android deletes it when the app is uninstalled, exactly as it deletes internal storage.
- The commit that introduced the default (`5d2d460`, May 18) says it makes data "survive uninstalls and
  signing key changes". It survives *updates*, which is true; it does not survive uninstalls. The
  comment in `MauiProgram.cs` and the Settings text repeat the claim.
- The setting that points at the folder is not safe either. `settings.json` lives under
  `SpecialFolder.ApplicationData`, which on Android is app-private internal storage, so an uninstall
  also removes the pointer.
- **The uninstall was forced, not chosen.** The release workflow stamps the Android `versionCode` as
  seconds since an epoch, so it rises with every release, and Android refuses to install an older
  `versionCode` over a newer one. Going back from 0.9.3 to the 09/25 build required an uninstall.

What *does* survive an uninstall: shared storage the user chooses (a Storage Access Framework folder,
or "All files access" for a sideloaded app), or the cloud. The `feature_google-drive-character-storage`
branch, which gained a commit today ("Add authoritative Google Drive cloud saves with recovery"), is
the right fix and also covers moving between devices.

Not verified on a device: that the folder was actually removed on the reporting phone. The
characters being gone is consistent with it. `adb shell ls /sdcard/Android/data/` for the package name
is a ten-second check.

Separately, the Settings claim that external storage is "accessible from any file manager" is
unreliable on Android 11 and later, where scoped storage restricts `Android/data`.

## 2. Android "not responding", then silent close

### What was tested and ruled out (all measured on desktop)

| Hypothesis | Result |
|---|---|
| The content library's import got heavier between 0.7.1 (v0.9.0-beta) and 0.9.0 (v0.9.3) | **No.** Fresh import: 0.7.1 22.0 s, 0.9.0 21.7 s. Peak working set 435-522 MiB vs 406-410 MiB. Database 196-202 vs 198-206 MiB. |
| The first refresh after upgrading (old-library database refreshed by the new library) is a heavy migration | **No.** 29.0 s, 406 MiB peak, 1,188 of 1,191 files unchanged. Steady-state refresh is 19.0 s. |
| Progress reporting floods the UI thread | **No.** 86 callbacks (0.7.1) vs 82 (0.9.0) over about 22 s, roughly 4 per second. |
| The loaded catalog got bigger | **No, it shrank.** The old v11 load measured a 579 MiB peak in the September 15 format-transition rehearsal. v0.9.3 peaks at 508 MiB, and v0.9.0-v0.9.2 carried the eager spell proxies at 552 MiB managed. |
| The Android package changed | **No.** The SQLite packaging fix landed July 31 and is in every build tested. The build and packaging configuration is essentially identical between v0.9.0 and v0.9.3. |
| Code between `await`s in the loader holds the UI thread (there is no `ConfigureAwait(false)` in `CharacterService`, `DbElementLoader` or `ContentDatabaseService`) | **No, for the loader.** Run under a single-threaded pump, `DbElementLoader.TryLoadAsync` resumed on the UI thread 3 times for 0.03 s total; the longest block was 16 ms. |

### Claims from earlier revisions that were wrong

- *"Both reports share one cause: memory footprint."* Unsupported; the footprint did not grow.
- *"The pre-library build could not have run an import on a phone."* Wrong. `v0.8.4-beta` falls back to
  an in-process importer when no Translator executable exists, which is the Android case.
- *"The ANR is most likely GC pauses on a large heap."* Untested speculation. It remains possible, but
  nothing measured here supports it over alternatives.

### What this probe did not cover

The desktop probe cannot show Android runtime behaviour (Mono's collector, a CPU several times
slower, hard memory limits). It also did not cover `CharacterService`'s post-load steps, the
compendium catalog rebuild that `ContentService` starts in the background after every refresh
(`CompendiumService` changed substantially since v0.9.0), the equipment search index warm-up,
Blazor re-rendering, or SQLite I/O against FUSE-backed external storage.

### The explanation that fits every measurement, unproven

Every desktop figure is flat or better, so if 0.9.3 genuinely behaves worse on the phone, the
difference is either device-specific or a workload difference. One workload difference is documented:
`docs/android-aggregate-index-refresh-diagnosis.md` records that on **0.7.1**, a full-index refresh
on Android threw a classification error and aborted (reported September 25). 0.8.0 and later fixed
that. If the stable build never completed the full import on the phone, then the heavy on-device work
may never have run to completion before 0.9.x, which would explain "stable then not" without any
regression in the code.

### Decisive tests, in order of cost

1. **On the 09/25 build, run a full-index refresh.** If it aborts with a classification error, the
   stable build never did the heavy work and the comparison was between different workloads. If it
   completes and also freezes, the heavy work was always a problem.
2. **Bisect on the device.** Upgrading keeps data and `versionCode` only rises, so install v0.9.1,
   then v0.9.2, then v0.9.3 over each other, refreshing and loading at each. The windows are 12, 10
   and 4 commits.
3. **Main-thread stack at the freeze.** After a "not responding", `adb bugreport` and read the main
   thread's stack in `/data/anr/`. A garbage-collector frame, a SQLite frame, and one of our frames
   point to three different fixes.
4. **Confirm the kill.** `adb logcat -d | grep -Ei "lowmemorykiller|Killing|am_kill"`.

## 3. Retained-XML prototype

Every element keeps the `XmlNode` it was parsed from (`ElementParser.cs:58`) plus an indented string
copy (`:59`). Three variants over the shipped catalog (20,946 elements), three runs each in separate
processes, results identical to within 1 MiB:

| | peak working set | steady heap | GC pause | elements holding a DOM |
|---|---:|---:|---:|---:|
| retain (today) | 463 MiB | 288.8 MiB | ~630 ms | 20,946 |
| release each `XmlNode` after parsing | 331 MiB | 130.9 MiB | ~310 ms | 0 |
| release both, keep compact UTF-8 | 311 MiB | 104.8 MiB | ~325 ms | 0 |

Releasing the DOM as each element is parsed lowers the **peak** by about 130 MiB, not just the steady
state, because the documents die young instead of being retained. It also halves collector pause time
for the same allocation volume, because 786,184 fewer objects are live. Keeping compact bytes instead
of an indented string saves a further 20-26 MiB.

Limits: this is the element catalog only, not the whole load; the saving for a whole launch is an
estimate, roughly 130-150 MiB off a 508 MiB peak, not a measurement. It is desktop CoreCLR, not
Android's runtime. Fifteen runtime call sites still read `ElementNode`/`ElementNodeString` and would
need on-demand parsing, and `ElementBase` lives in the parity-gated `Builder.Data`.

**Given section 2, this is worth doing as a general footprint reduction but should not be sold as
the fix for the ANR.**

## 4. macOS: machine slow while installed

Unchanged from the earlier revision; nothing in sections 1-3 bears on it.

**Ruled out:** Spotlight/iCloud/Time Machine (the app is sandboxed, so data lives under
`~/Library/Containers`); a background updater (Velopack is Windows-only, behind `#if WINDOWS`); a
login item (no such keys in `Info.plist`); a file watcher (`DataManager.InitializeFileWatcher`
leaks resources but is never called).

**Hypothesis:** a Mac Catalyst app does not quit when its window is closed, and `AppDelegate` is the
default with no termination handling, so a tester who "closed" the app still has a resident process
holding the catalog plus a WebView. This is untested and the footprint it rests on is now known to be
no larger than in earlier builds, so treat it as weaker than before.

**Tests:** close the window, then look for the process in Activity Monitor and check Memory Pressure;
ask whether the machine was already slow before the first launch.

Separately, `Info.plist` declares `UIRequiredDeviceCapabilities = arm64` while the release ships both
x64 and arm64 builds.

## 5. Suggested order

1. Run the device tests in section 2 before changing code for the ANR; three negative results mean a
   guess is more likely to be wrong than right.
2. Replace the false "survives uninstalls" claims, and move the content database (which is
   regenerable) out of external storage. The Google Drive branch addresses the characters.
3. The retained-XML change, as a footprint improvement.
4. Small items: delete `InitializeFileWatcher`; scope `allowBackup`; move the `SpellProxyCatalog.Prime`
   call in `DbElementLoader` inside the offloaded block; reconcile the Catalyst
   `UIRequiredDeviceCapabilities`.
