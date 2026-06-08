Status: ready-for-agent

# Scope scores and result saving to active local profile

## Parent

.scratch/local-profiles/PRD.md

## What to build

Route local score storage and score reads through the active player session. Guest must keep the current score behavior. Local Profile mode must save results and read score facets from only the selected local profile.

This slice covers the end-to-end score path: selected local profile, song-list score/rank/play-count/DX display, result save, and durable reload.

## Acceptance criteria

- [ ] Guest scores continue using the current Guest storage and behavior.
- [ ] Each local profile has isolated score storage.
- [ ] Saving a result in one local profile does not change Guest or another local profile.
- [ ] Song-list score display reads rank, play count, DX score, and FC/AP state from the active profile scope.
- [ ] Score-driven sorting/grouping reads from the active profile scope where those systems use local score data.
- [ ] Local profile scores persist across game/profile reloads.
- [ ] Unit tests cover score save/read isolation for Guest, profile A, and profile B.
- [ ] An integration test verifies that a saved or simulated score in one profile is not visible in another profile.

## Blocked by

- .scratch/local-profiles/issues/03-create-local-profile-screen-validation-and-selection.md
