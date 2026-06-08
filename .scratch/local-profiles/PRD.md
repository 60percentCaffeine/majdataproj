# PRD: Local Profiles

Status: ready-for-agent

## Problem Statement

MajdataPlay currently supports Guest play and majdata.net login, but it does not provide simple local accounts for shared offline play. On a shared setup, players cannot choose a local identity and have their favorites, scores, and player settings stay separate without using majdata.net.

The user wants a local profiles system that does not require majdata.net. Anybody should be able to create a local profile by entering a valid unique name, select any saved local profile on the machine, and play with favorites, scores, and profile-wide settings saved to that local profile. Guest play must remain available with the same behavior as today. majdata.net login must remain available, but online account play must not affect local profile data.

## Solution

Add a local-account branch to the existing login flow while preserving the current MajdataPlay screen language and controls.

From the player's perspective:

- The current `Login Screen` remains the entry point for account choice.
- The current Guest action on the `Login Screen` is replaced by `Local Account`.
- Choosing `Local Account` opens `Local Profile Selection`.
- `Local Profile Selection` resembles the current folder selection screen, but folder names are account names.
- The first account-list entry is always `Create New`.
- The second account-list entry is always `Guest`.
- Remaining entries are local profiles saved on this machine.
- Choosing `Create New` opens `Create Local Profile`.
- Choosing `Guest` starts Guest play with the exact current Guest behavior.
- Choosing a local profile loads that profile and starts local-profile play.
- `Create Local Profile` is based on the current Search Songs screen, removes sort selection, focuses the text input, and shows a tip to use the keyboard.
- Empty names are rejected.
- Duplicate names are rejected after trimming whitespace and comparing case-insensitively.
- New local profiles start empty; no Guest data migration is shown or performed.
- The existing account display UI is reused for Guest, local profiles, and majdata.net accounts.
- Result screens clearly show whether the result was saved to Guest, a local profile, or a majdata.net account.
- Settings descriptions always append either `(profile-wide)` or `(system-wide)` with no additional explanatory text.

The three active player modes for MVP are mutually exclusive:

- `Guest`
- `Local Profile`
- `majdata.net Account`

There is no `Local Profile + majdata.net` mode in MVP.

## User Stories

1. As a shared MajdataPlay player, I want to create a local profile without majdata.net, so that my play data can be separated from other players on the same machine.
2. As a shared MajdataPlay player, I want to select my local profile before play, so that my scores, favorites, and profile-wide settings load for the session.
3. As a shared MajdataPlay player, I want Guest play to remain available, so that people can play without creating a profile.
4. As a Guest player, I want Guest play to behave exactly as it does today, so that the feature does not surprise existing users.
5. As a majdata.net user, I want online login to remain available, so that I can keep using existing online-account behavior.
6. As a local-profile player, I want local play to require no network or majdata.net login, so that I can play offline.
7. As a player on the Login Screen, I want the old Guest button/action to be replaced by Local Account, so that local and Guest play are reached through one offline account path.
8. As a player on the Login Screen, I want online login fields and QR/plain login behavior to remain familiar, so that the online flow is not redesigned.
9. As a player on the Login Screen, I want selecting Local Account to open Local Profile Selection, so that I can choose between Guest, profile creation, and saved profiles.
10. As a player on Local Profile Selection, I want the screen to resemble folder selection, so that it feels native to MajdataPlay and works with the same physical-button mental model.
11. As a player on Local Profile Selection, I want account entries to appear where folder names normally appear, so that profile choice is easy to understand.
12. As a player on Local Profile Selection, I want Create New to be the first entry, so that making a local profile is discoverable.
13. As a player on Local Profile Selection, I want Guest to be the second entry, so that Guest play remains easy to reach.
14. As a player on Local Profile Selection, I want saved profiles to appear after Create New and Guest, so that existing local accounts are visible without hiding Guest.
15. As a player on Local Profile Selection, I want saved profiles to be listed by account name, so that I can recognize my profile quickly.
16. As a player on Local Profile Selection, I want pressing Back to return to the Login Screen, so that I can choose online login or leave the local-account branch.
17. As a player on Local Profile Selection, I want timeout to return to the Login Screen, so that the game resets to the familiar login entry point after inactivity.
18. As a player on Local Profile Selection, I want selecting Create New to open Create Local Profile, so that I can make a new empty account.
19. As a player on Local Profile Selection, I want selecting Guest to immediately start Guest play, so that temporary players can skip account creation.
20. As a player on Local Profile Selection, I want selecting an existing profile to immediately start profile play, so that returning players can begin quickly.
21. As a player creating a profile, I want the Create Local Profile screen to be based on the Search Songs screen, so that keyboard text entry feels consistent with existing UI.
22. As a player creating a profile, I want sort selection removed from the Create Local Profile screen, so that the screen is focused only on entering a name.
23. As a player creating a profile, I want a visible tip that says to use the keyboard, so that I understand how to enter my name.
24. As a player creating a profile, I want the name field focused by default, so that I can start typing immediately.
25. As a player creating a profile, I want submitting an empty name to be rejected, so that unusable blank account entries are never created.
26. As a player creating a profile, I want spaces-only names to be rejected, so that the account list does not contain invisible names.
27. As a player creating a profile, I want leading and trailing spaces ignored for validation, so that accidental whitespace does not create confusing names.
28. As a player creating a profile, I want duplicate names to be rejected, so that every account name in the list identifies exactly one profile.
29. As a player creating a profile, I want duplicate checks to be case-insensitive, so that `Alice` and `alice` are not treated as different accounts.
30. As a player creating a profile, I want a clear validation error for an empty name, so that I know what to fix.
31. As a player creating a profile, I want a clear validation error for a duplicate name, so that I know why creation failed.
32. As a player creating a profile, I want a valid unique name to create an empty profile, so that new local accounts do not inherit Guest or other profile data.
33. As a player creating a profile, I want successful creation to load the new profile and start play, so that I do not need to select it again.
34. As a player creating a profile, I want pressing Back to cancel creation and return to Local Profile Selection, so that mistakes are easy to exit.
35. As a local-profile player, I want my favorites to be saved in my selected profile, so that another player changing favorites does not change mine.
36. As a local-profile player, I want MyFavorites to be rebuilt from my selected profile, so that the favorite folder reflects my account.
37. As a local-profile player, I want adding a favorite to affect only my selected profile, so that shared machines remain safe for multiple players.
38. As a local-profile player, I want removing a favorite to affect only my selected profile, so that I do not remove another player's favorite.
39. As a local-profile player, I want local scores to be saved in my selected profile, so that my results do not overwrite or mix with another player's results.
40. As a local-profile player, I want score display on the song list to read from my selected profile, so that rank, play count, DX score, and FC/AP state are mine.
41. As a local-profile player, I want score sorting and grouping to use my selected profile's scores, so that score-driven browsing reflects my history.
42. As a local-profile player, I want result saving to update my selected profile only, so that local-account data is isolated.
43. As a local-profile player, I want profile-wide settings to save in my selected profile, so that my gameplay preferences follow my profile.
44. As a local-profile player, I want chart-specific settings to save in my selected profile, so that per-chart offsets and preferences are personal.
45. As a local-profile player, I want runtime list state that is considered profile-wide to save in my selected profile, so that returning to my profile can restore my browsing context.
46. As a Guest player, I want favorites to keep using the current Guest behavior, so that Guest data is not migrated or redirected unexpectedly.
47. As a Guest player, I want scores to keep using the current Guest behavior, so that existing Guest score behavior remains intact.
48. As a Guest player, I want settings to keep using the current Guest behavior, so that Guest play remains compatible with the existing setup.
49. As a majdata.net player, I want logging into majdata.net to unselect any local profile, so that online-account play cannot write to local-profile storage.
50. As a majdata.net player, I want online scores and uploads to remain part of the online account flow, so that majdata.net behavior is preserved.
51. As a majdata.net player, I want online play to avoid modifying local profile scores, favorites, or settings, so that online sessions do not corrupt local accounts.
52. As a local-profile player, I want selecting a local profile to unload online account state, so that local profile play is not accidentally linked to majdata.net.
53. As a player, I want exactly one active player mode at a time, so that it is clear where data will be saved.
54. As a player, I want the existing account display UI to show Guest, so that the current identity is always visible.
55. As a player, I want the existing account display UI to show the selected local profile name, so that I can confirm I am on the right account.
56. As a player, I want the existing account display UI to show when the active account is local, so that I know results are not online.
57. As a player, I want the existing account display UI to show when the active account is majdata.net, so that I know online account behavior is active.
58. As a player, I want result screens to say `Saved to Guest` when Guest saves a result, so that I understand the save target.
59. As a local-profile player, I want result screens to say the local profile name when saving, so that I know the result went to my local account.
60. As a majdata.net player, I want result screens to say when a result was uploaded or saved to the online account, so that the save target is explicit.
61. As a majdata.net player, I want result screens to distinguish local-only fallback from online upload when applicable, so that I do not assume a result was uploaded when it was not.
62. As a player in song folder select, I want the existing Back/Login behavior to open the Login Screen path, so that switching player uses the familiar current flow.
63. As a player in chart list mode, I want Back to keep returning to folder select first, so that existing song-list navigation is preserved.
64. As a player affected by the inactivity logout timer, I want the game to save and leave the active player session before returning to the Login Screen, so that the next player starts from a safe account choice.
65. As a local-profile player affected by the inactivity logout timer, I want my profile data saved and unmounted, so that later players cannot accidentally keep playing on my profile.
66. As a majdata.net player affected by the inactivity logout timer, I want the online session to follow the current logout behavior before returning to the Login Screen, so that online account state is cleared.
67. As a player, I want the loading/empty transition to show meaningful status while switching accounts, so that saving, logging out, and loading profiles are understandable.
68. As a player using Settings, I want every setting description to append `(profile-wide)` or `(system-wide)`, so that I can see each setting's storage scope.
69. As a Guest player using Settings, I want the same `(profile-wide)` or `(system-wide)` labels to appear, so that settings scope is always visible regardless of player mode.
70. As a local-profile player using Settings, I want the same current settings menu structure, so that local profiles do not require learning a new settings UI.
71. As a majdata.net player using Settings, I want the same scope labels to appear, so that online play uses the same settings presentation.
72. As a player using Settings, I do not want extra explanatory text after the scope label, so that descriptions remain compact.
73. As a cabinet/operator user, I want system-wide settings to remain shared, so that device and installation settings are not duplicated per local profile.
74. As a cabinet/operator user, I want profile-wide settings separated from system-wide settings, so that players can customize gameplay without breaking shared setup settings.
75. As a player, I want new profiles to start empty, so that account creation does not silently copy Guest, online, or another player's data.
76. As an existing user, I want no first-run migration prompt, so that updating the game/mod does not interrupt existing Guest play.
77. As an existing user, I want Guest data to stay where it is, so that installing local profiles does not move or reinterpret my current files.
78. As a player, I want profile loading failures to fail safely back to account selection with an error, so that corrupted profile data does not crash the game.
79. As a player, I want profile data writes to be durable, so that scores and favorites survive game shutdown.
80. As a player, I want switching accounts only from safe menu/list/login flows, so that profile storage cannot change during gameplay or result saving.
81. As a player, I want the Local Profile Selection account list to work with physical buttons, so that the feature remains usable on a cabinet-style setup.
82. As a player, I want Create Local Profile to use keyboard input only for name text, so that the physical-button UI remains simple and the screen mirrors Search Songs.
83. As a tester, I want observable save-target text and account-display text, so that account isolation can be verified in game without inspecting files.
84. As a tester, I want local profile files to have deterministic locations under a profile identifier, so that profile isolation can be tested reliably.
85. As a tester, I want invalid profile names to leave no partial profile behind, so that failed validation does not create hidden state.
86. As a tester, I want duplicate-name validation to be independent of account-list sorting, so that uniqueness remains stable if display order changes later.
87. As a player, I want local profile names to be displayed consistently after trimming, so that account names do not appear with accidental leading or trailing spaces.
88. As a player, I want online login cancellation to return to the Login Screen behavior that already exists, so that the new local-account path does not disrupt online cancellation.
89. As a player, I want Local Profile Selection timeout to return to Login Screen rather than starting Guest automatically, so that inactive users do not accidentally start a session.
90. As a player, I want no password or PIN on local profiles for MVP, so that any local user can select any local profile as requested.

## Implementation Decisions

- Build an `ActivePlayerSession` deep module that exposes the active player mode, display name, stable profile identity when applicable, and save target. This module should be the single source of truth for whether the game is in Guest, Local Profile, or majdata.net Account mode.
- Build a `LocalProfileStore` deep module that lists local profiles, validates new names, creates empty profiles, and resolves profile metadata. Its public behavior should include ordered account-list entries, empty-name rejection, duplicate-name rejection, trimmed display names, and case-insensitive uniqueness.
- Local profiles should have a stable internal identifier that is not the display name, even though duplicate display names are disallowed. The stable identifier prevents future rename support or filesystem-safe normalization from changing storage identity.
- New local profiles start with empty profile-owned data. They must not import or copy Guest data.
- Guest mode must keep the current Guest storage paths and behavior.
- majdata.net Account mode must not mount a local profile. Online login should clear or replace any active local profile session before user data is fetched or displayed.
- Selecting a local profile must clear online account/session display state and online-score state so that local profile play is not mixed with majdata.net behavior.
- Build a `ProfileScopedStorage` or equivalent deep module that maps profile-owned data to the current active player mode. Profile-owned local data includes scores, MyFavorites/favorite storage, profile-wide settings, chart-specific settings, and profile-wide runtime/list state.
- System-wide data remains shared across all modes. System-wide data includes device/IO configuration, online endpoint configuration, proxy/network endpoint configuration, installation-level cache roots, and other setup values that describe the machine or game installation rather than a player.
- Define a setting-scope catalog that assigns every visible setting description exactly one label: `(profile-wide)` or `(system-wide)`.
- The settings UI must append only the scope label. It must not add explanatory sentences.
- The settings UI must append scope labels regardless of Guest, Local Profile, or majdata.net Account mode.
- Scope labels should survive language changes and dynamic description refreshes, including descriptions that already append offset-unit text.
- The `Login Screen` remains based on the current login screen. The old Guest button/action is renamed/replaced with `Local Account` and opens Local Profile Selection instead of starting Guest directly.
- The online login portion of the `Login Screen` remains functionally the same for MVP.
- `Local Profile Selection` is a new screen based on the folder-selection visual model. It uses account entries instead of folder entries.
- `Local Profile Selection` orders entries as `Create New`, `Guest`, then saved local profiles on this machine.
- `Local Profile Selection` Back returns to the `Login Screen`.
- `Local Profile Selection` timeout returns to the `Login Screen`.
- `Create New` opens `Create Local Profile`.
- `Guest` starts Guest play with the current Guest behavior.
- Selecting a saved local profile loads that profile and enters the song list through the same loading/list initialization pattern used by current Guest or login flow.
- `Create Local Profile` is a new screen based on the Search Songs screen. It removes sort selection, keeps/focuses the text input, and displays a keyboard-use tip.
- `Create Local Profile` Back cancels profile creation and returns to `Local Profile Selection`.
- `Create Local Profile` submit validates the trimmed name. Invalid submissions keep the user on the screen and show an error. Valid submissions create an empty local profile, select it, and enter play.
- The existing user/account display UI should be reused for all modes. It should render Guest, Local Account plus profile name, or Online Account plus online username.
- Result UI should render save-target copy sourced from `ActivePlayerSession`, not from ad hoc checks in each result path.
- Song list score, rank, play count, DX score, and favorite display must read from the active storage scope.
- Favorite toggles must write to the active storage scope.
- Result saving must write to the active storage scope. In majdata.net Account mode, online upload behavior remains online-account behavior and must not write to local profile storage.
- Existing Back/Login behavior from song folder select should continue to use the current transition logic, but the destination path is the updated `Login Screen` with `Local Account` instead of direct Guest.
- Existing chart-list Back behavior remains unchanged and returns to folder select first.
- When the existing inactivity logout timer is activated, it should save the active mode, end/unmount the active player session as appropriate, and return through the updated `Login Screen` path.
- Account switching should only happen in login/list/menu-safe flows, not during gameplay or while a result save is in progress.
- Loading/empty transition copy may be updated to mention saving current player, logging out, loading profile, loading scores, and loading favorites.
- No migration prompt or first-run onboarding should be added for MVP.
- No local profile delete, rename, password, PIN, avatar, sync, or online-linking management is included for MVP.

## Testing Decisions

- Good tests should verify external behavior: account-list ordering, profile-name validation, active-mode transitions, storage isolation, visible UI labels, save-target text, and persistence across session reloads. Tests should avoid relying on private fields or exact implementation internals.
- `LocalProfileStore` should have pure unit tests for list ordering, empty-name rejection, whitespace trimming, case-insensitive duplicate rejection, successful creation, stable profile identity, and failed validation leaving no profile behind.
- `ActivePlayerSession` should have pure unit tests for mode transitions among Guest, Local Profile, and majdata.net Account, including clearing local profile state on online login and clearing online state on local profile selection.
- `ProfileScopedStorage` should have pure or filesystem-isolated tests proving that Guest, each local profile, and online-account mode do not write to local profile data accidentally.
- Favorite storage should be tested through behavior: adding/removing favorites in one local profile must not change Guest or another local profile.
- Score storage should be tested through behavior: saving a result in one local profile must not change Guest or another local profile.
- Settings storage should be tested through behavior: changing a profile-wide setting in one local profile must not change another local profile, while changing a system-wide setting remains shared.
- The setting-scope catalog should have tests ensuring every visible setting has exactly one label and that labels are only `(profile-wide)` or `(system-wide)`.
- Settings description presentation should have tests proving labels are appended in Guest, Local Profile, and majdata.net Account modes, without explanatory text.
- Screen-flow logic should have tests for Login Screen Local Account action, Local Profile Selection Back, Local Profile Selection timeout, Create New navigation, Guest start, saved-profile start, Create Local Profile Back, invalid submit, duplicate submit, and valid submit.
- Result save-target presentation should have tests for Guest, Local Profile, successful online upload, and online local-only fallback copy.
- Integration tests should use the existing in-game TestHookMod/harness pattern to verify real MajdataPlay scene flow where feasible.
- Integration canaries should verify that the updated Login Screen can open Local Profile Selection, that Local Profile Selection lists Create New and Guest first, and that Back returns to Login Screen.
- Integration canaries should verify that a profile can be created with keyboard text input, then appears in Local Profile Selection and can be selected.
- Integration canaries should verify that empty and duplicate profile names are rejected in game.
- Integration canaries should verify local favorite isolation by adding a favorite in one profile, switching to another profile or Guest, and observing that the favorite state differs.
- Integration canaries should verify local score isolation by writing or simulating a score under one profile and confirming another profile does not show it.
- Integration canaries should verify that online login does not mount or write to a local profile.
- Integration canaries should verify the result screen displays the correct save target for at least Guest and Local Profile modes.
- Screenshot checks should be used for UI confidence because the UI must resemble existing Majdata screens and remain usable in the lower circular play area.
- Prior art: pure core tests should follow the style used by the existing QoL song-list core tests, while real-game canaries should follow the existing mod-test-tools integration canary pattern.

## Out of Scope

- Local profile passwords or PINs.
- Allowing duplicate local profile names.
- Allowing empty or whitespace-only local profile names.
- Importing or migrating Guest data into a local profile.
- Any first-run migration or onboarding prompt.
- Local Profile plus majdata.net linked mode.
- Linking a local profile to a majdata.net account.
- Syncing local profiles to majdata.net or any other cloud service.
- Local profile rename, delete, avatar, ordering, or management beyond create/select.
- Replacing the entire Login Screen UI; only the Guest action changes to Local Account.
- Replacing the settings menu structure; only scope labels are appended to descriptions.
- Adding explanations for `(profile-wide)` or `(system-wide)` labels.
- Reworking song selection navigation beyond using the existing Back/Login and inactivity flows to reach the updated Login Screen path.
- Changing Guest storage behavior.
- Changing majdata.net account semantics except for ensuring it does not affect local profile data.

## Further Notes

- UI prototypes and screenshots should be used before finalizing the new screens, especially because the Local Profile Selection screen should feel like current folder selection and Create Local Profile should feel like current Search Songs without sort selection.
- The exact setting-scope catalog should be reviewed carefully because labels are shown for every player mode and every visible setting.
- File and database writes should be atomic or recoverable enough that a crash during save does not corrupt unrelated profiles.
- The implementation should prefer deep, testable modules for session state, profile metadata, storage routing, setting scope, and save-target presentation so that most behavior can be tested without launching Unity.
