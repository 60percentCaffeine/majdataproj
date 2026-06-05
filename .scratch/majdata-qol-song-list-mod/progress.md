# Progress

## 2026-06-05T13:20:52+09:00 - Issue 10 settings bridge and list patches

- Added a core `MapListSettingsBridge` model for the `Map List` settings group, PRD defaults, menu ordering, and request mapping.
- Added the MajdataPlay runtime bridge that injects `Map List` before `Game`, binds native setting cards to map-list settings, preserves default folders, adds `Random Recommended`, and rebuilds virtual folders for representative grouping modes.
- Added a live canary script on TestHookMod port `17444` that asserts default `All`/`MyFavorites`/`Random Recommended`, setting order `Map List, Game`, and `Difficulty Bracket` grouped folders with `Random Recommended`.
- Verification: `test-unit.ps1` passed 91/91, `build.ps1` passed, and `test-smoke.ps1` passed against MajdataPlay.

## 2026-06-05T13:27:18+09:00 - Issue 11 selected song metadata and status UI

- Added a pure selected-song metadata formatter for source text, length fallback, difficulty count, BPM formatting, and newline normalization.
- Added the native list-scene metadata line between artist and charter/author, reusing `CoverBigDisplayer` TMP styling and moving score/rank text down to avoid overlap.
- Added a right-side upper-screen status overlay with dynamic width, idle hiding, diagnostic hydration status, and persistent `Random Recommended` refresh instruction.
- Extended the live smoke canary to verify the metadata line, list UI preservation, hydration status visibility/idle hide, and `Random Recommended` refresh status.
- Verification: `test-unit.ps1` passed 96/96, `build.ps1` passed through smoke install, and `test-smoke.ps1` passed against MajdataPlay.

## 2026-06-05T13:51:22+09:00 - Issue 12 integration canaries and completion audit

- Extended `test-smoke.ps1` to verify default folder behavior, level grouping for a known chart bucket, hydration gameplay/practice pause policy, mod-owned cache writes, and list-to-gameplay entry.
- Fixed two Majdata runtime compatibility issues discovered by the smoke canary: `LevelBucketizer` no longer calls Mono-missing `Math.Floor(decimal)`, and `HydrationStore` no longer uses Mono-missing `File.WriteAllLines(string,string[])`.
- Added `.scratch/majdata-qol-song-list-mod/completion-audit.md` mapping PRD/issue requirements to current evidence and documented residual limitations.
- Verification: `test-unit.ps1` passed 96/96, `build.ps1` passed through smoke install, and the expanded `test-smoke.ps1` passed against MajdataPlay.

## 2026-06-05T18:43:28+09:00 - Issue 13 production UI screenshot parity canaries

- Added `test-ui-screenshots.ps1`, an AFK production screenshot canary that clean-starts MajdataPlay, installs the production QoL mod and TestHookMod, drives the prototype-approved UI states, and captures six reviewable production screenshots.
- The canary writes stable local artifacts under `.scratch/majdata-qol-song-list-mod/screenshots/production`: Map List settings, selected-song metadata, selected-song metadata with visible score/rank, hydration progress/status, Random Recommended folder tile, Random Recommended refresh status, and `latest-production-ui-screenshots.json`.
- The artifact manifest records the game build/runtime metadata, current git commit, screenshot paths, and objective assertions for text content, setting order, status visibility/idle hide, score/rank visibility, Random Recommended tile selection, and list UI preservation.
- Updated `test-smoke.ps1` to clean-start MajdataPlay before installing/running so stale hook processes from screenshot canaries cannot corrupt settings-order checks.
- Verification: `test-unit.ps1` passed 96/96; `test-ui-screenshots.ps1` passed and refreshed all six production screenshot artifacts; `test-smoke.ps1` passed against MajdataPlay.
