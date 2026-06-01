# Add Install Smoke Verification

Status: ready-for-agent

## Parent

.scratch/ui-prototype-template-mod/PRD.md

## What to build

Add a lightweight smoke verification path for the prototype mod using the repository's existing build/install and real-game harness conventions where practical. The smoke path should prove that the prototype mod builds, installs, launches in MajdataPlay, and logs activation without fatal boot failures.

This verification should stay focused on boot/install confidence. It should not become a screenshot layout test or a production gameplay regression suite.

## Acceptance criteria

- [ ] There is a documented command or script for smoke-verifying the prototype mod.
- [ ] The smoke path builds and installs the prototype mod before launch.
- [ ] The smoke path launches MajdataPlay using the existing project conventions.
- [ ] The smoke path verifies that the prototype activation log line appears.
- [ ] The smoke path checks for fatal boot failures using existing log/assertion conventions where practical.
- [ ] The smoke path does not require manual UI interaction to pass.
- [ ] Documentation explains what the smoke verification does and does not prove.

## Blocked by

- .scratch/ui-prototype-template-mod/issues/01-scaffold-runnable-prototype-mod.md
- .scratch/ui-prototype-template-mod/issues/05-add-three-runtime-variants.md
