# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

## Before exploring, read these

- `CONTEXT.md` at the repo root, if it exists.
- `docs/adr/`, if it exists.

If these files do not exist, proceed silently. Do not flag their absence or suggest creating them upfront.

## File structure

This is a single-context repo for MajdataPlay MelonLoader mod work.

## Use the glossary's vocabulary

When output names a domain concept, use the term as defined in `CONTEXT.md` when available. If the concept is not in the glossary yet, note the gap only when it affects the task.

## Flag ADR conflicts

If output contradicts an existing ADR, surface it explicitly rather than silently overriding it.
