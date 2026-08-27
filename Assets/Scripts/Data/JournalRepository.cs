using System;
using System.Collections.Generic;
using System.IO;
using TraceJournal.Models;
using UnityEngine;

namespace TraceJournal.Data
{
    /// <summary>
    /// Local-only repository for F1. One versioned JSON index plus an app-owned
    /// Images folder, both under a root directory (persistentDataPath in the app,
    /// a temp directory in EditMode tests — see rootOverride).
    ///
    /// Durability: the index is written to a temp file first, then the previous
    /// index is replaced only after the temp write succeeds and is flushed. If
    /// anything fails after copying/normalizing a new image but before the index
    /// is durable, the newly-created image file is deleted so we never leak an
    /// orphaned image with no record pointing at it.
    /// </summary>
    public class JournalRepository
    {
        private const string IndexFileName = "journal_index.json";
        private const string ImagesFolderName = "Images";

        private readonly string _rootDir;
        private readonly string _indexPath;
        private readonly string _imagesDir;

        /// rootOverride lets EditMode tests point at a temporary directory instead
        /// of Application.persistentDataPath.
        public JournalRepository(string rootOverride = null)
        {
            _rootDir = rootOverride ?? Application.persistentDataPath;
            _indexPath = Path.Combine(_rootDir, IndexFileName);
            _imagesDir = Path.Combine(_rootDir, ImagesFolderName);

            Directory.CreateDirectory(_rootDir);
            Directory.CreateDirectory(_imagesDir);
        }

        public string ImagesDirectory => _imagesDir;

        public List<JournalRecord> LoadAll()
        {
            if (TryLoad(out List<JournalRecord> records, out _))
            {
                return records;
            }

            return new List<JournalRecord>();
        }

        public bool TryLoad(out List<JournalRecord> records, out string error)
        {
            records = new List<JournalRecord>();
            error = null;

            if (!File.Exists(_indexPath))
            {
                return true;
            }

            try
            {
                string json = File.ReadAllText(_indexPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new InvalidDataException("The index is empty.");
                }

                JournalCollection collection = JsonUtility.FromJson<JournalCollection>(json);
                if (collection == null)
                {
                    throw new InvalidDataException("The index root could not be parsed.");
                }

                if (collection.schemaVersion != JournalCollection.CurrentSchemaVersion)
                {
                    throw new InvalidDataException(
                        $"Unsupported schema version {collection.schemaVersion}.");
                }

                if (json.IndexOf("\"records\"", StringComparison.Ordinal) < 0 ||
                    collection.records == null)
                {
                    throw new InvalidDataException("The index has no records collection.");
                }

                records = collection.records;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Could not read the journal index: {ex.Message}";
                Debug.LogError(
                    $"{nameof(JournalRepository)}.{nameof(TryLoad)} [Index] path={_indexPath}, error={ex.Message}");
                return false;
            }
        }

        /// Appends one record and durably writes the index. On failure, deletes
        /// newlyCreatedImageAbsolutePath (if provided) so we don't leak an orphaned
        /// image file. Returns true only once the index is confirmed durable.
        public bool TryAppend(JournalRecord record, string newlyCreatedImageAbsolutePath, out string error)
        {
            error = null;

            if (!ValidateNewRecordImage(record, newlyCreatedImageAbsolutePath, out error))
            {
                CleanupImage(newlyCreatedImageAbsolutePath);
                return false;
            }

            if (!TryLoad(out List<JournalRecord> current, out string loadError))
            {
                error = $"Could not load existing journal data. {loadError}";
                CleanupImage(newlyCreatedImageAbsolutePath);
                return false;
            }

            current.Add(record);

            var collection = new JournalCollection
            {
                schemaVersion = JournalCollection.CurrentSchemaVersion,
                records = current
            };

            string json;
            try
            {
                json = JsonUtility.ToJson(collection, prettyPrint: false);
            }
            catch (Exception ex)
            {
                error = $"Serialization failed: {ex.Message}";
                CleanupImage(newlyCreatedImageAbsolutePath);
                return false;
            }

            if (!TryWriteJson(json, out error))
            {
                CleanupImage(newlyCreatedImageAbsolutePath);
                return false;
            }

            return true;
        }

        public bool TryGet(string recordId, out JournalRecord record, out string error)
        {
            record = null;
            if (!TryLoad(out List<JournalRecord> records, out error))
            {
                return false;
            }

            record = records.Find(item => string.Equals(item.id, recordId, StringComparison.Ordinal));
            if (record == null)
            {
                error = $"Journal record {recordId} was not found.";
                Debug.LogWarning(
                    $"{nameof(JournalRepository)}.{nameof(TryGet)} [Record] id={recordId}, count={records.Count}");
                return false;
            }

            return true;
        }

        public bool TryUpdateRemoteState(
            string recordId,
            SyncState syncState,
            string remoteId,
            string syncError,
            out string error)
        {
            if (!TryLoad(out List<JournalRecord> records, out error))
            {
                return false;
            }

            JournalRecord record = records.Find(
                item => string.Equals(item.id, recordId, StringComparison.Ordinal));
            if (record == null)
            {
                error = $"Journal record {recordId} was not found.";
                Debug.LogWarning(
                    $"{nameof(JournalRepository)}.{nameof(TryUpdateRemoteState)} [Record] id={recordId}, count={records.Count}");
                return false;
            }

            record.syncState = syncState;
            record.remoteId = remoteId ?? string.Empty;
            record.syncError = syncError ?? string.Empty;

            var collection = new JournalCollection
            {
                schemaVersion = JournalCollection.CurrentSchemaVersion,
                records = records
            };

            return TryWriteCollection(collection, out error);
        }

        private bool TryWriteCollection(JournalCollection collection, out string error)
        {
            try
            {
                string json = JsonUtility.ToJson(collection, prettyPrint: false);
                return TryWriteJson(json, out error);
            }
            catch (Exception ex)
            {
                error = $"Serialization failed: {ex.Message}";
                return false;
            }
        }

        private bool TryWriteJson(string json, out string error)
        {
            string tempPath = _indexPath + ".tmp";

            try
            {
                File.WriteAllText(tempPath, json);

                if (File.Exists(_indexPath))
                {
                    File.Replace(tempPath, _indexPath, null);
                }
                else
                {
                    File.Move(tempPath, _indexPath);
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Index write failed: {ex.Message}";
                TryDeleteFile(tempPath);
                return false;
            }
        }

        private bool ValidateNewRecordImage(
            JournalRecord record,
            string newlyCreatedImageAbsolutePath,
            out string error)
        {
            if (record == null)
            {
                error = "The journal record is missing.";
                return false;
            }

            if (string.IsNullOrEmpty(record.imageRelativePath) ||
                string.IsNullOrEmpty(newlyCreatedImageAbsolutePath))
            {
                error = "The selected image is not owned by the app.";
                return false;
            }

            string imagesRoot;
            string expectedImagePath;
            string suppliedImagePath;
            try
            {
                imagesRoot = Path.GetFullPath(_imagesDir) + Path.DirectorySeparatorChar;
                expectedImagePath = Path.GetFullPath(
                    Path.Combine(_imagesDir, record.imageRelativePath));
                suppliedImagePath = Path.GetFullPath(newlyCreatedImageAbsolutePath);
            }
            catch (Exception ex)
            {
                error = $"The selected image path is invalid: {ex.Message}";
                return false;
            }

            if (!expectedImagePath.StartsWith(imagesRoot, StringComparison.Ordinal) ||
                !string.Equals(expectedImagePath, suppliedImagePath, StringComparison.Ordinal))
            {
                error = "The selected image path is outside app ownership.";
                return false;
            }

            if (!File.Exists(expectedImagePath))
            {
                error = "The selected image file no longer exists.";
                return false;
            }

            error = null;
            return true;
        }

        private void CleanupImage(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return;
            }

            if (!IsPathInImagesDirectory(absolutePath))
            {
                Debug.LogWarning(
                    $"{nameof(JournalRepository)}.{nameof(CleanupImage)} [Cleanup] refused non-owned path={absolutePath}");
                return;
            }

            TryDeleteFile(absolutePath);
        }

        private bool IsPathInImagesDirectory(string path)
        {
            try
            {
                string imagesRoot = Path.GetFullPath(_imagesDir) + Path.DirectorySeparatorChar;
                string fullPath = Path.GetFullPath(path);
                return fullPath.StartsWith(imagesRoot, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"{nameof(JournalRepository)}.{nameof(TryDeleteFile)} [Cleanup] path={path}, error={ex.Message}");
            }
        }
    }
}
