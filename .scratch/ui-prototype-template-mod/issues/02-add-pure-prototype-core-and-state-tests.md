# Add Pure Prototype Core And State Tests

Status: done

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

- [x] The prototype core can be built and tested without Unity or MajdataPlay runtime initialization.
- [x] Fake song data includes multiple categories, songs, difficulties, availability states, score/rank-like values, and special flags.
- [x] Song navigation updates the selected song according to the chosen wrap or clamp behavior.
- [x] OK from song selection moves to difficulty selection while preserving the selected song.
- [x] Back from difficulty selection returns to song selection with the selected song preserved.
- [x] Difficulty changes respect the valid/available difficulty range.
- [x] Unit tests cover the state machine's externally visible behavior.

## Completion notes

- Added `UiPrototypeTemplateMod.Core` with fake data, phases, semantic actions, selected song state, selected difficulty state, and state transitions.
- Chose wrapping song navigation and clamped difficulty navigation that skips unavailable or locked difficulties.
- Added 7 xUnit tests covering fake data shape, song wrapping, OK/Back routing, difficulty bounds, confirmed back behavior, and per-song difficulty preservation.
- Updated the prototype mod build/install path to build and install `UiPrototypeTemplateMod.Core.dll`.
- Verified a real MajdataPlay launch resolves the support DLL and logs `Prototype core ready: phase=SongSelect song=MAJTITLE difficulty=Basic`.

## Blocked by

- .scratch/ui-prototype-template-mod/issues/01-scaffold-runnable-prototype-mod.md
