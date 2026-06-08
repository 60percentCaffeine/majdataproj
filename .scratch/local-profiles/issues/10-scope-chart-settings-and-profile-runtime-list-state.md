Status: ready-for-agent

# Scope chart-specific settings and profile runtime/list state

## Parent

.scratch/local-profiles/PRD.md

## What to build

Extend profile scoping to chart-specific settings and profile-wide runtime/list state. Per-chart offsets/preferences should belong to the active local profile. Profile-wide list state should also belong to the active local profile where it is treated as player state.

Guest must keep the current runtime/chart-setting behavior. System-wide runtime/configuration values must remain shared.

## Acceptance criteria

- [ ] Chart-specific settings changed in one local profile do not affect Guest or another local profile.
- [ ] Profile-wide list/runtime state changed in one local profile does not affect Guest or another local profile.
- [ ] Guest chart-setting and runtime/list behavior remains unchanged.
- [ ] System-wide runtime/configuration values remain shared.
- [ ] Local profile chart settings and list state persist across profile/game reloads.
- [ ] Unit tests cover chart-setting and list-state isolation for Guest, profile A, and profile B.
- [ ] Integration tests verify at least one chart-specific setting and one profile-wide list state value differ between two local profiles.

## Blocked by

- .scratch/local-profiles/issues/03-create-local-profile-screen-validation-and-selection.md
- .scratch/local-profiles/issues/09-persist-profile-wide-settings-separately-system-wide-globally.md
