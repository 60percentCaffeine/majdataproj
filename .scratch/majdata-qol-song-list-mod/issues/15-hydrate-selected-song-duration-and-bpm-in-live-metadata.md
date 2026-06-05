Status: ready-for-agent

# Hydrate selected-song duration and BPM in live metadata

## What to build

Replace the live selected-song metadata fallback values with real duration and BPM data when they are available, while preserving quiet pending/unknown display when data has not been hydrated yet. The metadata line should continue to use the prototype-approved format and layout.

The implementation should use the existing hydration/cache concepts so chart data improves outside gameplay without blocking list startup or causing the visible list to jump while the player browses.

## Acceptance criteria

- [ ] Selected-song metadata displays a real song length when a reliable nonblocking runtime source is available, and falls back to `--:--` only when length is genuinely unknown.
- [ ] Selected-song metadata displays hydrated BPM from local or online chart data when available.
- [ ] Pending and unknown BPM states remain explicit and do not look like confirmed zero or confirmed data.
- [ ] BPM hydration does not run during gameplay/practice and can resume in list/menu/settings scenes.
- [ ] Hydrated metadata updates do not force an immediate live resort or cursor jump while the player is browsing.
- [ ] In-game canaries prove at least one selected song can show hydrated BPM or a controlled hydrated test value in the metadata line.
- [ ] Unit tests and in-game smoke/canaries still pass.

## Blocked by

None - can start immediately
