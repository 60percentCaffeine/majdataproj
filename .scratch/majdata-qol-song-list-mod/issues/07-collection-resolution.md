Status: ready-for-agent

# Website collection resolution

## What to build

Support website collection summaries and hash-list contents as virtual folders. Collection entries should resolve by hash against the unified catalog, prefer downloaded playback for duplicates, include online-only charts when online browsing is enabled, and report unresolved counts.

## Acceptance criteria

- [ ] User-owned and subscribed/favorited collection DTOs convert into collection summaries.
- [ ] Collection hash contents resolve against local-only, online-only, and duplicate rows.
- [ ] Unresolved collection entries are omitted from playable rows but reflected in resolved versus total counts.
- [ ] Online-disabled scope hides or degrades online-only collection rows cleanly.
- [ ] Unit tests cover local-only, online-only, duplicate, unresolved, and mixed-scope cases.

## Blocked by

- 02-catalog-row-and-index-deduplication
- 06-majdatanet-adapter-and-random-recommend
