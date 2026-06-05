Status: ready-for-agent

# Sort core

## What to build

Implement pure sorting for existing Majdata modes plus artist, play count, BPM, AP/FC rank, DX score, and release/upload time where data exists. Unknown values should sort after known values for descending progress-style sorts, and unknown online play count must remain distinct from confirmed zero.

## Acceptance criteria

- [ ] Sort option set matches the UI proposal and omits unsupported Sinmai-only modes.
- [ ] Artist/title/release sorting are deterministic with stable tie-breakers.
- [ ] Local play count sorts immediately when available.
- [ ] Unknown online play count is distinguishable from confirmed zero.
- [ ] BPM sorting uses known values and places unknown/pending after known values.
- [ ] AP/FC and DX score sorts degrade cleanly when no score data exists.
- [ ] Unit tests cover known, unknown, stale, and zero values.

## Blocked by

- 03-level-difficulty-and-grouping-core
