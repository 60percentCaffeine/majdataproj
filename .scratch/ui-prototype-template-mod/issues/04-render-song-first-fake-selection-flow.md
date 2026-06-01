# Render Song-First Fake Selection Flow

Status: ready-for-agent

## Parent

.scratch/ui-prototype-template-mod/PRD.md

## What to build

Render the first useful fake-screen prototype for the Sinmai-style song-first selection flow. The screen should make songs the primary browsing unit, show selected song details, show difficulty as secondary context while browsing, and transition into a dedicated difficulty selection phase after OK.

This slice should be demoable in the running MajdataPlay window: navigate songs, lock a song, choose difficulty, confirm, and back out while preserving context.

## Acceptance criteria

- [ ] Song selection renders a fake song carousel or chain list with a clearly selected song.
- [ ] The selected song area shows representative title, artist, BPM, category/genre, difficulty chips, score/rank-like data, and special flags where applicable.
- [ ] In song selection, A3/A6 or equivalent semantic actions browse songs.
- [ ] In song selection, OK transitions to difficulty selection.
- [ ] Difficulty selection keeps the selected song visible while making difficulty selection visually dominant.
- [ ] In difficulty selection, A3/A6 or equivalent semantic actions change difficulty.
- [ ] In difficulty selection, Back returns to song selection with the selected song preserved.
- [ ] Visible button affordances change by phase so the current meaning of controls is clear.
- [ ] The prototype renders current phase, selected song, selected difficulty, and relevant diagnostics.

## Blocked by

- .scratch/ui-prototype-template-mod/issues/02-add-pure-prototype-core-and-state-tests.md
- .scratch/ui-prototype-template-mod/issues/03-wire-semantic-input-actions.md
