Status: ready-for-agent

# Show result save target for Guest, local, and online modes

## Parent

.scratch/local-profiles/PRD.md

## What to build

Add result-screen save-target presentation sourced from the active player session and result/upload outcome. Players should be able to see whether the result was saved to Guest, saved to a local profile, uploaded/saved to a majdata.net account, or only saved locally because online upload was not applicable or failed.

## Acceptance criteria

- [ ] Result UI shows a clear Guest save target when Guest mode saves a result.
- [ ] Result UI shows the selected local profile name when Local Profile mode saves a result.
- [ ] Result UI shows the online account identity when majdata.net Account mode uploads/saves a result online.
- [ ] Result UI distinguishes online local-only fallback from successful online upload when applicable.
- [ ] Save-target text is driven by the active player session and result/upload outcome rather than scattered ad hoc checks.
- [ ] Unit tests cover save-target copy/formatting for Guest, Local Profile, successful online account, and online fallback outcomes.
- [ ] A real-game integration or smoke test verifies result save-target text for at least Guest and Local Profile modes.

## Blocked by

- .scratch/local-profiles/issues/04-reuse-account-display-ui-for-all-player-modes.md
- .scratch/local-profiles/issues/06-scope-scores-and-result-saving-to-active-local-profile.md
