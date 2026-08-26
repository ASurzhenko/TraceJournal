# Trace Journal — Decision Log

This is a living record of consequential project choices. Each accepted entry
states the choice, why it was made, what it costs, and what would justify
changing it. Proposals are not implementation instructions until accepted.

## Accepted decisions

### D-001 — Unity version and template

- **Choice:** Unity `6000.3.5f2`, Universal 2D.
- **Why:** The application is a portrait 2D/mobile experience and this is the
  owner's current verified Unity 6 editor line.
- **Cost:** URP adds packages and configuration that a mostly-uGUI application
  uses only lightly.
- **Revisit if:** The challenge owner requires a different editor line.

### D-002 — Android baseline

- **Choice:** Android 9.0+ (API 28), portrait, IL2CPP, ARMv7 + ARM64,
  application ID `com.asurzhenko.tracejournal`.
- **Why:** This directly matches the delivery target while retaining broad
  Android device coverage.
- **Cost:** Native Android integrations and device verification remain required.
- **Revisit if:** A required image-acquisition route has a documented platform
  constraint that changes the supported-device matrix.

### D-003 — UI stack

- **Choice:** uGUI + TextMeshPro.
- **Why:** It is a low-risk, well-understood route for a compact Unity mobile UI.
- **Cost:** Responsive layout, keyboard, safe-area and dynamic-list behaviour
  need explicit care.
- **Revisit if:** Discovery proves a concrete requirement that UI Toolkit serves
  substantially better.

### D-004 — Repository and branch flow

- **Choice:** Keep the challenge repository private during development. Work
  flows `main` ← `dev` ← one bounded branch per package.
- **Why:** The source brief is confidential, while visible package boundaries
  make the development process easy to review.
- **Cost:** Access must be granted explicitly and merges add modest process
  overhead.
- **Revisit if:** The recipient explicitly requests a public repository or a
  different handoff mechanism.

### D-005 — Planning and implementation boundary

- **Choice:** Use a fast `taskplan` → one or two review rounds → owner gate →
  implementation cadence. Planning/review sessions do not implement product code.
- **Why:** This preserves an independent review surface without turning a small
  challenge into a long process exercise.
- **Cost:** Code changes wait for a separate, bounded implementation handoff.
- **Revisit if:** A package is purely mechanical and the owner explicitly changes
  the workflow.

### D-006 — Clarification email

- **Choice:** Do not use the single permitted pre-coding question email by default.
- **Why:** The underspecified areas are an opportunity to demonstrate ownership
  through explicit, defensible decisions.
- **Cost:** Assumptions and their trade-offs must be documented rigorously.
- **Revisit if:** Discovery finds a genuine external blocker that cannot be
  resolved safely from the supplied brief.

### D-007 — Dependencies and development tooling

- **Choice:** Keep production dependencies minimal and justified. Unity MCP is
  local development tooling and will not be part of the submitted project.
- **Why:** Reviewer setup must remain reproducible and unrelated agent tooling
  must not become a build dependency.
- **Cost:** The local package must be removed and the project reverified before
  release.
- **Revisit if:** Never for submission tooling; runtime libraries still require
  an explicit architecture decision.

## Decisions pending discovery

- Backend, database, hosting and media-storage stack.
- Local persistence format and migration boundary.
- Camera capture, gallery upload, or both.
- Record lifecycle beyond create/list.
- Remote-delivery and duplicate-prevention semantics.
- Server-side CSV schema, escaping and access route.
- The single product differentiator and its explicit cut line.
- Automated and device-test boundaries.
