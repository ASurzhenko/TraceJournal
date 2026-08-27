using System;
using System.Collections.Generic;

namespace TraceJournal.Models
{
    /// <summary>
    /// Root object of index.json. JsonUtility can't serialize a bare List&lt;T&gt;
    /// at the top level, so this wrapper is required.
    /// </summary>
    [Serializable]
    public class JournalCollection
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion;
        public List<JournalRecord> records = new List<JournalRecord>();

        public static JournalCollection CreateEmpty()
        {
            return new JournalCollection
            {
                schemaVersion = CurrentSchemaVersion,
                records = new List<JournalRecord>()
            };
        }
    }
}
