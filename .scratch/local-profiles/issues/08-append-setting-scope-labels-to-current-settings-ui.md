Status: ready-for-agent

# Append setting scope labels to current Settings UI

## Parent

.scratch/local-profiles/PRD.md

## What to build

Keep the current Settings menu structure and append a storage-scope label to every visible setting description. The label must be exactly `(profile-wide)` or `(system-wide)`, with no added explanatory sentence. Labels must appear in Guest, Local Profile, and majdata.net Account modes.

This slice only changes presentation and the setting-scope catalog; profile-specific settings persistence is handled by a later slice.

## Acceptance criteria

- [ ] Every visible setting description appends exactly one scope label.
- [ ] Scope labels are only `(profile-wide)` or `(system-wide)`.
- [ ] No explanatory text is appended beyond the label.
- [ ] Labels appear regardless of active player mode: Guest, Local Profile, or majdata.net Account.
- [ ] Labels survive language changes.
- [ ] Labels work with dynamic descriptions that also show offset-unit text.
- [ ] The current Settings menu structure is unchanged.
- [ ] Unit tests verify complete visible-setting catalog coverage and valid label values.
- [ ] UI/integration tests verify labels appear in the Settings screen without replacing existing descriptions.

## Blocked by

- .scratch/local-profiles/issues/01-bootable-local-profiles-shell-guest-default.md
