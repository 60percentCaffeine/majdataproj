# PRD: Template Mod For UI Prototyping

Status: ready-for-agent

## Problem Statement

MajdataPlay mod UI work is hard to evaluate quickly because the real game flow, scene objects, input handling, and MelonLoader runtime all sit between an idea and the moment where the UI can be felt on cabinet-style controls. The user wants a fast way to prototype UI screens for a MajdataPlay MelonLoader mod, launch the game, block normal gameplay, and explore screen variants using the same controls as the game.

The immediate design question is whether Majdata should adopt a Sinmai-like song selection flow where the player selects a song first, presses OK, and then selects difficulty, instead of Majdata's current chart/difficulty-centered selection flow. The user needs to feel this flow in-game without committing to a production rewrite of Majdata's list scene.

## Solution

Build a reusable template MelonLoader mod for quick UI prototyping. The template mod launches inside MajdataPlay, takes over the visual experience, reads the game's button and touch inputs where possible, and renders fake/mock screens through a lightweight prototype runtime.

The first scenario implemented with the template will be a fake song selection prototype that models a Sinmai-style flow:

1. Browse songs as the primary list item.
2. Press OK to lock the selected song.
3. Transition into a dedicated difficulty selection phase.
4. Press OK again to confirm difficulty.
5. Press Back to return to the previous phase with context preserved.

The template should be reusable for later prototype screens, not hard-coded only for this one flow. It should let future agents add new prototype screens and variants with a small state object, an update function, and a draw function.

## User Stories

1. As a mod designer, I want to launch MajdataPlay and see the prototype UI instead of normal gameplay, so that I can evaluate UI concepts in the real runtime.
2. As a mod designer, I want normal gameplay to be blocked while the prototype mod is active, so that game systems do not distract from prototype evaluation.
3. As a mod designer, I want prototype screens to use the same button mappings as MajdataPlay, so that navigation feel matches the game.
4. As a mod designer, I want prototype screens to react to cabinet-style button input, so that I can judge whether interactions are comfortable.
5. As a mod designer, I want prototype screens to react to touch sensor input where supported, so that touch-driven UI concepts can be evaluated.
6. As a mod designer, I want a fallback keyboard input path, so that the prototype remains usable when the game input layer is unavailable.
7. As a mod designer, I want to switch between prototype variants at runtime with keyboard left/right arrows, so that I can compare layouts without rebuilding the mod.
8. As a mod designer, I want the current prototype state to be visible on screen, so that I can understand how inputs change the state.
9. As a mod designer, I want fake/mock screens for existing game flows, so that I can test screen transitions without safely hooking production game scenes.
10. As a mod designer, I want fake screens to preserve relevant context, so that returning from a new screen feels like returning to a real existing screen.
11. As a mod designer, I want a fake song selection screen, so that I can test alternative Majdata list navigation.
12. As a mod designer, I want song selection to treat the song as the primary item, so that the prototype matches Sinmai's song-first flow.
13. As a mod designer, I want difficulty to be secondary while browsing songs, so that I can test whether users understand the deferred difficulty decision.
14. As a mod designer, I want pressing OK on a song to transition into difficulty selection, so that I can feel the two-step selection flow.
15. As a mod designer, I want difficulty selection to keep the selected song visible, so that the user retains context after confirming a song.
16. As a mod designer, I want pressing Back in difficulty selection to return to the same selected song, so that back navigation feels stable.
17. As a mod designer, I want harder/easier controls to be phase-dependent, so that A3/A6 can browse songs in one phase and change difficulty in another.
18. As a mod designer, I want visible button affordances to change by phase, so that the UI communicates the current meaning of each control.
19. As a mod designer, I want prototype transitions between song browsing and difficulty selection, so that I can evaluate whether the mode shift feels natural.
20. As a mod designer, I want multiple visual variants of the Sinmai-style flow, so that I can compare a faithful Sinmai-like design against a Majdata-native hybrid.
21. As a mod designer, I want sample songs, categories, difficulties, scores, and badges in prototype data, so that the UI has realistic density.
22. As a mod designer, I want unavailable difficulties to appear disabled, so that the prototype covers locked or missing chart cases.
23. As a mod designer, I want long-song or special-song indicators in fake data, so that edge cases affect layout early.
24. As a mod designer, I want a category or genre indicator, so that the song carousel can model category boundaries.
25. As a mod designer, I want fake score/rank display per difficulty, so that deferred difficulty selection can still show useful song browsing information.
26. As a mod designer, I want the template to be easy to copy for a new prototype, so that future UI questions can be answered quickly.
27. As a mod designer, I want the prototype runtime to be separated from the MelonLoader adapter, so that most behavior can be tested outside the game.
28. As a mod designer, I want pure state transitions to be testable without Unity, so that input behavior can be verified quickly.
29. As a mod designer, I want rendering code to be disposable and clearly marked as prototype code, so that it does not become mistaken for production UI.
30. As a mod designer, I want one build/install command for the prototype mod, so that iteration stays fast.
31. As a mod designer, I want clear logs when the prototype mod activates, so that I can confirm the right mod is running.
32. As a mod designer, I want the prototype to avoid Harmony by default, so that it works with the current constrained MelonLoader setup.
33. As a mod designer, I want the template to use Unity IMGUI for the first pass, so that screens can be changed quickly without prefabs or asset bundles.
34. As a mod designer, I want the template to support fixed virtual layout coordinates, so that rhythm-game screen proportions stay consistent.
35. As a mod designer, I want the prototype to scale to the current game window, so that it remains usable across common resolutions.
36. As a mod designer, I want a simple overlay showing current phase, selected song, selected difficulty, and active variant, so that state changes are obvious during testing.
37. As a mod designer, I want a giant on-screen label such as `VARIANT 1/3`, so that I always know which prototype variant I am evaluating.
38. As a mod designer, I want the prototype to be removable by deleting the mod DLL, so that normal MajdataPlay behavior is easy to restore.
39. As a future implementing agent, I want the template to document how to add a new screen, so that future prototypes are cheaper to build.
40. As a future implementing agent, I want the input abstraction to expose semantic controls instead of raw Unity keys, so that screen logic is independent of physical input source.
41. As a future implementing agent, I want the first scenario to demonstrate the intended architecture, so that later prototypes follow a known pattern.

## Implementation Decisions

- Build a new template MelonLoader mod rather than modifying MajdataPlay production scenes.
- Treat the prototype mod as disposable tooling. It should be clearly named and documented as prototype-only.
- Keep normal game functionality blocked while the prototype is active. The first implementation should freeze or hide game scene content sufficiently for UI evaluation, without relying on Harmony patches.
- Avoid Harmony for the initial implementation because this repository's current MelonLoader v0.4.3 setup has several Harmony-dependent startup paths disabled or unreliable.
- Use a plain MelonLoader adapter with lifecycle hooks for startup, per-frame input/state updates, GUI rendering, and shutdown.
- Use Unity IMGUI for the first version. It is fast enough for throwaway UI layout exploration and avoids Unity prefab or asset-bundle work.
- Use a fixed virtual canvas for prototype rendering, scaled to the actual game window. The virtual coordinate system should make UI proportions stable across resolutions.
- Build a small pure core module that owns prototype data, state transitions, and screen routing. The MelonLoader adapter should be thin.
- Build an input adapter with a stable semantic interface. Prototype screens should ask for actions such as `SongNext`, `SongPrevious`, `Ok`, `Back`, `DifficultyUp`, and `DifficultyDown`, not direct keyboard or enum calls.
- Prefer reading MajdataPlay's centralized input layer by reflection so the prototype can use the same button and touch state as the game while avoiding compile-time access problems with internal types.
- Include a fallback input adapter using Unity keyboard input with equivalent mappings for development and failure recovery.
- Include visible diagnostics for the active input source so users know whether the prototype is using real game input or fallback input.
- Represent prototype screens as stateful modules with a small interface: enter, update with input, draw, and expose debug state.
- Include a prototype router that can switch between screens and visual variants without rebuilding.
- Variant switching must use keyboard left/right arrow keys, independent of the Majdata/cabinet input mapping, so variant comparison does not interfere with the prototype flow being tested.
- The active variant must be displayed with a very large, unmistakable on-screen label in the format `VARIANT N/TOTAL`, for example `VARIANT 1/3`.
- Include a first scenario named around the Sinmai-style song selection experiment.
- Model the first scenario with the following state machine:

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

- Preserve selected song when moving from song selection to difficulty selection and when backing out of difficulty selection.
- Preserve selected difficulty per song where practical, so returning to a song can restore the last explored difficulty.
- The fake song selection screen should render a horizontal song carousel or chain list, a selected-song detail region, category/genre context, difficulty chips, and button prompts.
- The fake difficulty selection screen should visually retain the selected song while making difficulty selection the dominant interaction.
- Provide exactly three initial visual variants for the first scenario: Sinmai-like, Majdata-native hybrid, and compact/fast-flow hybrid.
- Use representative fake data rather than live chart storage for the first pass. The fake data should include categories, songs, artists, BPM, difficulties, availability, ranks, DX score-like values, and special flags.
- Keep integration with real Majdata chart storage out of the first implementation unless it becomes necessary for feel-testing.
- Use build and install scripts consistent with the existing sample mod pattern.
- Reference only the Unity and MelonLoader assemblies needed by the prototype mod. Expected Unity dependencies include core, IMGUI, legacy input, and audio modules if audio pause/mute is used.
- The first pass does not need production-quality art. Use colored panels, text, layout, and simple generated shapes to evaluate flow and hierarchy.
- Add a short README explaining how to build, install, run, switch variants, and remove the prototype mod.

## Testing Decisions

- Test external behavior of the pure prototype core, not Unity drawing implementation details.
- Unit test the prototype state machine: song navigation wraps or clamps as designed, OK moves from song selection to difficulty selection, Back returns with context preserved, and difficulty changes respect available ranges.
- Unit test the semantic input mapping: raw input snapshots should produce the expected actions for OK, Back, song next/previous, and difficulty up/down.
- Unit test variant routing: keyboard left/right variant switching should cycle among the three variants and should not reset the selected song or selected difficulty unless explicitly requested.
- Unit test fake data edge cases: missing difficulties, locked difficulties, long-song flags, and category boundaries.
- Use the existing sample mod's pure core test style as prior art for testing non-Unity logic outside the game.
- Add a small integration smoke test only if practical with the existing mod test harness: build/install the prototype mod, launch MajdataPlay, and confirm the prototype mod logs activation without fatal boot errors.
- Avoid screenshot-based layout tests in the first implementation. The value of the prototype is interactive feel, and the rendering layer is intentionally throwaway.
- Do not test private rendering helper calculations unless they become part of a stable layout API.

## Out of Scope

- Rewriting MajdataPlay's production song list scene.
- Hooking or patching real MajdataPlay scenes with Harmony.
- Building production Unity UI prefabs or asset bundles.
- Loading real song jackets or chart databases into the first prototype.
- Implementing the final Sinmai-style flow in production code.
- Supporting two-player synchronized selection behavior.
- Implementing online, login, matching, ghost, rival, challenge, tournament, or mission behavior beyond fake visual placeholders.
- Creating final art, animation polish, or sound design.
- Persisting prototype settings or user choices.
- Supporting mobile platforms.

## Further Notes

Sinmai's reference flow separates selection into explicit sub-sequences: category/genre selection, music selection, difficulty selection, and menu/start. The prototype should borrow the interaction shape rather than trying to port Sinmai code.

In Sinmai, music browsing uses A3/A6 for song navigation, A4 for OK, and A5 for Back. Difficulty selection then reuses A3/A6 for difficulty changes, with A4 confirming and A5 returning to music selection. The key design question for Majdata is whether this phase-dependent remapping feels better than the current combined chart selection flow.

The current repository already has a minimal MelonLoader sample mod and a real-game test harness. The prototype template should follow those conventions closely while keeping its reusable state machine isolated enough to test without Unity.
