Status: ready-for-agent

# Login Screen opens Local Profile Selection

## Parent

.scratch/local-profiles/PRD.md

## What to build

Change the current Login Screen so the old Guest action is replaced by `Local Account`. Selecting `Local Account` opens a new `Local Profile Selection` screen. The selection screen should resemble the current folder-selection screen, but account names replace folder names.

The account list must always show `Create New` first, `Guest` second, and saved local profiles after that. This slice should make Guest reachable through Local Profile Selection and should wire Back/timeout from Local Profile Selection back to the Login Screen. `Create New` may enter a minimal Create Local Profile shell if the full text-entry flow is completed by the next slice.

## Acceptance criteria

- [ ] The Login Screen still supports the existing online login UI and behavior.
- [ ] The old Guest button/action on the Login Screen is replaced with `Local Account`.
- [ ] Selecting `Local Account` opens Local Profile Selection.
- [ ] Local Profile Selection visually follows the folder-selection model: physical-button navigation selects account-like entries where folder names would normally appear.
- [ ] Local Profile Selection orders entries as `Create New`, `Guest`, then saved local profiles from this machine.
- [ ] Selecting `Guest` starts Guest play with current Guest behavior.
- [ ] Pressing Back from Local Profile Selection returns to the Login Screen.
- [ ] Local Profile Selection timeout returns to the Login Screen and does not start Guest automatically.
- [ ] Selecting `Create New` transitions to the Create Local Profile screen or shell.
- [ ] Unit tests cover account-list ordering and selection outcomes for Create New, Guest, Back, and timeout.
- [ ] A real-game integration or smoke test verifies Login Screen → Local Account → Local Profile Selection → Guest reaches the song list.

## Blocked by

- .scratch/local-profiles/issues/01-bootable-local-profiles-shell-guest-default.md
