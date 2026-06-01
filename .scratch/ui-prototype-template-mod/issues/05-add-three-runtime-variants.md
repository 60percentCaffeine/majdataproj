# Add Three Runtime Variants

Status: done

## Parent

.scratch/ui-prototype-template-mod/PRD.md

## What to build

Add exactly three visual variants for the Sinmai-style selection prototype: a Sinmai-like layout, a Majdata-native hybrid, and a compact fast-flow hybrid. The variants should be switchable at runtime using keyboard left/right arrows only, without interfering with cabinet-style prototype controls.

Every variant must display a giant `VARIANT N/3` label and preserve prototype state when switching.

## Acceptance criteria

- [x] The prototype includes exactly three initial visual variants.
- [x] Keyboard left/right arrows cycle through the variants at runtime.
- [x] Variant switching does not reset selected song, selected difficulty, or current phase unless explicitly reset by a separate action.
- [x] Every variant displays a very large label in the format `VARIANT N/3`.
- [x] The three variants differ meaningfully in layout, not just color.
- [x] Variant switching is covered by unit tests where possible.
- [x] The running mod remains usable with Majdata/cabinet-style controls while keyboard arrows only control variant selection.

## Completion notes

- Added `PrototypeVariantRouter` with exactly three variants and wraparound next/previous behavior.
- Added tests proving the three-variant count, wraparound cycling, and state preservation across variant switching.
- Reserved keyboard Left/Right for runtime variant switching and removed those keys from the semantic keyboard fallback navigation path.
- Added three distinct layouts: Sinmai-like carousel, Majdata-native vertical list hybrid, and compact fast-flow hybrid.
- Added a large `VARIANT N/3` label to every rendered variant.
- Verified `test-unit.ps1`, `install.ps1`, and a real MajdataPlay launch after the variant renderer was connected. Synthetic Windows `SendKeys` did not produce a variant-switch log in the launch check, so runtime switching evidence is from code path plus unit tests rather than an observed keypress in the game window.

## Blocked by

- .scratch/ui-prototype-template-mod/issues/04-render-song-first-fake-selection-flow.md
