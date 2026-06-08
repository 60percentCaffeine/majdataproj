Status: ready-for-agent

# Route folder-select Back/Login and inactivity logout through Login Screen

## Parent

.scratch/local-profiles/PRD.md

## What to build

Update the existing song-list account-switching paths to use the new Login Screen path. From song folder select, the existing Back/Login behavior should return through the Login Screen where `Local Account` is available. Chart-list Back behavior should remain unchanged and return to folder select first.

When the existing inactivity logout timer activates, the game should save the active player, unmount/end the active player session, follow current online logout behavior for online accounts, and return through the Login Screen path.

## Acceptance criteria

- [ ] Chart-list Back still returns to folder select first.
- [ ] Folder-select Back/Login follows the existing transition pattern but returns to the updated Login Screen path.
- [ ] Inactivity timeout saves the active player session before leaving the song list.
- [ ] Inactivity timeout unmounts a local profile so the next player does not continue on it accidentally.
- [ ] Inactivity timeout follows current online logout/unload behavior for majdata.net Account mode.
- [ ] Inactivity timeout returns to Login Screen, not directly to Local Profile Selection and not automatically to Guest.
- [ ] Loading/empty transition text clearly indicates account switching work where practical.
- [ ] Unit tests cover route decisions for chart-list Back, folder-select Back/Login, Guest timeout, Local Profile timeout, and online timeout.
- [ ] Integration tests verify folder-select Back/Login and inactivity timeout reach the Login Screen without corrupting active profile data.

## Blocked by

- .scratch/local-profiles/issues/05-scope-myfavorites-to-active-local-profile.md
- .scratch/local-profiles/issues/06-scope-scores-and-result-saving-to-active-local-profile.md
- .scratch/local-profiles/issues/09-persist-profile-wide-settings-separately-system-wide-globally.md
- .scratch/local-profiles/issues/11-keep-majdatanet-and-local-profile-sessions-mutually-exclusive.md
