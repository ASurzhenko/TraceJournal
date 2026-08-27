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

- **Choice:** Use bounded implementation prompts followed by an independent review
  and targeted fixes. Broader planning was deliberately cut after the initial estimate.
- **Why:** This preserved a review surface while keeping the one-day fast track viable.
- **Cost:** Some production-hardening work and additional planning artifacts were cut.
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
  local development tooling and is not part of the submitted project. NativeGallery
  `1.9.4` is the single third-party runtime integration.
- **Why:** Reviewer setup must remain reproducible and unrelated agent tooling
  must not become a build dependency. A verified gallery bridge avoids a custom
  Android Java subsystem within the challenge schedule.
- **Cost:** NativeGallery adds a small MIT-licensed plugin surface; its original
  license and attribution must ship with the source.
- **Revisit if:** Never for submission tooling; runtime libraries still require
  an explicit architecture decision.

### D-008 — Append-only local persistence

- **Choice:** Store a versioned JSON index plus app-owned bounded JPEG files under
  `Application.persistentDataPath`; records are append-only.
- **Why:** This directly covers create/list/restart with a small, inspectable format.
  Temp-write replacement protects the index and stable UUIDs support remote retry.
- **Cost:** There is no edit/delete UX, conflict resolution or migration beyond the
  current schema boundary.
- **Revisit if:** The product requires long-lived upgrades, editing or larger datasets.

### D-009 — Gallery upload rather than camera capture

- **Choice:** Implement the specification's image-upload alternative using the
  on-device gallery; normalize the result immediately into app ownership.
- **Why:** The brief allows either capture or upload, and gallery input reduced native
  integration risk while preserving the complete record flow.
- **Cost:** The app does not launch the camera directly.
- **Revisit if:** Direct capture becomes an explicit requirement.

### D-010 — Supabase direct REST contour

- **Choice:** Use Supabase anonymous Auth, private Storage and PostgreSQL/PostgREST
  through direct HTTPS, with RLS scoped by `auth.uid()`.
- **Why:** One hosted surface satisfies remote database, image storage and CSV needs
  without a custom service or Unity backend SDK.
- **Cost:** Clearing app data loses the anonymous identity; there is no account recovery
  or multi-device merge.
- **Revisit if:** The application needs durable user identities or production support.

### D-011 — Idempotent explicit sync

- **Choice:** Attempt delivery after save/startup and expose Pending/Synced/Failed with
  manual Retry. Storage paths and database upserts reuse the local record UUID.
- **Why:** It gives truthful failure feedback and safe retry without a background-sync
  subsystem.
- **Cost:** There is no exponential backoff, connectivity observer or orphan worker.
- **Revisit if:** Continuous unattended synchronization becomes a requirement.

### D-012 — Database-driven study prompt

- **Choice:** `app_config.active_prompt_id` selects an enabled `study_prompts` row;
  the UI falls back to `Free reflection` and snapshots the chosen prompt into records.
- **Why:** This is a visible, testable interpretation of the database-driven UI
  requirement and preserves provenance in the exported data.
- **Cost:** There is one global prompt rather than segmentation or experimentation.
- **Revisit if:** Per-user configuration or richer remote content is required.

### D-013 — Server-side CSV view

- **Choice:** Export one RLS-scoped `journal_records_csv` view through PostgREST
  `Accept: text/csv`, including UTC timestamps and compact image/prompt metadata.
- **Why:** The database remains authoritative and no custom export service is needed.
- **Cost:** Export requires an authenticated reviewer/session path; image bytes and
  signed URLs are intentionally excluded.
- **Revisit if:** Administrative cross-owner exports or richer reporting are required.

### D-014 — Restricted challenge access

- **Choice:** Embed only the client Supabase URL/publishable key required by the APK;
  deliver dashboard access separately by project invitation. Never ship owner passwords
  or service-role keys.
- **Why:** RLS, not client-key secrecy, protects user-owned rows and objects while the
  challenge build remains directly testable.
- **Cost:** The hosted project must remain available for the review window and its
  invitations must be managed separately from GitHub.
- **Revisit if:** The evaluator specifies a different access mechanism.

### D-015 — Transparent image normalization

- **Choice:** Continue normalizing app-owned images to bounded JPEG, explicitly
  compositing transparent and semi-transparent source pixels onto white first.
- **Why:** JPEG keeps the existing local/remote contract compact and predictable,
  while an intentional neutral background prevents transparent PNG pixels from
  becoming accidental black areas during encoding.
- **Cost:** Transparency is intentionally discarded and cannot be recovered from
  the stored image.
- **Revisit if:** Preserving alpha becomes a product requirement; that would require
  PNG/WebP storage plus matching path and Content-Type changes across local and
  remote persistence.
