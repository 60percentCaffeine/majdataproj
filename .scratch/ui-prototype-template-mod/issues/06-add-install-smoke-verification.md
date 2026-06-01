# Add Install Smoke Verification

Status: done

## Parent

.scratch/ui-prototype-template-mod/PRD.md

## What to build

Add a lightweight smoke verification path for the prototype mod using the repository's existing build/install and real-game harness conventions where practical. The smoke path should prove that the prototype mod builds, installs, launches in MajdataPlay, and logs activation without fatal boot failures.

This verification should stay focused on boot/install confidence. It should not become a screenshot layout test or a production gameplay regression suite.

## Acceptance criteria

- [x] There is a documented command or script for smoke-verifying the prototype mod.
- [x] The smoke path builds and installs the prototype mod before launch.
- [x] The smoke path launches MajdataPlay using the existing project conventions.
- [x] The smoke path verifies that the prototype activation log line appears.
- [x] The smoke path checks for fatal boot failures using existing log/assertion conventions where practical.
- [x] The smoke path does not require manual UI interaction to pass.
- [x] Documentation explains what the smoke verification does and does not prove.

## Completion notes

- Added `ui-prototype-template-mod/test-smoke.ps1`.
- The script stops stale game/REPL processes, runs `install.ps1`, launches `Majdata Hub\game\start-controller.bat`, waits for prototype mod load/activation/core/render/timer log lines, scans current MelonLoader and runtime logs for fatal boot patterns, copies artifacts, and stops the game process.
- Documented the smoke command and its limits in `ui-prototype-template-mod/README.md`.
- Verified `test-smoke.ps1` passes without manual UI interaction.

## Blocked by

- .scratch/ui-prototype-template-mod/issues/01-scaffold-runnable-prototype-mod.md
- .scratch/ui-prototype-template-mod/issues/05-add-three-runtime-variants.md
