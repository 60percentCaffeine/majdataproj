Status: ready-for-agent

# Persist profile-wide settings separately and system-wide settings globally

## Parent

.scratch/local-profiles/PRD.md

## What to build

Implement storage routing for settings according to the setting-scope catalog. Profile-wide settings should persist separately per selected local profile. System-wide settings should remain shared across Guest, local profiles, and online play. Guest settings must keep current behavior.

This slice should use the existing Settings menu; the player's interaction stays the same, but the save/load target changes for profile-wide settings when a local profile is active.

## Acceptance criteria

- [ ] Profile-wide settings changed under one local profile do not affect another local profile.
- [ ] Profile-wide settings changed under one local profile do not rewrite Guest setting behavior.
- [ ] System-wide settings remain shared globally across Guest, local profiles, and online play.
- [ ] New local profiles start with default/initial profile-wide settings rather than copied Guest data.
- [ ] Settings changes are durable across profile/game reloads.
- [ ] The existing Settings menu remains the interaction surface for all settings.
- [ ] Unit tests cover profile-wide isolation and system-wide sharing across Guest, profile A, and profile B.
- [ ] Integration tests verify changing at least one profile-wide setting and one system-wide setting through the Settings flow has the expected scope.

## Blocked by

- .scratch/local-profiles/issues/03-create-local-profile-screen-validation-and-selection.md
- .scratch/local-profiles/issues/08-append-setting-scope-labels-to-current-settings-ui.md
