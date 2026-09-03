# CLAUDE.md

## Mission

Improve this project safely.

## Rules

Read first.
Think first.
Plan first.

Never guess.
Never assume.
Never invent requirements.

## Preserve

- Architecture
- Database
- Business logic
- Existing features
- UI behavior
- Folder structure
- Naming conventions
- SQLite compatibility
- WinForms compatibility

## Never

- Delete working code.
- Break existing features.
- Rename files without request.
- Rename classes without request.
- Rename methods without request.
- Rename database tables or columns.
- Rewrite working code.
- Refactor unless necessary.
- Add unnecessary libraries.
- Change project structure.
- Change coding style.

## Editing

Make the smallest possible change.

Touch the fewest files.

Reuse existing code.

Keep performance in mind.

## Before Coding

1. Analyze.
2. Explain the plan briefly.
3. List affected files.
4. Mention risks.
5. Wait for approval if the change is large.

## After Coding

- Verify build.
- Verify compile errors.
- Verify references.
- Verify dependencies.
- Verify existing features still work.

## Response Style

Be concise.

Avoid unnecessary explanations.

Avoid repeating information.

Minimize token usage.

## Project

Language: C#

Framework: WinForms

Database: SQLite

Priority:

1. Stability
2. Correctness
3. Performance
4. Maintainability
5. UI

Never sacrifice stability for prettier code.

If something is unclear, ask instead of guessing.

## Critical

This is a production project.

Existing functionality is more important than new functionality.

If a requested change could affect existing behavior, stop and ask before changing.

Never remove a feature to implement another feature.

Never simplify by deleting code.

Always extend, never replace, unless explicitly instructed.