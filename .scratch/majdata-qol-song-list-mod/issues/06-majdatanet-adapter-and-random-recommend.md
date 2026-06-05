Status: completed

# MajdataNet adapter and Random Recommend fetch

## What to build

Add `MajdataNetAdapter` for public chart-list DTO conversion and implement random recommendation batches by fetching the public chart list and deterministic shuffling, with configured local fallback behavior when online fetch fails.

## Acceptance criteria

- [ ] Chart-list JSON converts into catalog-compatible online rows.
- [ ] Random recommendations can be built from unauthenticated chart-list data.
- [ ] Recommendation batches are deterministic for a supplied seed but varied for different seeds.
- [ ] Network failures are non-fatal and return a recoverable result.
- [ ] Configured local fallback produces recommendations from local catalog rows.
- [ ] Unit tests use representative JSON fixtures and failure cases.

## Blocked by

- 02-catalog-row-and-index-deduplication

## Comments

- 2026-06-05: Implemented `MajdataNetAdapter`, `ITextFetcher`/`HttpTextFetcher`, recoverable `MajdataNetResult<T>`, and `RandomRecommendationService`. The adapter converts representative public `/api/maichart/list` JSON into online catalog inputs, fetches the unauthenticated chart-list endpoint, builds deterministic random recommendation batches from online rows, treats network failures as recoverable, and supports configured local fallback batches. Verification: `test-unit.ps1` passed 59/59 tests; `build.ps1` passed; `install.ps1` installed the updated mod/core DLLs.
