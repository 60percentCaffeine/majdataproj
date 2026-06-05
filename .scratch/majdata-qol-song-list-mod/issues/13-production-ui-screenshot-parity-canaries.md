Status: ready-for-agent

# Production UI screenshot parity canaries

## What to build

Add AFK screenshot canaries for the production Majdata QoL song list mod surfaces that were validated during prototyping. The canaries should launch or drive the installed production mod, capture the current UI states, and save the screenshots as reviewable artifacts so a human can inspect them later without rerunning the game.

The goal is to verify that the shipped mod still uses the prototype-approved native Majdata UI treatment for selected-song metadata, S-rank score/rank spacing, Random Recommended folder tile text/icon treatment, Map List settings, hydration status text, and Random Recommended refresh status.

## Acceptance criteria

- [ ] Production screenshots are captured for selected-song metadata, selected-song metadata with visible score/rank, Random Recommended folder tile, Map List settings, hydration progress/status, and Random Recommended refresh instruction/status.
- [ ] Captured screenshots are written to stable files under the feature scratch screenshot/artifact area with names that distinguish production captures from prototype captures.
- [ ] The canary asserts objective UI facts for each captured state, such as expected object presence, text content, visibility, setting order, status visibility/idle hide, and list UI preservation.
- [ ] The canary leaves enough metadata in logs or an artifact file to identify the game build, mod build/commit, capture time, and screenshot paths.
- [ ] The canary does not require human judgment to pass, but the saved artifacts are suitable for later human visual review against the prototype screenshots.
- [ ] Existing unit tests and in-game smoke/canary tests still pass.

## Blocked by

None - can start immediately
