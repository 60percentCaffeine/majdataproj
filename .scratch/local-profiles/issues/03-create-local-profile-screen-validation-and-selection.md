Status: ready-for-agent

# Create Local Profile screen with validation and profile selection

## Parent

.scratch/local-profiles/PRD.md

## What to build

Complete the `Create Local Profile` screen. It should be based on the current Search Songs screen, with sort selection removed, the text input focused, and a visible tip telling players to use the keyboard. Submitting a valid unique name creates an empty local profile, selects it, and starts local-profile play.

Names are trimmed for storage/display validation. Empty or whitespace-only names are rejected. Duplicate names are rejected case-insensitively. Invalid submissions must keep the player on the screen and must not create partial profile data.

## Acceptance criteria

- [ ] Create Local Profile uses a Search Songs-like text-input layout without sort selection.
- [ ] The name input is focused by default.
- [ ] The screen shows a tip to use the keyboard for name entry.
- [ ] Back cancels creation and returns to Local Profile Selection.
- [ ] Empty names and whitespace-only names are rejected with a clear error.
- [ ] Names are trimmed before validation and display/storage.
- [ ] Duplicate names are rejected after case-insensitive comparison.
- [ ] Failed validation leaves no partial local profile behind.
- [ ] A valid unique name creates a new empty local profile with a stable internal identifier.
- [ ] After successful creation, the new local profile becomes the active player and play starts.
- [ ] The new profile appears in Local Profile Selection after returning to account selection.
- [ ] Unit tests cover validation, trimming, duplicate rejection, partial-write prevention, stable identity, and successful creation.
- [ ] A real-game integration test verifies creating a profile with keyboard text input and selecting it from Local Profile Selection.

## Blocked by

- .scratch/local-profiles/issues/02-login-screen-opens-local-profile-selection.md
