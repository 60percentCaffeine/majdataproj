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
