Status: ready-for-agent

# Keep majdata.net and local profile sessions mutually exclusive

## Parent

.scratch/local-profiles/PRD.md

## What to build

Ensure MVP account modes remain mutually exclusive: Guest, Local Profile, or majdata.net Account. Online login must unselect any local profile before online user data is used. Selecting a local profile must clear online account/session display state and online-score state so local play is not accidentally linked to majdata.net.

This slice should preserve the existing majdata.net login behavior while preventing local profile data from being mounted during online-account play.

## Acceptance criteria

- [ ] At most one active mode is present at a time: Guest, Local Profile, or majdata.net Account.
- [ ] Starting majdata.net login/session clears any active local profile selection.
- [ ] Selecting a local profile clears online account display/session state and unloads online scores.
- [ ] Online account play does not mount local profile storage.
- [ ] Local profile play does not reuse stale online username/avatar/score state.
- [ ] Existing online login cancellation behavior remains familiar and returns through the current Login Screen behavior.
- [ ] Unit tests cover mode transitions and clearing behavior between Local Profile and majdata.net Account.
- [ ] Integration tests or in-game diagnostics verify local profile state is absent during online-account mode and online state is absent during local-profile mode.

## Blocked by

- .scratch/local-profiles/issues/03-create-local-profile-screen-validation-and-selection.md
- .scratch/local-profiles/issues/04-reuse-account-display-ui-for-all-player-modes.md
