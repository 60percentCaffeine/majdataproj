# Add Three Runtime Variants

Status: ready-for-agent

## Parent

.scratch/ui-prototype-template-mod/PRD.md

## What to build

Add exactly three visual variants for the Sinmai-style selection prototype: a Sinmai-like layout, a Majdata-native hybrid, and a compact fast-flow hybrid. The variants should be switchable at runtime using keyboard left/right arrows only, without interfering with cabinet-style prototype controls.

Every variant must display a giant `VARIANT N/3` label and preserve prototype state when switching.

## Acceptance criteria

- [ ] The prototype includes exactly three initial visual variants.
- [ ] Keyboard left/right arrows cycle through the variants at runtime.
- [ ] Variant switching does not reset selected song, selected difficulty, or current phase unless explicitly reset by a separate action.
- [ ] Every variant displays a very large label in the format `VARIANT N/3`.
- [ ] The three variants differ meaningfully in layout, not just color.
- [ ] Variant switching is covered by unit tests where possible.
- [ ] The running mod remains usable with Majdata/cabinet-style controls while keyboard arrows only control variant selection.

## Blocked by

- .scratch/ui-prototype-template-mod/issues/04-render-song-first-fake-selection-flow.md
