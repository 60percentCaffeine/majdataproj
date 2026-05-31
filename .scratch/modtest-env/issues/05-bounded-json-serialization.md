# Add bounded JSON serialization for eval results

Status: completed

## What to build

Make eval responses machine-readable for real Unity and CLR objects while enforcing finite depth, size, and timeout limits. Serialization should preserve useful data where practical and return structured serialization errors when values cannot be fully represented.

## Acceptance criteria

- [x] Primitive values, strings, arrays, dictionaries, and DTO-shaped objects serialize normally.
- [x] Object cycles are represented with reference markers instead of causing unbounded recursion.
- [x] Unsupported runtime values such as delegates, pointers, native handles, IntPtr, streams, tasks, and reflection objects are represented with pointer-like identifiers and string conversion where available.
- [x] Per-request maxDepth and maxResponseBytes overrides are enforced.
- [x] Serialization failures and over-limit responses return structured errors with phase `serialization`.

## Blocked by

- .scratch/modtest-env/issues/03-isolated-roslyn-eval.md
