Status: ready-for-agent

# Whole-system local profile test pass and bug hardening

## Parent

.scratch/local-profiles/PRD.md

## What to build

Run a whole-system verification pass over the completed local profiles MVP and fix bugs found during that pass. This issue is not a substitute for per-slice testing; it is the final integration, regression, screenshot, and polish pass across the complete system.

The final behavior should prove that Guest, Local Profile, and majdata.net Account modes are mutually exclusive, that data isolation holds across favorites/scores/settings, and that all new/changed screens behave as specified.

## Acceptance criteria

- [ ] Full automated unit test suite for the local profiles feature passes.
- [ ] Real-game canaries cover Login Screen → Local Account → Local Profile Selection, Create New, Guest start, local profile start, Back, and timeout paths.
- [ ] Real-game canaries verify `Create New` and `Guest` are the first two Local Profile Selection entries.
- [ ] Real-game canaries verify empty and duplicate local profile names are rejected.
- [ ] Real-game canaries verify favorites are isolated between Guest and at least two local profiles.
- [ ] Real-game canaries verify scores are isolated between Guest and at least two local profiles.
- [ ] Real-game canaries verify profile-wide settings are isolated and system-wide settings are shared.
- [ ] Real-game canaries verify chart-specific settings and profile runtime/list state are isolated where implemented.
- [ ] Real-game canaries verify online login does not mount or write to a local profile.
- [ ] Result screen save-target text is verified for Guest and Local Profile modes, and online mode where feasible.
- [ ] Screenshots confirm Local Profile Selection resembles folder selection and Create Local Profile resembles Search Songs without sort selection.
- [ ] Current Guest behavior is regression-tested and remains unchanged.
- [ ] Bugs found during this pass are fixed or documented with follow-up issues if truly out of MVP scope.
- [ ] The final implementation has no fatal boot/log errors in a clean real-game smoke run.

## Blocked by

- .scratch/local-profiles/issues/01-bootable-local-profiles-shell-guest-default.md
- .scratch/local-profiles/issues/02-login-screen-opens-local-profile-selection.md
- .scratch/local-profiles/issues/03-create-local-profile-screen-validation-and-selection.md
- .scratch/local-profiles/issues/04-reuse-account-display-ui-for-all-player-modes.md
- .scratch/local-profiles/issues/05-scope-myfavorites-to-active-local-profile.md
- .scratch/local-profiles/issues/06-scope-scores-and-result-saving-to-active-local-profile.md
- .scratch/local-profiles/issues/07-show-result-save-target-for-guest-local-online.md
- .scratch/local-profiles/issues/08-append-setting-scope-labels-to-current-settings-ui.md
- .scratch/local-profiles/issues/09-persist-profile-wide-settings-separately-system-wide-globally.md
- .scratch/local-profiles/issues/10-scope-chart-settings-and-profile-runtime-list-state.md
- .scratch/local-profiles/issues/11-keep-majdatanet-and-local-profile-sessions-mutually-exclusive.md
- .scratch/local-profiles/issues/12-route-back-login-and-inactivity-through-login-screen.md
