Status: ready-for-agent

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
