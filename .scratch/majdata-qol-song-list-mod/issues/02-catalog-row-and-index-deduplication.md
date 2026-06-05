Status: completed

# Catalog row and index deduplication

## What to build

Add the unified catalog row model and `CatalogIndex` behavior for local and online chart identities. The index should deduplicate by hash, prefer downloaded/local playback when both sources exist, and retain online metadata for collections, recommendations, and interaction facts.

## Acceptance criteria

- [x] Catalog rows include hash, title, artist, uploader/designer fields, levels, timestamp, source, optional online id, optional local folder, score facets, interaction facets, collection membership, and hydration state.
- [x] Local-only, online-only, and duplicate local-plus-online inputs produce the expected unified rows.
- [x] Local assets are marked preferred when a duplicate hash has both local and online data.
- [x] Online metadata remains attached to duplicate downloaded rows.
- [x] Tests cover missing/blank fields without dropping rows.

## Blocked by

- 01-scaffold-production-mod-and-core-defaults

## Comments

- 2026-06-05: Implemented `CatalogInput`, `CatalogRow`, `CatalogIndex`, levels, source/preferred-playback state, score facets, interaction facets, collection memberships, and hydration state. Local and online rows deduplicate by normalized hash; duplicate local-plus-online rows prefer local playback while retaining online id/uploader/interaction/collection metadata. Verification: `test-unit.ps1` passed 11/11 tests; `build.ps1` passed; `install.ps1` installed the updated mod/core DLLs.
