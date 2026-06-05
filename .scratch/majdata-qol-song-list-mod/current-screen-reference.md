# Current MajdataPlay Screen Reference for QoL Song List UI

Captured on 2026-06-05 from `MajdataPlay` `0.1.52-20260511-93957ae` using the installed `TestHookMod` and Unity `ScreenCapture.CaptureScreenshot`.

## Relevant PRD Surfaces

The PRD primarily touches four existing UI surfaces:

- the song carousel in `List`, where selected-song metadata and new per-song facts such as BPM, difficulty count, and source indicators would appear;
- the folder/directory carousel in `List`, where default folder behavior, `All`, `MyFavorites`, online folders, website collections, and `Random Recommend` need to coexist;
- the `SortFind` scene, where existing search and sort controls live;
- the `Setting` scene, where grouping/filter/scope selectors need an in-game configuration home.

Runtime metadata from this launch:

- `SongStorage.Collections` contained 49 collections.
- Existing virtual collections included `All` with 13,321 entries and `MyFavorites` with 4 entries.
- Existing online collection source included `MajdataNET` with 11,717 entries and `isOnline = true`.
- Existing built-in `SortType` values were `Default`, `ByTime`, `ByDiff`, `ByDes`, `ByTitle`, and `ByRank`.
- Existing settings categories were `Game`, `Judge`, `Display`, `Volume`, `Mod`, `Debug`, and `ChartSetting`; no visible `Online` category appeared in this build.

## Title Baseline

![Title screen](screenshots/00-title-unity.png)

The title screen confirms the current skin, portrait 1080x1920 layout, top FPS/debug text, and large lower circular visual motif used across menus. This matters because the list and settings screens reuse the same split composition: lightweight information at the top, black middle gutter, and primary interaction inside the lower circular area.

## Song Carousel

![Song carousel](screenshots/01-list-default-folder-browser.png)

This is the `List` scene in chart/song mode (`CoverListMode.Chart`).

Current behavior and layout notes:

- The selected song is represented by the largest cover in the lower circular carousel.
- Adjacent songs are smaller circular covers positioned around the ring, each with a compact level badge.
- The right-side brown panel shows selected song title, subtitle/description line, and artist/designer text.
- The favorite affordance is already present as a heart-plus icon near the selected song metadata.
- The top area shows the logged-in display name, search/sort instructions, refresh instructions, practice/logout/exit instructions, and chart analysis text.
- BPM is already displayed in the upper chart-analysis panel for the selected chart as `BPM = 122`; this is currently part of analysis text, not a dedicated selected-song metadata row.

PRD implications:

- Difficulty count and source indicators should compete for space with the right-side selected-song metadata, not with the cover ring.
- BPM display can probably move from or duplicate the analysis panel into a more deliberate metadata area.
- Hydration progress text belongs in the upper text area; it already contains instructional/status text and has room before the lower circular interaction area.
- The current selected-song panel has little unused vertical space, so extra fields need compact formatting.

## Folder Carousel

![Folder carousel](screenshots/02-list-folder-first-dir-view.png)

This is the same `List` scene after switching the cover list to directory mode (`CoverListMode.Directory`).

Current behavior and layout notes:

- Folders are rendered as brown folder icons around the same circular carousel.
- The selected folder is emphasized by the ring position and a large empty/loader-style center circle.
- The right-side panel shows folder name and count, for example `JPORTAL` and `Count:154`.
- The visible folders are local directory collections such as `JPORTAL`, `KOL`, `KOL2`, `LOA2`, `FFMC`, and others.
- Runtime data confirms `All` and `MyFavorites` exist as virtual collections even when they are not visible in this particular carousel position.

PRD implications:

- The default `Folder` grouping should preserve this visual model and current collection insertion behavior.
- Alternate groupings (`Difficulty`, `Level`, `Artist`, `Name`, `Rank`) can plausibly reuse the folder-card treatment because the current directory view already renders arbitrary collection names and counts.
- `Random Recommend`, website collections, and online grouping buckets should expose a count in the same right-side panel.
- Partial collection resolution (`resolved / total`) likely needs to extend or replace the single `Count:n` line.

## Search And Sort

![Sort/find screen](screenshots/03-sort-find-current.png)

This is the `SortFind` scene.

Current behavior and layout notes:

- The screen has a single large `Search` input with `Use the keyboard...` placeholder text.
- Sorting is a compact selector below the input: heading `Sort by`, left/right arrows, current value `Default`, and label `Sort`.
- There is no visible grouping selector, difficulty-count filter, online scope selector, or multi-row sort/filter stack.
- The screen uses the lower circular canvas but leaves more empty space than the list screen.

PRD implications:

- Additional filters and sort modes could fit here, but the current design only shows one selector at a time.
- If grouping remains in settings as decided by the PRD, this screen should probably stay focused on keyword search plus per-folder sorting/filtering.
- New sort modes should integrate with the existing left/right selector pattern unless a denser filter panel is prototyped.

## Settings: Current Main Category

![Game settings](screenshots/04-settings-current-main.png)

The `Setting` scene opens on `Game Settings`.

Current behavior and layout notes:

- Settings are presented as a carousel of large brown option cards.
- The selected setting is centered and enlarged; the next setting is shown smaller on the right.
- Each option card contains title, description, current value, and left/right arrows.
- Category switching uses the circular outer controls, with `-` and `+` at the top and back/confirm at the bottom.

PRD implications:

- A grouping selector such as `Folder / Difficulty / Level / Artist / Name / Rank` matches the existing left/right option-card pattern.
- The description text needs to stay short; long explanations will crowd the card.
- A QoL/list settings area would need either a new category or placement in an existing category.

## Settings: Current Mod Category

![Mod settings](screenshots/05-settings-mod-category.png)

The existing `Mod` category is the closest current settings home for mod-owned list behavior.

Current behavior and layout notes:

- The category title is simply `Mod`.
- Existing options are gameplay-oriented mod settings such as `Playback Speed` and `Auto Play`.
- The category can host normal option cards, but it is not currently list-specific.

PRD implications:

- Adding every song-list control directly into `Mod` risks mixing unrelated gameplay and list-browsing settings.
- A dedicated `List QoL` or `Song List` category would better match the PRD wording, but would require extending the settings menu category list.
- If implementation cost favors existing categories, a small number of high-level selectors can fit in `Mod`, while volatile actions such as `Random Recommend` refresh should stay in the list UI.
