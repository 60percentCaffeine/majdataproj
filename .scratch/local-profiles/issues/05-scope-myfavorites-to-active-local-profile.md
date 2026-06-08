Status: ready-for-agent

# Scope MyFavorites to active local profile

## Parent

.scratch/local-profiles/PRD.md

## What to build

Route MyFavorites/favorite storage through the active player session. Guest must keep the exact current favorite behavior. Local Profile mode must read, display, add, and remove favorites from only the selected local profile.

This should work end-to-end from profile selection through the song list favorite toggle and MyFavorites folder rebuild.

## Acceptance criteria

- [ ] Guest favorites continue using the current Guest storage and behavior.
- [ ] Each local profile has its own favorite storage.
- [ ] MyFavorites is rebuilt from the selected local profile in Local Profile mode.
- [ ] Adding a favorite in one local profile does not add it for Guest or another local profile.
- [ ] Removing a favorite in one local profile does not remove it for Guest or another local profile.
- [ ] Favorite writes are durable across game/profile reloads.
- [ ] Unit tests cover favorite add/remove/read behavior for Guest, profile A, and profile B.
- [ ] A real-game integration test verifies favorite isolation by selecting profiles and observing different favorite state for the same song.

## Blocked by

- .scratch/local-profiles/issues/03-create-local-profile-screen-validation-and-selection.md
