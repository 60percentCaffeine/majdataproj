Status: completed

# Settings bridge and list patches

## What to build

Connect the pure map-list settings, grouping, sorting, filtering, and virtual folders into MajdataPlay's settings and list screens through thin Harmony/Unity adapters. The `Map List` settings group should appear first, before `Game`, and default folder browsing must remain unchanged until the player changes settings.

## Acceptance criteria

- [x] The `Map List` settings group appears before `Game`.
- [x] Settings cards expose difficulty filter, sorting, grouping, and downloaded songs filter options with PRD defaults.
- [x] Default `Folder` mode preserves current folder list behavior, including existing `All` and `MyFavorites`.
- [x] Alternate grouping modes rebuild the folder carousel using grouped virtual collections.
- [x] `Random Recommended` appears in every grouping mode and uses the cloud/online icon treatment.
- [x] Existing gameplay entry from the list remains functional.
- [x] Unit and in-game canary tests cover default behavior and a representative grouping mode.

## Blocked by

- 04-virtual-folders-random-and-favorites
- 05-sort-core
- 07-collection-resolution
