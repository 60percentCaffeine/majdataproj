# UI Proposal: Majdata QoL Song List Mod

This proposal is based on the current MajdataPlay screens, the prototype screenshots, and the UI mod patterns captured in `majdata-ui-mod-patterns.md`.

The main design rule is to stay native to MajdataPlay: reuse the existing folder carousel, song info panel, settings cards, folder tile iconography, and upper-screen text style. Avoid heavy custom overlays unless the existing screen has no practical place for the information.

## Map List Settings Group

Add a new `Map List` settings group as the first settings group, before `Game`.

Use the existing settings carousel card style.

Settings:

- `Difficulty Filter`
  - Default: `No`
  - Values: `No`, `>1 difficulty`, `>2 difficulties`, `>3 difficulties`

- `Sorting`
  - Default: `Default`
  - Values: `Default`, `Date Added`, `Difficulty`, `Note Designer`, `Title`, `Rank`, `Artist`, `Play Count`, `BPM`, `AP/FC Rank`

- `Grouping`
  - Default: `Default`
  - Values: `Default`, `Difficulty Bracket`, `Difficulty Level`, `Title`, `Artist`, `Rank`

- `Downloaded Songs Filter`
  - Default: `Mixed`
  - Values: `Mixed`, `Downloaded only`, `Online only`

For now, descriptions should be placeholders:

```text
{Setting Name} Description
```

Example:

```text
Difficulty Filter Description
```

## Song Metadata Line

In the selected song info panel, add one compact metadata line between artist and charter/author.

Format:

```text
{Folder/Source} | {Length} | {Diff Count} diffs | {BPM}
```

Example:

```text
JPORTAL | 01:20 | 3 diffs | 240BPM
```

Fallback examples:

```text
JPORTAL | 01:20 | 3 diffs | BPM pending
Online | --:-- | 1 diff | unknown BPM
```

Layout rules:

- Keep title, artist, metadata, and charter as a readable vertical stack.
- Do not duplicate or clone the author text object for this line.
- Use a fresh TMP object copied from the local style.
- If score/rank info is visible, keep enough vertical spacing so the metadata line does not overlap score text.

## Random Recommended Folder

Add `Random Recommended` as a virtual folder in every grouping mode.

In the small folder tile, use a line break so the text remains readable:

```text
Random
Recommended
```

In the selected folder info panel, keep spaces:

```text
Random Recommended
Count: 0
```

Use the same cloud/online icon as `MajdataNET`, because recommendations are online-backed.

If local fallback recommendations are active later, use one of these approaches:

- Hide the cloud icon for local-only fallback.
- Keep the cloud icon for mixed/online recommendation sources.
- Use the metadata/status text to explain fallback state instead of adding more tile icons.

## Website Collection Folders

Show MajdataNet collections as normal folder tiles in the folder carousel.

Use the folder tile style and the existing cloud/online icon.

Small tile:

```text
Collection
Name
```

Selected info panel:

```text
Collection Name
Count: 24/31 resolved
```

If unresolved entries exist, show resolved count versus total count in the selected info panel. Avoid putting this extra detail on every small folder tile.

## Grouping Modes

Grouping should change the folders shown in the existing folder carousel. It should not introduce a separate browsing screen.

Proposed folder sets:

- `Default`
  - Existing folder list
  - Existing `All`
  - Existing `MyFavorites`
  - Added `Random Recommended`

- `Difficulty Bracket`
  - `Easy`
  - `Basic`
  - `Advance`
  - `Expert`
  - `Master`
  - `ReMaster`
  - `UTAGE`
  - `Other`

- `Difficulty Level`
  - `1`, `2`, `3`, etc.
  - `13`
  - `13+`
  - `14`
  - `14+`
  - `15`
  - `Other`

- `Title`
  - `A`, `B`, `C`, etc.
  - `#`
  - `Other`

- `Artist`
  - Artist-name folders
  - `Unknown Artist`

- `Rank`
  - Rank folders such as `SSS+`, `SSS`, `SS+`, `SS`, `S+`, `S`
  - `No Play`

Use normal folder tiles for all grouping folders. Reserve special icons for source/online state, not for every grouping type.

## Sorting And Filters

Sorting and filters should live in the `Map List` settings group first.

The existing sort/search screen can remain mostly unchanged until implementation proves a faster in-list affordance is needed.

When active filters or non-default map-list settings affect the current folder, show a passive upper-screen status message:

```text
Map List: >2 difficulties | Sorting: BPM | Grouping: Difficulty Level
```

Use the AquaMai-like upper-screen text style described below.

## Downloaded And Online Source Indicators

Use source indicators only where they clarify playback/source behavior.

Recommended places:

- Folder tile cloud icon for online-backed folders.
- Song metadata line source text such as `JPORTAL`, `Online`, `Downloaded`, or `Mixed`.
- Optional later metadata suffixes such as `DL`, `Online`, or `DL+Online`.

Avoid adding source icons to every song cover in the carousel for the first implementation. The carousel is already visually dense.

## Hydration Progress

Hydration progress should use AquaMai-like upper-screen text: lightweight, passive text shown on the upper screen, not a modal and not a card.

Examples:

```text
Calculating BPM for 4/18 songs...
Fetching collection data 2/7...
Refreshing Random Recommended...
```

Behavior:

- Show only while work is active or recently completed.
- Hide after a short idle period.
- Do not block input.
- Do not move, resort, or rebuild the current list while the player is browsing.
- Updated metadata can affect order after the player refreshes, re-enters the folder, or changes sorting.

## Random Recommended Refresh

When `Random Recommended` is selected, show an AquaMai-like upper-screen instruction text.

Example:

```text
Long press refresh to get new recommendations
```

If a refresh is running:

```text
Refreshing Random Recommended...
```

If refresh fails:

```text
Could not refresh recommendations. Showing cached list.
```

Prefer reusing an existing gesture, such as the current long-press refresh behavior, before adding new controls.

## Other Upper-Screen Instruction Text

Any transient instruction or passive status introduced by this mod should use the same AquaMai-like upper-screen text style.

Use it for:

- Hydration progress.
- Random recommendation refresh instructions.
- Active map-list filter summaries.
- Online fallback/cached-data notices.
- Non-fatal network failure notices.

Do not use it for:

- Persistent selected-song metadata.
- Folder names.
- Settings values.
- Errors that require user action.

## Upper-Screen Status Prototype

The AquaMai-like upper-screen text prototype uses a transient status label on the right side of the upper screen. It uses white right-aligned text on a semi-transparent black rectangle, matching AquaMai's lightweight instruction/status treatment rather than the game's larger card panels.

Prototype states:

- Hydration progress:

```text
Calculating BPM for 4/18 songs...
```

- Random Recommended refresh:

```text
Refreshing Random Recommended...
```

Prototype script:

- `prototype-upper-screen-status/inject-and-capture.ps1`

Screenshots:

- `screenshots/latest-hydration-progress-prototype.png`
- `screenshots/latest-random-recommended-refresh-prototype.png`

## Unknown And Pending Data

Represent missing data explicitly but quietly.

Examples:

```text
BPM pending
unknown BPM
? plays
```

Unknown data should not look like confirmed zero. For example, use `? plays`, not `0 plays`, until play-count data is fetched or calculated.

## Current Prototype Artifacts

Reference prototypes:

- `screenshots/latest-song-info-line-prototype.png`
- `screenshots/latest-song-info-line-s-rank.png`
- `screenshots/latest-random-recommended-folder-prototype.png`
- `screenshots/latest-map-list-settings-prototype.png`

Prototype scripts:

- `prototype-song-info-line/inject-and-capture.ps1`
- `prototype-random-recommended-folder/inject-and-capture.ps1`
- `prototype-map-list-settings/inject-capture-and-test.ps1`

Auto-test report:

- `map-list-settings-auto-test.json`
