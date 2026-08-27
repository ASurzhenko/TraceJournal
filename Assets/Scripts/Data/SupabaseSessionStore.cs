using System;
using System.IO;
using UnityEngine;

namespace TraceJournal.Data
{
    [Serializable]
    public sealed class SupabaseSession
    {
        public string accessToken;
        public string refreshToken;
        public string userId;
        public long expiresAt;
    }

    public interface ISupabaseSessionStore
    {
        bool TryLoad(out SupabaseSession session, out bool fileExists, out string error);
        bool TrySave(SupabaseSession session, out string error);
    }

    public sealed class SupabaseSessionFileStore : ISupabaseSessionStore
    {
        private static readonly string SessionFileName = "supabase_session.json";

        private readonly string _sessionPath;

        public SupabaseSessionFileStore(string rootDirectory)
        {
            Directory.CreateDirectory(rootDirectory);
            _sessionPath = Path.Combine(rootDirectory, SessionFileName);
        }

        public bool TryLoad(out SupabaseSession session, out bool fileExists, out string error)
        {
            session = null;
            fileExists = File.Exists(_sessionPath);
            error = null;
            if (!fileExists)
            {
                return true;
            }

            try
            {
                string json = File.ReadAllText(_sessionPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new InvalidDataException("The anonymous session file is empty.");
                }

                session = JsonUtility.FromJson<SupabaseSession>(json);
                if (session == null ||
                    json.IndexOf("\"accessToken\"", StringComparison.Ordinal) < 0 ||
                    json.IndexOf("\"refreshToken\"", StringComparison.Ordinal) < 0 ||
                    json.IndexOf("\"userId\"", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidDataException("The anonymous session has an invalid shape.");
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Could not read the anonymous session: {ex.Message}";
                Debug.LogError(
                    $"{nameof(SupabaseSessionFileStore)}.{nameof(TryLoad)} [Session] error={ex.Message}");
                return false;
            }
        }

        public bool TrySave(SupabaseSession session, out string error)
        {
            string tempPath = _sessionPath + ".tmp";
            try
            {
                string json = JsonUtility.ToJson(session, prettyPrint: false);
                File.WriteAllText(tempPath, json);

                if (File.Exists(_sessionPath))
                {
                    File.Replace(tempPath, _sessionPath, null);
                }
                else
                {
                    File.Move(tempPath, _sessionPath);
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Could not save the anonymous session: {ex.Message}";
                TryDeleteTemp(tempPath);
                Debug.LogError(
                    $"{nameof(SupabaseSessionFileStore)}.{nameof(TrySave)} [Session] error={ex.Message}");
                return false;
            }
        }

        private void TryDeleteTemp(string path)
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
                    $"{nameof(SupabaseSessionFileStore)}.{nameof(TryDeleteTemp)} [Cleanup] error={ex.Message}");
            }
        }
    }
}
