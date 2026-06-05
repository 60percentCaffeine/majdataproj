Status: completed

# Level, difficulty, and grouping core

## What to build

Implement the pure grouping rules for `Difficulty`, `Level`, `Artist`, `Name`, and `Rank`, plus difficulty-count calculation/filtering. Default `Folder` grouping must remain a pass-through of the game's current folder collections with existing `All` behavior preserved.

## Acceptance criteria

- [ ] `LevelBucketizer` maps integer, decimal, plus, blank, malformed, and boundary levels according to the PRD.
- [ ] Difficulty count is based on nonempty usable levels and filters support `No`, `>1`, `>2`, and `>3`.
- [ ] Difficulty grouping includes a song in every usable difficulty folder and places songs with no usable levels in `Other`.
- [ ] Level grouping uses the selected difficulty's normalized level bucket.
- [ ] Artist grouping maps blank/null artist to `Unknown Artist`.
- [ ] Name grouping maps blank/null title to `Other`.
- [ ] Rank grouping maps missing score to `No Play`.
- [ ] Unit tests cover all grouping modes and default folder preservation.

## Blocked by

- 02-catalog-row-and-index-deduplication

## Comments

- 2026-06-05: Implemented `LevelBucketizer`, `DifficultyCount`, `CatalogCollection`, `CatalogGroupingRequest`, and `CatalogNavigator`. Default grouping now passes through existing folder collections, while alternate grouping supports difficulty bracket, normalized level, artist, title/name, and rank buckets plus difficulty-count filters. Verification: `test-unit.ps1` passed 36/36 tests; `build.ps1` passed; `install.ps1` installed the updated mod/core DLLs.
