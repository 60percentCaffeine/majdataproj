# Render Song-First Fake Selection Flow

Status: done

## Parent

.scratch/ui-prototype-template-mod/PRD.md

## What to build

Render the first useful fake-screen prototype for the Sinmai-style song-first selection flow. The screen should make songs the primary browsing unit, show selected song details, show difficulty as secondary context while browsing, and transition into a dedicated difficulty selection phase after OK.

This slice should be demoable in the running MajdataPlay window: navigate songs, lock a song, choose difficulty, confirm, and back out while preserving context.

## Acceptance criteria

- [x] Song selection renders a fake song carousel or chain list with a clearly selected song.
- [x] The selected song area shows representative title, artist, BPM, category/genre, difficulty chips, score/rank-like data, and special flags where applicable.
- [x] In song selection, A3/A6 or equivalent semantic actions browse songs.
- [x] In song selection, OK transitions to difficulty selection.
- [x] Difficulty selection keeps the selected song visible while making difficulty selection visually dominant.
- [x] In difficulty selection, A3/A6 or equivalent semantic actions change difficulty.
- [x] In difficulty selection, Back returns to song selection with the selected song preserved.
- [x] Visible button affordances change by phase so the current meaning of controls is clear.
- [x] The prototype renders current phase, selected song, selected difficulty, and relevant diagnostics.

## Completion notes

- Replaced the static placeholder canvas with a song-first prototype canvas driven by `PrototypeSession`.
- Rendered previous/current/next song panels, selected song details, category, artist, BPM, long/special/badge metadata, difficulty chips, rank/DX-score-like values, phase panel, prompts, and diagnostics.
- Added a synchronization-context timer pump so input/state refresh does not depend only on MelonLoader `OnUpdate` in this patched runtime.
- The same semantic input path from issue 03 drives song browsing, OK transition, difficulty changes, confirmation, and Back navigation.
- Verified `test-unit.ps1`, `install.ps1`, and a real MajdataPlay launch. The launch logged `UI prototype song-first canvas installed`, `Prototype input timer reached Unity main thread`, `UI prototype placeholder rendering through IMGUI`, and `UI prototype overlay behaviour is ticking`.

## Blocked by

- .scratch/ui-prototype-template-mod/issues/02-add-pure-prototype-core-and-state-tests.md
- .scratch/ui-prototype-template-mod/issues/03-wire-semantic-input-actions.md
