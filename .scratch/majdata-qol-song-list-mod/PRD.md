# PRD: Majdata QoL Song List Mod

Status: ready-for-agent

## Problem Statement

MajdataPlay's current song list is folder-first. Charts are grouped by local chart directories and the user can search and sort within that structure, but the list does not support the ways players commonly decide what to play: by difficulty, level bracket, artist, prior play history, score state, online collections, or random recommendations.

The user wants a MajdataPlay MelonLoader mod that improves quality of life in the song list without changing the default folder behavior. The mod should add richer grouping, filtering, sorting, metadata display, random recommendations, online collection folders, and background metadata hydration while preserving gameplay performance.

## Solution

Build a MajdataPlay MelonLoader mod, that augments the existing list scene and settings flow.

From the player's perspective:

- The default `Folder` grouping behaves like MajdataPlay does today, including existing `All` and `MyFavorites` folders.
- A new settings selector lets the player choose alternate grouping modes: `Folder`, `Difficulty`, `Level`, `Artist`, `Name`, and `Rank`.
- `Random Recommend` is always available as a virtual folder alongside `MyFavorites` in every grouping mode. It is fetched on login and can be refreshed by the user through an obvious UI affordance to be designed later during UI prototyping.
- Website collections owned or subscribed by the logged-in user appear as folders in game.
- Songs show how many usable difficulties they have.
- Songs can be filtered to those with more than 1, more than 2, or more than 3 usable difficulties.
- Sorting includes existing MajdataPlay sort modes plus artist, play count, and supported Sinmai-like modes where Majdata data exists.
- BPM is displayed when known and hydrated in the background when unknown.
- Metadata calculation and online fetching happen in the background only outside gameplay, with simple upper-screen progress text.

## User Stories

1. As a MajdataPlay player, I want the default folder list to keep working as it does today, so that installing the QoL mod does not disrupt my existing workflow.
2. As a MajdataPlay player, I want to choose a grouping mode in Settings, so that I can organize the song list around the way I pick charts.
3. As a MajdataPlay player, I want to group songs by difficulty bracket, so that I can quickly browse charts that have Easy, Basic, Advance, Expert, Master, ReMaster, or UTAGE charts.
4. As a MajdataPlay player, I want to group songs by level, so that I can browse charts by difficulty target.
5. As a MajdataPlay player, I want level grouping to follow Sinmai-like buckets, so that `13`, `13.0`, `13+`, and `13.1` are grouped predictably.
6. As a MajdataPlay player, I want malformed or unknown levels to appear in `Other`, so that unusual charts do not disappear.
7. As a MajdataPlay player, I want to group songs by artist, so that I can browse music from a specific artist.
8. As a MajdataPlay player, I want songs with missing artist data to appear in `Unknown Artist`, so that incomplete metadata remains discoverable.
9. As a MajdataPlay player, I want to group songs by title/name buckets, so that I can browse large lists alphabetically.
10. As a MajdataPlay player, I want songs with missing title data to appear in `Other`, so that incomplete metadata remains visible.
11. As a MajdataPlay player, I want to group songs by rank, so that I can find charts by my score state.
12. As a MajdataPlay player, I want unplayed charts to appear in `No Play` for rank grouping, so that unplayed content is easy to identify.
13. As a MajdataPlay player, I want `Random Recommend` to be available in every grouping mode, so that I can jump into random suggestions regardless of the current organization.
14. As a MajdataPlay player, I want `Random Recommend` to be fetched on login, so that recommendations are ready when I reach the song list.
15. As a MajdataPlay player, I want an obvious way to refresh `Random Recommend`, so that I can request a new batch when the current suggestions are not interesting.
16. As a MajdataPlay player, I want random recommendations to use the unauthenticated MajdataNet recommendation/list data, so that they work even when the endpoint does not require user auth.
17. As a MajdataPlay player, I want local fallback behavior for random recommendations when configured, so that the folder can still be useful when online data is unavailable.
18. As a MajdataPlay player, I want website collections to appear as folders, so that collections I manage on MajdataNet can be played in game.
19. As a MajdataPlay player, I want subscribed or favorited website collections to appear as folders, so that collections curated by others can be played in game.
20. As a MajdataPlay player, I want collection folders to resolve downloaded charts when available, so that local assets are preferred when I already have the chart.
21. As a MajdataPlay player, I want collection folders to include online-only charts when online play is enabled, so that collections are not limited to downloads.
22. As a MajdataPlay player, I want unresolved collection entries to be omitted from playable lists but reflected in folder counts, so that I understand when a collection could not fully resolve.
23. As a MajdataPlay player, I want each selected song to show its number of available difficulties, so that I know whether a song has multiple charts.
24. As a MajdataPlay player, I want to filter songs by more than 1 difficulty, so that I can find songs with at least two charts.
25. As a MajdataPlay player, I want to filter songs by more than 2 difficulties, so that I can find songs with at least three charts.
26. As a MajdataPlay player, I want to filter songs by more than 3 difficulties, so that I can find songs with broader chart sets.
27. As a MajdataPlay player, I want sorting by artist, so that artist browsing can be applied within any folder.
28. As a MajdataPlay player, I want sorting by local play count, so that I can find charts I have played most or least.
29. As a MajdataPlay player, I want sorting by online play count where online data is available, so that online charts can be ordered like the website.
30. As a MajdataPlay player, I want missing play count data to be treated as unknown until fetched, so that unknown data is not falsely shown as zero.
31. As a MajdataPlay player, I want stale online play count data to be used until refreshed, so that sorting remains useful even without current network results.
32. As a MajdataPlay player, I want sorting by release/upload time, so that recent charts are easy to find.
33. As a MajdataPlay player, I want sorting by AP/FC state where scores exist, so that I can find charts by clear status.
34. As a MajdataPlay player, I want sorting by DX score where scores exist, so that I can find charts by score progress.
35. As a MajdataPlay player, I want BPM displayed for the selected song, so that I can judge chart feel before starting.
36. As a MajdataPlay player, I want unknown BPM to display as pending or unknown, so that the UI does not imply a false BPM.
37. As a MajdataPlay player, I want BPM data to be calculated in the background, so that list browsing is not blocked by chart parsing.
38. As a MajdataPlay player, I want background calculation and fetching to stop during gameplay, so that gameplay performance is protected.
39. As a MajdataPlay player, I want background hydration to resume in menus and settings, so that metadata improves while I am not playing.
40. As a MajdataPlay player, I want upper-screen progress text for hydration, so that I know why data is changing or appearing.
41. As a MajdataPlay player, I want stale metadata to remain usable, so that temporary network failures do not erase useful data.
42. As a MajdataPlay player, I want offline charts to hydrate before normal online charts, so that my downloaded library becomes useful first.
43. As a MajdataPlay player, I want special online charts from collections and recommendations to hydrate before unrelated online charts, so that the folders I intentionally opened improve first.
44. As a MajdataPlay player, I want normal online charts to hydrate only when browsing folders where they are visible, so that the mod does not fetch the entire server catalog unnecessarily.
45. As a MajdataPlay player, I want visible songs and nearby songs to hydrate first while browsing a large online folder, so that the data I can see updates quickly.
46. As a MajdataPlay player, I want the list not to jump around while hydration completes, so that browsing remains stable.
47. As a MajdataPlay player, I want refreshed hydrated values to affect sorting after re-entering or refreshing a folder, so that new data improves future browsing without disrupting the current cursor.
48. As a MajdataPlay player, I want downloaded and online copies of the same chart to be deduplicated by hash, so that the same chart is not shown twice unnecessarily.
49. As a MajdataPlay player, I want downloaded assets to be preferred when a chart exists both locally and online, so that startup and playback use reliable local data.
50. As a MajdataPlay player, I want online metadata to remain attached to downloaded charts when hashes match, so that collections, play count, and recommendation data still work.
51. As a MajdataPlay player, I want an online scope setting, so that I can choose downloaded-only, online-only, or mixed browsing.
52. As a MajdataPlay player, I want online-only folders hidden or degraded cleanly when online is disabled, so that the song list remains understandable.
53. As a MajdataPlay player, I want the mod to respect existing MajdataPlay online settings and login state, so that it fits the current account flow.
54. As a MajdataPlay player, I want simple source indicators for downloaded, online, and downloaded-plus-online charts, so that I understand what will be streamed or local.
55. As a MajdataPlay player, I want errors from network hydration to be non-fatal, so that browsing and gameplay continue if the server is unavailable.
56. As a MajdataPlay player, I want chart metadata hydration to be rate-limited, so that the mod does not spam MajdataNet.
57. As a MajdataPlay player, I want cache data to survive game restarts, so that expensive calculations and recent fetches are reused.
58. As a MajdataPlay player, I want level normalization to allow decimal levels and trailing plus signs, so that charts with common custom level notation group correctly.
59. As a MajdataPlay player, I want unsupported Sinmai-like data modes to be omitted or degraded, so that the UI does not offer broken choices.
60. As a future implementing agent, I want grouping and sorting rules isolated in pure modules, so that they can be tested without launching MajdataPlay.
61. As a future implementing agent, I want online API access isolated behind one adapter, so that endpoint changes do not spread throughout UI patches.
62. As a future implementing agent, I want hydration scheduling isolated from cache storage and chart parsing, so that priority rules can be tested independently.
63. As a future implementing agent, I want Harmony patches to be thin adapters around deep modules, so that runtime patch risk stays localized.

## Implementation Decisions

- The feature is a MajdataPlay MelonLoader mod, not a fork of MajdataPlay.
- The default grouping mode is `Folder` and must preserve current MajdataPlay folder behavior, including the existing `All` folder and local `MyFavorites`.
- `All` is not a separate grouping mode.
- `Version` grouping is explicitly out of scope and must not be added.
- Supported grouping modes are `Folder`, `Difficulty`, `Level`, `Artist`, `Name`, and `Rank`.
- Grouping is selected through the Settings screen via an additional selector similar in spirit to Sinmai's category/sort selectors.
- The grouping selector should live in a QoL/list settings area exposed by the mod. It should not require users to edit JSON manually.
- `Random Recommend` is an always-present virtual folder alongside `MyFavorites` in every grouping mode.
- `Random Recommend` should be fetched on login so the folder is ready when the player reaches the song list.
- There must be an obvious user-facing way to refresh `Random Recommend`, but the exact UI affordance is intentionally deferred to a later UI prototyping stage.
- `Random Recommend` should use MajdataNet's unauthenticated chart list/recommendation behavior. The current frontend implements random recommendations by fetching the chart list and deterministically shuffling a batch.
- The mod should use a unified catalog row model for local and online chart identities. The catalog row should include hash, title, artist, uploader, designers, levels, timestamp, source, optional online id, optional local folder, optional collection membership, score facets, interaction facets, and hydration state.
- Local and online entries with the same hash should be deduplicated. Local assets should be preferred for gameplay when available, while online metadata remains available for collections, recommendations, and interaction data.
- `OnlineSongDetail` should remain the runtime mechanism for online-only chart playback. The mod should not reimplement online chart streaming/downloading.
- The online catalog should be populated from the existing online chart list endpoint. The list response includes id, title, artist, designer, description, levels, uploader, timestamp, hash, tags, and public tags.
- The online summary endpoint currently does not provide BPM. It should not be used as a BPM source.
- BPM should be calculated from `maidata` parsing, matching the game's existing chart analysis approach.
- BPM should be displayed as a single value when min and max match, as a range when they differ, and as unknown/pending when no value is available.
- BPM sorting must not block list construction. Missing BPM sorts after known BPM.
- Local BPM cache can be kept forever by chart hash because a changed chart produces a changed hash.
- Online BPM cache can also be kept forever by chart hash once the chart's `maidata` has been fetched and parsed.
- Online interaction stats such as play count, like count, and comment count should use a 24-hour stale-while-revalidate policy.
- Stale data must not be discarded. Stale data is better than no data and should be used immediately while refresh is queued.
- Local personal play count is available from the game's score database after score manager initialization and can be used immediately.
- Online aggregate play count is not available from the online chart list or summary endpoints. It should be fetched from interaction summary or interaction endpoints.
- Sorting by play count should use local play count immediately for downloaded charts.
- For server-side online lists sorted by play count, the adapter may request `sort=playp` from the chart list endpoint.
- For mixed/custom folders, online play count sorting should use cached interaction data if available. Unknown online play count should be treated as unknown, not confirmed zero.
- Unknown values should sort after known values for descending "most played" style sorts.
- The song list should not live-resort while hydration updates values. New values may affect ordering after the player re-enters the folder, refreshes the folder, or triggers a deliberate resort.
- Difficulty count is the count of nonempty usable levels in the song's level array.
- Difficulty count filters are `Off`, `>1`, `>2`, and `>3`.
- Difficulty grouping should include a song in every difficulty folder where it has a nonempty usable level.
- Songs with no usable levels should appear in `Other` for difficulty grouping.
- Level grouping should use the currently selected difficulty's level where applicable.
- Level normalization rules are:
  - `13` and `13.0` map to `13`.
  - `13+` maps to `13+`.
  - Decimal values greater than the integer floor, such as `13.1`, map to `13+`.
  - Values greater than or equal to the next integer map according to their own integer floor.
  - Blank, null, malformed, or unparsable values map to `Other`.
- Artist grouping should map blank or null artist values to `Unknown Artist`.
- Name grouping should map blank or null title values to `Other`.
- Rank grouping should map charts with no score to `No Play`.
- Played-before style folders or filters should include only songs with local or online score/play evidence.
- Website collections should appear as virtual folders.
- User-created collections should be fetched from collection list endpoints using the logged-in username.
- Subscribed/favorited collections should be fetched from the favorite collection endpoint.
- Collection contents should resolve by hash against the unified catalog. If the hash resolves to both local and online, local playback should be preferred.
- Collection folder details should communicate when only some collection entries resolved, using a resolved-count versus total-count concept.
- Online collection membership and collection summaries should be cached with stale-while-revalidate behavior.
- Hydration should run only outside gameplay.
- Hydration is allowed in menu, list, setting, login, title, and similar non-gameplay scenes.
- Hydration should pause or cancel active work when entering gameplay or practice.
- Hydration should resume in non-gameplay scenes.
- Hydration should use a background worker for CPU-heavy calculation and a bounded asynchronous network lane for online fetching.
- Hydration progress should be shown on the upper screen as a simple text string, for example `Fetching data for 1/10 songs...` or `Calculating BPM for 4/18 songs...`.
- Hydration status should be passive and should hide after a short idle period.
- Hydration priority is:
  - missing data before stale data;
  - offline charts before online charts;
  - special online charts before other online charts;
  - visible online charts before non-visible online charts.
- Special online charts include online charts from collections and `Random Recommend`.
- Other online charts should not hydrate unless the user is currently browsing a folder where they are visible, such as `All`, `Online`, or an online grouping folder.
- When browsing a large online-visible folder, hydrate visible cover slots and nearby lookahead first, then continue with the rest of that folder only while idle.
- The hydration queue should reprioritize when the current folder or cursor changes.
- Network hydration failures should be logged and treated as recoverable.
- The deep modules to build are:
  - `CatalogNavigator`, which exposes a small interface for building grouped collections and sorting/filtering them.
  - `CatalogIndex`, which builds and maintains unified local/online chart rows.
  - `LevelBucketizer`, which owns level normalization and folder naming rules.
  - `VirtualCollectionFactory`, which creates grouped folders and always-present virtual folders.
  - `ScoreFacet`, which adapts local and online score/play state into sortable facts.
  - `MajdataNetAdapter`, which owns all MajdataNet API calls and DTO conversion.
  - `HydrationScheduler`, which owns pause/resume, queue priority, and work dispatch.
  - `HydrationStore`, which owns persistent cache values, freshness, and stale-while-revalidate semantics.
  - `ChartDataHydrator`, which calculates BPM and other parsed chart facts from `maidata`.
  - `OnlineStatsHydrator`, which fetches interaction stats such as play count.
  - `BrowsingContextTracker`, which tracks current grouping, folder, cursor, and visible songs for hydration priority.
  - `HydrationStatusOverlay`, which renders upper-screen progress text.
  - `SettingsBridge`, which exposes QoL settings in the game's settings UI.
- Harmony patches should be thin adapters that connect existing list, sort/find, settings, login, and scene lifecycle events to these modules.

## Testing Decisions

- Tests should focus on external behavior of deep modules, not Harmony patch internals or Unity rendering details.
- Unit test `LevelBucketizer` thoroughly, including integer levels, decimal levels, plus levels, blank values, malformed values, and boundary cases.
- Unit test difficulty count calculation and difficulty count filters.
- Unit test grouping behavior for folder-preserving defaults, difficulty groups, level groups, artist groups, name groups, rank groups, `Other`, `Unknown Artist`, and `No Play`.
- Unit test that `Random Recommend` and `MyFavorites` are inserted as always-present virtual folders in all grouping modes.
- Unit test that `All` remains part of default folder behavior and is not represented as its own grouping mode.
- Unit test local/online deduplication by hash and local-asset preference.
- Unit test collection hash resolution, including local-only, online-only, duplicate local-plus-online, and unresolved hashes.
- Unit test sorting behavior for artist, title, release, play count, BPM, AP/FC, DX score, and unknown values.
- Unit test that unknown online play count is distinct from confirmed zero.
- Unit test stale-while-revalidate cache behavior: fresh data is used, stale data is used and refresh is queued, missing data is queued before stale data, and stale data is not deleted on network failure.
- Unit test hydration priority ordering for missing versus stale data, offline versus online data, special online charts versus other online charts, and visible versus non-visible online charts.
- Unit test hydration pause/resume behavior using scene-state inputs rather than real Unity scenes.
- Unit test that gameplay/practice scene states block hydration work.
- Unit test that folder/cursor changes reprioritize visible and nearby songs.
- Unit test MajdataNet adapter DTO conversion using representative JSON from chart list, collection list/hash list/song list, account scores, and interaction summary responses.
- Integration tests should use the existing mod-test-tools harness where practical.
- Add or extend canary tests to verify that song storage remains nonempty, known local charts remain playable, and the mod does not break existing list entry to gameplay.
- Add a canary test that reads the built catalog after boot and verifies the presence of default folder behavior, `MyFavorites`, and `Random Recommend`.
- Add a canary test that verifies level grouping places a known chart into the expected normalized bucket.
- Add a canary test that verifies hydration does not run during gameplay.
- Add a canary test that verifies persistent cache files are written under the mod's own cache location and do not mutate unrelated game state.
- Avoid screenshot-based UI tests for this PRD. The exact refresh affordance for `Random Recommend` is deferred to a UI prototyping stage.

## Out of Scope

- Changing MajdataPlay's default folder behavior.
- Moving `All` into a separate grouping mode.
- Adding `Version` grouping.
- Designing the final UI affordance for refreshing `Random Recommend`.
- Rewriting MajdataPlay's core song storage implementation.
- Replacing `OnlineSongDetail` or reimplementing online chart download/playback.
- Fetching and hydrating the entire online catalog at startup.
- Blocking list startup on BPM calculation or online play count fetching.
- Live-resorting a folder while the user is browsing it.
- Implementing unsupported Sinmai data that Majdata does not expose, such as sync-state sorting, unless a real data source is added later.
- Creating final visual polish, animations, icons, or layout for the settings/list changes.
- Implementing collection editing from inside the game.
- Implementing authenticated account creation or login UI changes.

## Further Notes

MajdataPlay currently builds `SongStorage.Collections` from local chart folders and online endpoints. `SongCollection.SortAndFilter` owns basic keyword filtering and several sort types. `CoverListDisplayer` copies collections for list browsing and has special handling for score/rank sort by difficulty. `SortFindManager` exposes a fixed set of sort options. `ScoreManager` loads local score and play-count data from the score database. `OnlineSongDetail` already fetches and caches online `maidata`, audio, image, and video assets on demand.

MajdataNet frontend source confirms that the website's Random Recommend section fetches the public chart list and creates a randomized batch client-side. The frontend's chart list and summary data do not include BPM. The website uses `sort=playp` for play-count style list sorting and uses interaction summary/interact endpoints for play/like/comment counts.

The implementation should favor deep, pure modules for catalog construction, grouping, sorting, level bucketing, and hydration priority. The Harmony and Unity-specific code should be treated as adapters so the behavioral rules in this PRD can be tested outside the game.
