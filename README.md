# Trace Journal

Trace Journal is a Unity-based Android journaling application created for the
SkillSquare / NXTech coding challenge. A journal record combines free text with
an image acquired on the device, remains available locally, and is also stored
in a remote database.

> Status: project bootstrap. Product implementation begins only after the
> pre-coding estimate and discovery decisions are approved.

## Target

- Unity `6000.3.5f2`, Universal 2D
- Android 9.0+ (API 28), portrait
- Application ID: `com.asurzhenko.tracejournal`
- uGUI + TextMeshPro

## Required coverage

| Requirement | Planned evidence | Status |
|---|---|---|
| Create a record with free text and an on-device image | Android acceptance scenario | Planned |
| Persist records locally and show them as a list | Restart/recovery scenario | Planned |
| Store records in a remote database | Deployed test-server round trip | Planned |
| Export database records as one CSV table | Repeatable export with timestamps and metadata | Planned |
| Let database values visibly change the app UI | Controlled remote-value demonstration | Planned |
| Provide an Android 9.0+ APK | Installation and device test report | Planned |
| Provide restricted remote-server access | Separate credential handoff | Planned |
| Document effort, architecture, process and challenges | Repository documentation | In progress |

## Documentation

- [`DECISIONS.md`](DECISIONS.md) records consequential choices and trade-offs.
- `ESTIMATE.md` will contain the estimate committed before product code.
- `REQUIREMENTS-TRACEABILITY.md` will map each requirement to implementation and evidence.
- `DEVLOG.md` will record dated progress, problems and deviations.
- `TEST-REPORT.md` will contain the final device/API test matrix and known limitations.

Architecture, setup instructions, server access procedure and verified demo steps
will be added as their corresponding packages are implemented. Credentials and
other secrets will never be committed to this repository.
