Status: ready-for-agent

# Apply live Map List sorting and filters

## What to build

Wire the Map List settings into live list construction so the player-visible folder contents respond to sorting, difficulty-count filter, and downloaded/online scope changes. The default Folder behavior must remain unchanged until the player changes settings.

This slice should make the existing settings cards do real work in the live carousel, not just expose values. It should preserve the current selected folder/list flow, avoid live resort jumps while browsing, and keep existing gameplay entry from the list functional.

## Acceptance criteria

- [ ] Changing `Sorting` in the Map List settings changes the visible song order for at least one non-default sort mode with a deterministic in-game canary.
- [ ] Changing `Difficulty Filter` changes the visible song set according to `No`, `>1 difficulty`, `>2 difficulties`, and `>3 difficulties` rules.
- [ ] Changing `Downloaded Songs Filter` applies `Mixed`, `Downloaded only`, and `Online only` scope behavior where the available runtime data supports it, and degrades cleanly when online-only content is unavailable.
- [ ] Default Folder mode still preserves the existing folder list behavior, including `All`, `MyFavorites`, and `Random Recommended`.
- [ ] Alternate grouping modes still rebuild the folder carousel and include `Random Recommended`.
- [ ] Existing list-to-gameplay flow remains functional after non-default sorting/filter settings are applied.
- [ ] Unit tests and in-game canaries cover at least one representative sorting mode, each difficulty filter, downloaded/online scope behavior, and default behavior preservation.

## Blocked by

None - can start immediately
