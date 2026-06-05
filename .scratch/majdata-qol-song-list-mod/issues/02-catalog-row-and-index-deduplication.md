Status: ready-for-agent

# Catalog row and index deduplication

## What to build

Add the unified catalog row model and `CatalogIndex` behavior for local and online chart identities. The index should deduplicate by hash, prefer downloaded/local playback when both sources exist, and retain online metadata for collections, recommendations, and interaction facts.

## Acceptance criteria

- [ ] Catalog rows include hash, title, artist, uploader/designer fields, levels, timestamp, source, optional online id, optional local folder, score facets, interaction facets, collection membership, and hydration state.
- [ ] Local-only, online-only, and duplicate local-plus-online inputs produce the expected unified rows.
- [ ] Local assets are marked preferred when a duplicate hash has both local and online data.
- [ ] Online metadata remains attached to duplicate downloaded rows.
- [ ] Tests cover missing/blank fields without dropping rows.

## Blocked by

- 01-scaffold-production-mod-and-core-defaults
