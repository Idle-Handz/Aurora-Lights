# Android sustained memory-pressure acceptance

October 22, 2026.

The tested foreground search, character editing and background-resume scenarios passed on the connected Samsung under measured, sustained memory pressure. No Aurora application code or package change was needed. This establishes bounded pressure coverage on this phone; Android did not reclaim Aurora itself, so natural low-memory-kill recovery remains an unobserved boundary.

Release scope remains **Windows x64 and Android stable; Windows ARM64 and macOS experimental; Web deferred**. Work is local and uncommitted.

## Package and preservation scope

- Samsung SM-S366V, ARM64, Android 16/API 36, **5,482,516 KiB** of visible RAM (about 5.2 GiB).
- Existing signed Release package `1.0.0-rc.android-storage2.20261022`, version code **214849101**; APK SHA-256 `8fda42900a44af420dac2bf561fcd5082040c2455d32a2ed562d66b0ad9a4f45`.
- The freshly pulled installed APK matched that digest. Aurora was not updated, uninstalled or cleared during this pass. The earlier storage report retains its signer/build provenance.
- Before testing, fully read and hashed **2,502 external app files / 240,504,313 bytes**. The existing local character library was empty. Two clearly named local synthetic wizard fixtures were added for the edit checks. No Drive files or account authorization were changed.

Evidence lives under [android-low-memory-acceptance-2026-10-22](../artifacts/android-low-memory-acceptance-2026-10-22/). Backups, device screenshots, detailed process diagnostics and package outputs are ignored by Git.

## Pressure method

A temporary, separate ARM64 Android package allocated native memory in 16 MiB blocks, initialized them with random bytes and repeatedly touched their pages. It had no access to Aurora's private files or accounts. Its foreground service declared the [specialUse type and permission](https://developer.android.com/develop/background-work/services/fgs/service-types#special-use), exposed a stop control, stopped automatically after 25 minutes, and released allocations on stop or severe thermal status.

Allocation was bounded by a 2,560 MiB ceiling and configurable headroom targets. The accepted foreground/edit runs used a 1,280 MiB allocation ceiling and an 896 MiB headroom target; background used 1,536 MiB and 640 MiB respectively. These were controller targets, rather than a fixed reservation of physical RAM. Available-memory readings and the helper's actual process existence were checked over USB. No global memory-factor override, process-limit setting, synthetic trim signal, device-wide storage limit or phone trust setting was changed.

Kernel `/proc/pressure/memory` readings supplied independent pressure evidence. PSI figures describe device-wide time spent waiting for memory; UI behavior was checked separately through the native screens. The ordinary helper could not read this kernel interface, so the host collected it over USB.

The initial higher-load trial reached a reported 1,888 MiB of native allocations before Android reclaimed the **helper**. Its exit history records **reason 3, LOW_MEMORY**, while Aurora remained alive and its observed searches matched baseline. This is Android's documented [low-memory-killer exit reason](https://developer.android.com/reference/android/app/ApplicationExitInfo#REASON_LOW_MEMORY). The interrupted peak trial was retained under `trial-1/` and excluded from the sustained-hold pass. Subsequent monitoring required a live helper PID and fresh kernel memory readings, avoiding reliance on a status file left behind after process death.

Real pressure was used instead of injected trim callbacks: Android documents that the running-low/critical trim notifications are no longer delivered since API 34 and were deprecated in API 35. See [ComponentCallbacks2](https://developer.android.com/reference/android/content/ComponentCallbacks2).

## Results

| Scenario | Observed result |
| --- | --- |
| Warm baseline | All content: **18,458** results. Fireball: **21**; Longsword: **22**; Human: **376**. |
| Sustained foreground searches | **370.16 seconds**, **11** searches, every count matching baseline. The kernel sampling span for this phase was 367.05 seconds. Helper allocations were 944–1,280 MiB; available memory was **892.3–1,265.3 MiB**. Peak 10-second PSI averages: some 1.10%, full 0.89%. Aurora's PID stayed unchanged. |
| Two pending character edits | Edited A, left its tab inactive, then edited B while pressure remained active. Both saved fixture files stayed byte-identical. Across the measured edit samples, available memory reached **735.4 MiB** and the 10-second PSI averages peaked at **11.72% some / 11.11% full**. |
| Background hold | Completed a loop of at least **180 seconds**, with a 183.86-second kernel sampling span. The helper remained alive and reached 1,536 MiB of allocations. Available memory was **755.4–1,141.2 MiB**. Aurora remained in the same process throughout. |
| Resume and inactive-tab inspection | B's pending name was visible immediately on return. Reactivating each tab and checking its actual input field confirmed both pending names; source fixture hashes remained unchanged. A's direct field inspection occurred after pressure release. Saved-name tab labels were not used as evidence of pending text. |
| Explicit discard and restoration | Closed both owned dirty tabs through their close controls and accepted two Discard prompts. No recovery offers remained. Restored autosave to its original enabled state. Fireball again returned 21 results and the full compendium returned 18,458. |

All accepted pressure samples reported thermal status **0**. No Aurora process replacement or visible crash/ANR occurred during these scenarios. Foreground sampled PSS stayed between **832.0 and 861.0 MiB**; character/background samples ranged from **808.5 to 1,014.7 MiB**. These are bounded observations, not a long-duration leak guarantee. Raw `ActivityManager.MemoryInfo.LowMemory` samples remained false; the pressure evidence is the measured kernel waiting, reduced headroom and the separate helper's confirmed LOW_MEMORY exit.

No application regression was reproduced. Only the temporary companion was built; the application test suite and release publish were not rerun for this acceptance-only task. The earlier 58-test storage suite retains its original execution boundary.

## Cleanup and final checks

Both original synthetic character files were still byte-identical before removal. Only owned test files and their session sidecars were removed; the temporary helper was stopped and uninstalled. Autosave was restored through the app. Original package version, first-install identity and data directory were retained.

Final complete comparison: **all 2,502 original external files / 240,504,313 bytes were byte-identical**, with zero missing, changed or added files. A clean cold start retained the usual account, the original empty local library and no recovery offer; the full compendium returned **18,458 results**. Autosave was restored, startup downloads remained off, and network and rotation settings matched baseline. Aurora was left stopped on the phone's home screen. The byte comparison covers external app files; private preferences and secure storage remained in place, with their relevant account/settings behavior checked through the app and Android settings.

## Remaining limits

This is one API 36 Samsung with approximately 5.2 GiB of visible RAM and substantial configured swap. Aurora was **not** killed by Android's low-memory killer in this sustained-pressure pass, and no kernel OOM, foreground WebView renderer termination or in-process allocation exhaustion was induced. The subsequent [actual low-memory-kill recovery pass](android-lmk-recovery-acceptance-2026-10-22.md) closes the separate two-draft recovery scenario on this same package and phone; earlier controlled process-termination acceptance remains separate.

Lower-RAM devices, other Android versions/vendors, multi-hour leak testing, and pressure during content import/export or Drive operations remain outside this run. Whole-filesystem ENOSPC, final-replacement power loss and final shipping distribution acceptance retain their existing boundaries. No commit, push, tag or release publication was performed.
