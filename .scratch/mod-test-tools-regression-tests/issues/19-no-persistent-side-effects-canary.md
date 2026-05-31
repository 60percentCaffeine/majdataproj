# Add no persistent side effects canary

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add a regression test or harness helper that snapshots important user/config/profile files before and after a test run and fails if candidate mods unexpectedly mutate persistent state.

## Acceptance criteria

- [ ] The implementation identifies the smallest practical set of user/config/profile files to snapshot.
- [ ] A test run reports added, removed, or changed files outside an explicit allowlist.
- [ ] Expected test artifacts and logs are excluded from failure checks.
- [ ] The failure output identifies the changed paths.

## Blocked by

None - can start immediately

