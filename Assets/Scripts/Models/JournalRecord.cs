using System;

namespace TraceJournal.Models
{
    public enum SyncState
    {
        Pending = 0,
        Synced = 1,
        Failed = 2
    }

    /// <summary>
    /// Serializable journal record. Uses JsonUtility, so all fields are public,
    /// concrete, and avoid types JsonUtility can't handle (no Nullable<T>, no Dictionary).
    /// "Nullable" fields required for F2 (prompt/remote) are strings that are simply
    /// empty when absent — JsonUtility has no concept of a JSON null vs missing field.
    /// </summary>
    [Serializable]
    public class JournalRecord
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion;

        /// Stable identity — never re-derive from list position.
        public string id;

        /// ISO-8601 UTC, e.g. "2026-08-27T14:03:11.123Z". Always store/compare in UTC;
        /// convert to local only at display time.
        public string createdUtc;

        public string text;

        /// Path relative to the app-owned Images folder (NOT an absolute path and
        /// NOT a content:// / file:// provider URI). Resolve to absolute only when
        /// actually reading the file.
        public string imageRelativePath;

        public int imageWidth;
        public int imageHeight;

        public SyncState syncState;

        // --- F2 seam: unused in F1, kept empty, never read/written by F1 logic ---
        public string promptId;
        public string promptText;
        public string remoteId;
        public string syncError;

        public static JournalRecord CreateNew(
            string text,
            string imageRelativePath,
            int width,
            int height,
            string promptId = "",
            string promptText = "")
        {
            return new JournalRecord
            {
                schemaVersion = CurrentSchemaVersion,
                id = Guid.NewGuid().ToString("N"),
                createdUtc = DateTime.UtcNow.ToString("o"),
                text = text,
                imageRelativePath = imageRelativePath,
                imageWidth = width,
                imageHeight = height,
                syncState = SyncState.Pending,
                promptId = promptId ?? string.Empty,
                promptText = promptText ?? string.Empty,
                remoteId = string.Empty,
                syncError = string.Empty
            };
        }

        /// Parses createdUtc back to a DateTime for sorting/display. Falls back to
        /// DateTime.MinValue on a corrupt value rather than throwing, so one bad
        /// record can't break the whole list.
        public DateTime GetCreatedUtcDateTime()
        {
            if (DateTime.TryParse(createdUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            {
                return dt.ToUniversalTime();
            }
            return DateTime.MinValue;
        }
    }
}
