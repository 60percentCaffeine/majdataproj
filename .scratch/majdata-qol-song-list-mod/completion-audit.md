# Majdata QoL Song List Mod Completion Audit

Audit time: 2026-06-05T13:51:22+09:00

## Evidence Reviewed

- Issue files: `.scratch/majdata-qol-song-list-mod/issues/01-*.md` through `12-*.md`.
- PRD: `.scratch/majdata-qol-song-list-mod/PRD.md`.
- Progress log: `.scratch/majdata-qol-song-list-mod/progress.md`.
- Unit verification: `majdata-qol-song-list-mod/test-unit.ps1` passed 96/96.
- Build verification: `majdata-qol-song-list-mod/build.ps1` passed as part of `test-smoke.ps1`.
- In-game verification: `majdata-qol-song-list-mod/test-smoke.ps1` passed against MajdataPlay with TestHookMod on port `17444`.

## Issue Acceptance Criteria

All issue files `01` through `12` are marked `Status: completed` and their acceptance criteria are checked.

- Issues 01-02: production mod scaffold, defaults, catalog row/index, deduplication, and local preference are covered by core files and unit tests for catalog indexing.
- Issues 03-05: level grouping, virtual folders, and sort core are covered by `CatalogGroupingTests`, `CatalogSortingTests`, and settings/grouping canaries.
- Issues 06-07: MajdataNet random recommendation and website collection resolution are covered by adapter/core unit tests and virtual folder behavior.
- Issues 08-09: hydration store/scheduler and chart data/online stats hydrators are covered by hydration and chart hydrator unit tests plus smoke cache/hydration policy checks.
- Issues 10-11: settings/list UI, metadata line, status overlay, and Random Recommended affordance are covered by `test-smoke.ps1`.
- Issue 12: integration canaries and this audit cover default catalog behavior, level bucket grouping, gameplay hydration pause policy, mod-owned cache files, and list-to-gameplay flow.

## PRD Requirement Mapping

- Default folder behavior and existing workflow preservation: verified by smoke checks for nonempty storage, `All`, `MyFavorites`, and `Random Recommended`; issue 10 complete.
- Settings-selected grouping modes and defaults: verified by `Map List` before `Game`, native settings cards, and `MapListSettingsTests`; issue 10 complete.
- Difficulty, level, artist, title/name, and rank grouping rules: covered by `CatalogGroupingTests`; level bucket grouping is also verified in-game by smoke.
- `Other`, `Unknown Artist`, `No Play`, and malformed level handling: covered by `CatalogGroupingTests`.
- `Random Recommend` always-present virtual folder and refresh affordance: covered by virtual folder tests and smoke checks for every representative grouping plus refresh status.
- MajdataNet recommendations and website collection resolution: covered by `MajdataNetAdapterTests`, `WebsiteCollectionTests`, and issues 06-07.
- Difficulty count filters and selected-song difficulty count display: covered by grouping tests and smoke metadata line format check.
- Sorting by title, date, difficulty, note designer, rank, artist, play count, BPM, AP/FC, and DX-score degradation paths: covered by `CatalogSortingTests`.
- BPM parsing, formatting, pending/unknown display, and nonblocking hydration behavior: covered by `ChartHydratorTests`, formatter tests, and smoke status overlay check.
- Hydration store freshness, stale-while-revalidate, failures, scheduler priority, and gameplay/practice pause: covered by `HydrationTests`, `ChartHydratorTests`, and smoke hydration policy/cache checks.
- Local/online deduplication, local asset preference, online metadata attachment, and collection hash resolution: covered by `CatalogIndexTests` and `WebsiteCollectionTests`.
- Online scope/downloaded-online-mixed source behavior and selected song source indicators: covered by settings defaults/options, metadata formatter tests, and runtime source labels.
- Network failures as recoverable and stale data retained: covered by `ChartHydratorTests` and hydration store tests.
- Deep pure modules with thin Unity adapters: current implementation keeps catalog/grouping/sorting/hydration/formatting in core modules and connects them via `MajdataQolSongListMod.cs`.
- Integration testing decisions: current smoke verifies nonempty song storage, default folders, level grouping, hydration gameplay pause policy, mod-owned cache path, and list-to-gameplay flow.
- Out-of-scope constraints: no `Version` grouping, no separate `All` grouping mode, no core MajdataPlay fork, no online playback rewrite, no blocking BPM startup, and no collection editing UI were added.

## Residual Limitations

- The selected-song length currently displays the safe fallback `--:--`; no reliable nonblocking runtime duration source has been introduced yet.
- BPM display uses `BPM pending` until hydration data is available; the canary verifies rendering and formatting, not an end-to-end parsed BPM update in the live list scene.
- The rank grouping runtime adapter currently degrades to `No Play` grouping in live UI; core rank grouping rules are covered by unit tests.
