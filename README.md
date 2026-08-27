# Trace Journal

Trace Journal is a Unity-based Android journaling application created for the
SkillSquare / NXTech coding challenge. A journal record combines free text with
an image acquired on the device, remains available locally, and is also stored
in a remote database.

> Status: the required local and remote journal contour is complete. It has been
> exercised against the live Supabase project in the Editor and on a physical
> Android device. External repository/server access is prepared separately.

## Target

- Unity `6000.3.5f2`, Universal 2D
- Android 9.0+ (API 28), portrait
- Application ID: `com.asurzhenko.tracejournal`
- uGUI + TextMeshPro

## Required coverage

| Requirement | Planned evidence | Status |
|---|---|---|
| Create a record with free text and an on-device image | Gallery → composer → save on Android | Verified |
| Persist records locally and show them as a list | Save, list and restart on Android | Verified |
| Store records in a remote database | Storage upload + UUID PostgREST upsert | Verified live |
| Export database records as one CSV table | `journal_records_csv` with `Accept: text/csv` | Verified live |
| Let database values visibly change the app UI | `app_config` selects the composer prompt | Verified live |
| Provide an Android 9.0+ APK | API 28 minimum; APK supplied separately | Verified |
| Provide restricted remote-server access | Separate project invitation/handoff | Prepared separately |
| Document effort, architecture, process and challenges | This README, `DECISIONS.md`, `ESTIMATE.md` | Complete |

## Architecture

- `JournalRecord` is the versioned local/remote contract. Stable UUIDs are reused
  for retry and prompt ID/text are snapshotted with each new entry.
- `JournalRepository` owns one JSON index and an `Images` directory under
  `Application.persistentDataPath`. Index replacement is transactional and a failed
  append removes the newly owned image.
- The uGUI views are passive; `TraceJournalController` coordinates image acquisition,
  local persistence, prompt refresh and the explicit Pending/Synced/Failed flow.
- `SupabaseClient` uses direct HTTPS for anonymous Auth, private Storage and
  PostgREST. PostgreSQL RLS isolates every install by `auth.uid()`; deterministic
  `{user UUID}/{record UUID}.jpg` paths and UUID upserts make Retry idempotent.

## Development process and challenges

Work was split into a pre-code estimate, a local vertical slice, a remote compliance
slice and final device/release verification. Each slice was implemented on a feature
branch, reviewed, tested and integrated through `dev` before the final `main` release.

The main challenges were Android gallery/provider behaviour, preserving local image
ownership, and keeping retries safe across Storage and PostgreSQL. The selected image
is therefore normalized into an app-owned bounded JPEG before a record is saved.
Remote writes reuse deterministic IDs and paths, while RLS is the access boundary.
Legacy records without a valid prompt UUID required omitting `prompt_id` entirely
rather than sending an invalid empty UUID. Device verification also led to an explicit
safe-area root and lightweight loading feedback without adding another UI framework.

## Verification

- EditMode coverage exercises validation, durable local save/load and corruption
  handling, stable ordering/IDs, auth headers and refresh, deterministic retry,
  prompt selection/fallback, and legacy prompt payloads.
- Live Supabase checks covered anonymous auth, private image upload, database upsert,
  retry without duplicate rows, A → B prompt changes, owner isolation and CSV parsing.
- A physical Android device smoke covered install/launch, gallery selection, save,
  local restart, remote sync/retry and safe-area presentation. The submitted project
  targets Android 9.0+ (`minSdkVersion` 28), IL2CPP, ARMv7 + ARM64.

## Supabase setup

1. Create a Supabase project and enable **Anonymous Sign-Ins** under Authentication.
2. Apply [`supabase/migrations/202608270001_remote_contour.sql`](supabase/migrations/202608270001_remote_contour.sql)
   in the SQL editor or through the Supabase CLI. It creates the tables, private
   `journal-images` bucket, RLS policies, CSV view and two synthetic prompt rows.
3. For a different backend, select `Controller` in `Assets/Scenes/SampleScene.unity`
   and replace the Supabase URL and publishable key. A publishable key is client
   configuration; never put a service-role/secret key or account password in Unity.
4. Keep demo records synthetic. The client signs in anonymously once per install,
   persists that session in the app sandbox, and scopes rows/objects to `auth.uid()`.

The migration seeds prompt A as active. To demonstrate a database-driven A → B UI
change, update the singleton config row, then reopen the composer (opening it refreshes
the prompt):

```sql
update public.app_config
set active_prompt_id = '22222222-2222-4222-8222-222222222222'
where id = 'default';
```

Invalid, disabled, missing or unreachable prompt data visibly falls back to
`Free reflection`. New records snapshot the selected prompt ID and text.

## CSV export

The view returns one RLS-scoped row per authoritative database record with UTC
timestamps and compact image/prompt metadata. It contains no image bytes or signed
URLs. User text beginning with optional whitespace followed by `=`, `+`, `-` or `@`
is prefixed with a single quote by PostgreSQL before CSV serialization.

Use the install's anonymous access token through a separate safe channel:

```bash
curl "$SUPABASE_URL/rest/v1/journal_records_csv?select=record_id,created_utc,uploaded_utc,entry_text,image_metadata,prompt_metadata,client_schema_version&order=created_utc.asc" \
  -H "apikey: $SUPABASE_PUBLISHABLE_KEY" \
  -H "Authorization: Bearer $INSTALL_ACCESS_TOKEN" \
  -H "Accept: text/csv" \
  --output journal_records.csv
```

## Known security and reliability limits

- The publishable key is a client identifier; RLS, not key secrecy, is the access
  boundary. A service-role/secret key must never enter the project or APK.
- The anonymous access/refresh tokens are stored in plaintext inside the app sandbox.
  Clearing app data loses ownership; there is no account recovery or multi-device merge.
- Anonymous signup abuse protection is limited to Supabase project rate limits unless
  the deployment owner adds CAPTCHA outside this compact challenge flow.
- Sync is one attempt after local save/startup plus explicit `Failed · Retry`; there is
  no background worker, backoff engine, concurrency proof or orphan cleanup worker.
- A Storage upload can succeed before its database upsert fails. Retry safely overwrites
  the same deterministic object path and upserts the same UUID row.

## Reviewer handoff and demo

The APK is delivered separately because build artifacts are intentionally gitignored:

- `TraceJournal.apk`, version `1.0.0` (`versionCode` 1)
- SHA-256: `C42AB9BBB710D0262C22FC42831A52FC15DD3CE93BDBEFDD66A4D9403C6F6478`
- API 28 minimum, API 36 target, IL2CPP `arm64-v8a` + `armeabi-v7a`

Restricted Supabase dashboard access is granted by project invitation through a
separate channel; owner passwords and service-role keys are never shared or committed.

A compact review flow is:

1. Launch the APK and observe the database-selected composer prompt.
2. Create an entry with text and a gallery image; observe `Pending` → `Synced`.
3. Restart the app and confirm the local list survives.
4. Change `app_config.active_prompt_id`, reopen the composer and observe prompt B.
5. Inspect the RLS-owned row/image and export `journal_records_csv` as CSV.

## Third-party software

Image acquisition uses NativeGallery `1.9.4` under the MIT License. The original
license is included at `Assets/Plugins/NativeGallery/LICENSE.txt`.

[`DECISIONS.md`](DECISIONS.md) records consequential choices and trade-offs, and
[`ESTIMATE.md`](ESTIMATE.md) preserves the initial pre-code estimate. Process,
verification evidence and final demo steps remain in this README rather than separate
architecture, devlog or test-report documents. Privileged credentials are never
committed.
