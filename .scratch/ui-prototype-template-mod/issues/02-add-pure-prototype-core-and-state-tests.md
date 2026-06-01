# Add Pure Prototype Core And State Tests

Status: ready-for-agent

## Parent

.scratch/ui-prototype-template-mod/PRD.md

## What to build

Add a Unity-free prototype core for the Sinmai-style song selection experiment. The core should own fake song data, selected song state, selected difficulty state, phase routing, and state transitions. It should be usable from the MelonLoader adapter but testable without launching Unity or MajdataPlay.

The first scenario should follow this state machine from the PRD:

```text
SongSelect
  A3 / next action: selectedSongIndex += 1
  A6 / previous action: selectedSongIndex -= 1
  A4 / OK action: lock selected song -> DifficultySelect
  A5 / Back action: previous/category placeholder

DifficultySelect
  A3 / harder action: selectedDifficulty += 1
  A6 / easier action: selectedDifficulty -= 1
  A4 / OK action: confirm -> Confirmed or prototype follow-up screen
  A5 / Back action: return to SongSelect with selectedSongIndex preserved

Confirmed
  A5 / Back action: return to DifficultySelect
```

## Acceptance criteria

- [ ] The prototype core can be built and tested without Unity or MajdataPlay runtime initialization.
- [ ] Fake song data includes multiple categories, songs, difficulties, availability states, score/rank-like values, and special flags.
- [ ] Song navigation updates the selected song according to the chosen wrap or clamp behavior.
- [ ] OK from song selection moves to difficulty selection while preserving the selected song.
- [ ] Back from difficulty selection returns to song selection with the selected song preserved.
- [ ] Difficulty changes respect the valid/available difficulty range.
- [ ] Unit tests cover the state machine's externally visible behavior.

## Blocked by

- .scratch/ui-prototype-template-mod/issues/01-scaffold-runnable-prototype-mod.md
