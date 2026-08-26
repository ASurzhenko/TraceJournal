# Trace Journal — Initial Effort Estimate

**Prepared:** 2026-08-26, before product or server implementation  
**Target completion:** 2026-08-27  
**Expected effort:** **12 focused hours** (about **1.5 eight-hour working days**)  
**Working range:** **9–18 focused hours**

The Unity project scaffold, Android baseline settings, repository setup and initial
documentation existed before this estimate. No journal, image, persistence, UI,
networking, database or server implementation existed when this estimate was written.

## Breakdown

| Work | Best | Expected | Worst |
|---|---:|---:|---:|
| Local Unity journal: models, JSON/image persistence, gallery input and portrait UI | 3 h | 4 h | 5 h |
| Remote contour: Supabase database/storage, manual delivery, remote prompt and CSV | 3 h | 4 h | 6 h |
| Android smoke/build, concise documentation and submission preparation | 2 h | 2.5 h | 4 h |
| Integration contingency | 1 h | 1.5 h | 3 h |
| **Total** | **9 h** | **12 h** | **18 h** |

## Assumptions

- One experienced Unity developer using the existing Unity `6000.3.5f2` scaffold.
- One append-only record contains non-empty text and one gallery-imported image.
- Local data uses a small versioned JSON store plus app-owned image files.
- One Supabase project provides PostgreSQL, image storage and the CSV source.
- Only synthetic demonstration data is used.
- Verification focuses on the literal requirements and the end-to-end demo path.

## Deliberate scope cuts

Camera capture, edit/delete, background sync, multi-device conflict resolution,
account UI, production-grade session recovery, analytics, localization, voice, AI
features, elaborate animation and production-compliance claims are excluded.

## Main risks

- Android gallery/plugin behavior and IL2CPP build compatibility.
- Supabase access-policy and storage configuration.
- Device installation and end-to-end verification take real elapsed time even when
  implementation is completed quickly.

## Estimate integrity

This initial estimate is not rewritten after implementation starts. Actual focused
time and material variance may be appended below during final submission preparation.
