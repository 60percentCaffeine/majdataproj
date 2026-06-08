Status: ready-for-agent

# Reuse account display UI for Guest, local, and online modes

## Parent

.scratch/local-profiles/PRD.md

## What to build

Reuse the existing majdata.net account display UI to show the active player identity for all MVP modes: Guest, Local Profile, and majdata.net Account. The display should make the account type clear enough that players can tell whether results are going to Guest/current local account/online account.

This slice should centralize display text around the active player session instead of duplicating account-mode checks across screens.

## Acceptance criteria

- [ ] The existing account display UI shows Guest when the active player mode is Guest.
- [ ] The existing account display UI shows the selected local profile name when the active player mode is Local Profile.
- [ ] The local profile display clearly indicates that the active account is local.
- [ ] The existing account display UI continues to show majdata.net identity when the active player mode is online.
- [ ] Switching between Guest and a local profile updates the displayed identity without stale online/local state.
- [ ] Unit tests cover account-display text/metadata for Guest, Local Profile, and majdata.net Account modes.
- [ ] A real-game integration or smoke test verifies the song list displays Guest and a created local profile through the existing user-display UI.

## Blocked by

- .scratch/local-profiles/issues/03-create-local-profile-screen-validation-and-selection.md
