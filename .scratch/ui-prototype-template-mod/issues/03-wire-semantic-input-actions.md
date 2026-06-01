# Wire Semantic Input Actions

Status: ready-for-agent

## Parent

.scratch/ui-prototype-template-mod/PRD.md

## What to build

Wire the prototype runtime to semantic input actions instead of raw key or button checks. The prototype screens should consume actions such as OK, Back, song next, song previous, difficulty up, and difficulty down.

The adapter should prefer MajdataPlay's centralized input layer through reflection where practical, so the prototype can use the same cabinet and touch mappings as the game. It should also include a keyboard fallback path for development and for cases where the game input layer is unavailable.

## Acceptance criteria

- [ ] Prototype screen logic receives semantic input actions rather than direct Unity key checks.
- [ ] The input adapter can map Majdata-style A3/A6/A4/A5 controls to song and difficulty actions.
- [ ] A keyboard fallback can drive the same actions when Majdata input reflection is unavailable.
- [ ] The active input source is visible in prototype diagnostics.
- [ ] Input mapping behavior is covered by unit tests where it can be tested outside Unity.
- [ ] The runnable mod still launches and renders after the input adapter is connected.

## Blocked by

- .scratch/ui-prototype-template-mod/issues/02-add-pure-prototype-core-and-state-tests.md
