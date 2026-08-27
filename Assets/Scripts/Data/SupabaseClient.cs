using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TraceJournal.Models;
using UnityEngine;

namespace TraceJournal.Data
{
    public sealed class RemoteOperationResult
    {
        public bool Success;
        public string Error;
        public string RemoteRecordId;

        public static RemoteOperationResult Ok(string remoteRecordId = null)
        {
            return new RemoteOperationResult
            {
                Success = true,
                RemoteRecordId = remoteRecordId,
                Error = null
            };
        }

        public static RemoteOperationResult Fail(string error)
        {
            return new RemoteOperationResult
            {
                Success = false,
                Error = error,
                RemoteRecordId = null
            };
        }
    }

    public sealed class RemotePrompt
    {
        public static readonly string FallbackText = "Free reflection";

        public string Id;
        public string Text;
        public string Error;

        public bool IsFallback => string.IsNullOrEmpty(Id);

        public static RemotePrompt Fallback(string error = null)
        {
            return new RemotePrompt
            {
                Id = string.Empty,
                Text = FallbackText,
                Error = error
            };
        }
    }

    public sealed class SupabaseClient
    {
        private static readonly string ImagesBucket = "journal-images";
        private static readonly long MaximumUploadBytes = 6L * 1024L * 1024L;

        private readonly string _baseUrl;
        private readonly string _publishableKey;
        private readonly ISupabaseTransport _transport;
        private readonly ISupabaseSessionStore _sessionStore;

        public SupabaseClient(
            string baseUrl,
            string publishableKey,
            ISupabaseTransport transport,
            ISupabaseSessionStore sessionStore)
        {
            _baseUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
            _publishableKey = (publishableKey ?? string.Empty).Trim();
            _transport = transport;
            _sessionStore = sessionStore;
        }

        public bool IsConfigured
        {
            get
            {
                return Uri.TryCreate(_baseUrl, UriKind.Absolute, out Uri uri) &&
                       string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                       !string.IsNullOrWhiteSpace(_publishableKey);
            }
        }

        public async Task<RemoteOperationResult> DeliverAsync(
            JournalRecord record,
            string imagesDirectory,
            CancellationToken cancellationToken)
        {
            if (!TryValidateRecord(record, imagesDirectory, out Guid recordId, out string imagePath, out string error))
            {
                return RemoteOperationResult.Fail(error);
            }

            RemoteSessionResult sessionResult = await EnsureSessionAsync(cancellationToken);
            if (!sessionResult.Success)
            {
                return RemoteOperationResult.Fail(sessionResult.Error);
            }

            byte[] imageBytes;
            try
            {
                var fileInfo = new FileInfo(imagePath);
                if (fileInfo.Length > MaximumUploadBytes)
                {
                    return RemoteOperationResult.Fail("The owned JPEG is larger than the 6 MB upload limit.");
                }

                imageBytes = File.ReadAllBytes(imagePath);
            }
            catch (Exception ex)
            {
                return RemoteOperationResult.Fail($"Could not read the owned JPEG: {ex.Message}");
            }

            Guid userId = Guid.Parse(sessionResult.Session.userId);
            string remoteRecordId = recordId.ToString("D");
            string storageObjectPath = BuildStorageObjectPath(userId, recordId);

            SupabaseRequest uploadRequest = CreateAuthorizedRequest(
                "POST",
                $"{_baseUrl}/storage/v1/object/{ImagesBucket}/{storageObjectPath}",
                sessionResult.Session.accessToken,
                imageBytes,
                "image/jpeg");
            uploadRequest.Headers["x-upsert"] = "true";
            uploadRequest.Headers["cache-control"] = "max-age=3600";

            SupabaseResponse uploadResponse = await _transport.SendAsync(uploadRequest, cancellationToken);
            if (!uploadResponse.IsSuccess)
            {
                return RemoteOperationResult.Fail(BuildHttpError("Image upload", uploadResponse));
            }

            string payloadJson = BuildJournalUpsertJson(record, userId, recordId, storageObjectPath);
            SupabaseRequest recordRequest = CreateAuthorizedRequest(
                "POST",
                $"{_baseUrl}/rest/v1/journal_records?on_conflict=id",
                sessionResult.Session.accessToken,
                Encoding.UTF8.GetBytes(payloadJson),
                "application/json");
            recordRequest.Headers["Prefer"] = "resolution=merge-duplicates,return=minimal";

            SupabaseResponse recordResponse = await _transport.SendAsync(recordRequest, cancellationToken);
            if (!recordResponse.IsSuccess)
            {
                return RemoteOperationResult.Fail(BuildHttpError("Record upsert", recordResponse));
            }

            return RemoteOperationResult.Ok(remoteRecordId);
        }

        public async Task<RemotePrompt> FetchActivePromptAsync(CancellationToken cancellationToken)
        {
            RemoteSessionResult sessionResult = await EnsureSessionAsync(cancellationToken);
            if (!sessionResult.Success)
            {
                return RemotePrompt.Fallback(sessionResult.Error);
            }

            SupabaseRequest configRequest = CreateAuthorizedRequest(
                "GET",
                $"{_baseUrl}/rest/v1/app_config?select=active_prompt_id&id=eq.default&limit=1",
                sessionResult.Session.accessToken);
            SupabaseResponse configResponse = await _transport.SendAsync(configRequest, cancellationToken);
            if (!configResponse.IsSuccess)
            {
                return RemotePrompt.Fallback(BuildHttpError("Prompt config fetch", configResponse));
            }

            if (!TryParseConfig(configResponse.Body, out Guid promptId, out string configError))
            {
                return RemotePrompt.Fallback(configError);
            }

            string promptIdText = promptId.ToString("D");
            SupabaseRequest promptRequest = CreateAuthorizedRequest(
                "GET",
                $"{_baseUrl}/rest/v1/study_prompts?select=id,prompt_text&id=eq.{promptIdText}&is_enabled=eq.true&limit=1",
                sessionResult.Session.accessToken);
            SupabaseResponse promptResponse = await _transport.SendAsync(promptRequest, cancellationToken);
            if (!promptResponse.IsSuccess)
            {
                return RemotePrompt.Fallback(BuildHttpError("Prompt fetch", promptResponse));
            }

            if (!TryParsePrompt(promptResponse.Body, promptId, out RemotePrompt prompt, out string promptError))
            {
                return RemotePrompt.Fallback(promptError);
            }

            return prompt;
        }

        public static string BuildStorageObjectPath(Guid userId, Guid recordId)
        {
            return $"{userId:D}/{recordId:D}.jpg";
        }

        public static string BuildJournalUpsertJson(
            JournalRecord record,
            Guid userId,
            Guid recordId,
            string storageObjectPath)
        {
            JournalRecordPayload payload;
            if (Guid.TryParse(record.promptId, out Guid promptId))
            {
                payload = new PromptJournalRecordPayload
                {
                    prompt_id = promptId.ToString("D")
                };
            }
            else
            {
                payload = new JournalRecordPayload();
            }

            payload.id = recordId.ToString("D");
            payload.owner_id = userId.ToString("D");
            payload.created_utc = record.createdUtc;
            payload.text = record.text;
            payload.image_path = storageObjectPath;
            payload.image_width = record.imageWidth;
            payload.image_height = record.imageHeight;
            payload.prompt_text = string.IsNullOrWhiteSpace(record.promptText)
                ? null
                : record.promptText;
            payload.client_schema_version = record.schemaVersion;

            return JsonUtility.ToJson(payload, prettyPrint: false);
        }

        private async Task<RemoteSessionResult> EnsureSessionAsync(CancellationToken cancellationToken)
        {
            if (!IsConfigured)
            {
                return RemoteSessionResult.Fail("Supabase is not configured. Use Retry after setup is complete.");
            }

            if (!_sessionStore.TryLoad(
                    out SupabaseSession session,
                    out bool sessionFileExists,
                    out string loadError))
            {
                return RemoteSessionResult.Fail(loadError);
            }

            if (sessionFileExists)
            {
                if (!IsSessionShapeValid(session))
                {
                    return RemoteSessionResult.Fail(
                        "The saved anonymous session is invalid. This install cannot safely claim a new owner.");
                }

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (session.expiresAt > now + 60)
                {
                    return RemoteSessionResult.Ok(session);
                }

                return await RefreshSessionAsync(session.refreshToken, cancellationToken);
            }

            return await CreateAnonymousSessionAsync(cancellationToken);
        }

        private async Task<RemoteSessionResult> CreateAnonymousSessionAsync(
            CancellationToken cancellationToken)
        {
            byte[] body = Encoding.UTF8.GetBytes("{\"data\":{},\"gotrue_meta_security\":{}}");
            SupabaseRequest request = CreatePublicRequest(
                "POST",
                $"{_baseUrl}/auth/v1/signup",
                body,
                "application/json");
            SupabaseResponse response = await _transport.SendAsync(request, cancellationToken);
            if (!response.IsSuccess)
            {
                return RemoteSessionResult.Fail(BuildHttpError("Anonymous sign-in", response));
            }

            return ParseAndSaveSession(response.Body, "Anonymous sign-in");
        }

        private async Task<RemoteSessionResult> RefreshSessionAsync(
            string refreshToken,
            CancellationToken cancellationToken)
        {
            var refreshPayload = new RefreshTokenPayload
            {
                refresh_token = refreshToken
            };
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(refreshPayload));
            SupabaseRequest request = CreatePublicRequest(
                "POST",
                $"{_baseUrl}/auth/v1/token?grant_type=refresh_token",
                body,
                "application/json");
            SupabaseResponse response = await _transport.SendAsync(request, cancellationToken);
            if (!response.IsSuccess)
            {
                return RemoteSessionResult.Fail(BuildHttpError("Session refresh", response));
            }

            return ParseAndSaveSession(response.Body, "Session refresh");
        }

        private RemoteSessionResult ParseAndSaveSession(string json, string operation)
        {
            AuthTokenResponse response;
            try
            {
                response = JsonUtility.FromJson<AuthTokenResponse>(json);
            }
            catch (ArgumentException ex)
            {
                return RemoteSessionResult.Fail($"{operation} returned malformed JSON: {ex.Message}");
            }

            if (response == null ||
                string.IsNullOrWhiteSpace(response.access_token) ||
                string.IsNullOrWhiteSpace(response.refresh_token) ||
                response.user == null ||
                !Guid.TryParse(response.user.id, out Guid userId))
            {
                return RemoteSessionResult.Fail($"{operation} returned an incomplete session.");
            }

            long expiresAt = response.expires_at;
            if (expiresAt <= 0 && response.expires_in > 0)
            {
                expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + response.expires_in;
            }

            var session = new SupabaseSession
            {
                accessToken = response.access_token,
                refreshToken = response.refresh_token,
                userId = userId.ToString("D"),
                expiresAt = expiresAt
            };

            if (!_sessionStore.TrySave(session, out string saveError))
            {
                return RemoteSessionResult.Fail(saveError);
            }

            return RemoteSessionResult.Ok(session);
        }

        private SupabaseRequest CreatePublicRequest(
            string method,
            string url,
            byte[] body = null,
            string contentType = null)
        {
            var request = new SupabaseRequest
            {
                Method = method,
                Url = url,
                Body = body
            };
            request.Headers["apikey"] = _publishableKey;
            if (!string.IsNullOrEmpty(contentType))
            {
                request.Headers["Content-Type"] = contentType;
            }

            return request;
        }

        private SupabaseRequest CreateAuthorizedRequest(
            string method,
            string url,
            string accessToken,
            byte[] body = null,
            string contentType = null)
        {
            SupabaseRequest request = CreatePublicRequest(method, url, body, contentType);
            request.Headers["Authorization"] = $"Bearer {accessToken}";
            return request;
        }

        private static bool TryValidateRecord(
            JournalRecord record,
            string imagesDirectory,
            out Guid recordId,
            out string imagePath,
            out string error)
        {
            recordId = Guid.Empty;
            imagePath = null;
            error = null;
            if (record == null || !Guid.TryParse(record.id, out recordId))
            {
                error = "The local record does not have a valid UUID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.createdUtc) ||
                !DateTime.TryParse(
                    record.createdUtc,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out DateTime createdUtc) ||
                createdUtc.Kind != DateTimeKind.Utc)
            {
                error = "The local record does not have a valid UTC timestamp.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.imageRelativePath))
            {
                error = "The local record does not reference an owned image.";
                return false;
            }

            try
            {
                string imageRoot = Path.GetFullPath(imagesDirectory) + Path.DirectorySeparatorChar;
                imagePath = Path.GetFullPath(Path.Combine(imagesDirectory, record.imageRelativePath));
                if (!imagePath.StartsWith(imageRoot, StringComparison.Ordinal) || !File.Exists(imagePath))
                {
                    error = "The local record image is missing or outside app ownership.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"The local record image path is invalid: {ex.Message}";
                return false;
            }

            return true;
        }

        private static bool IsSessionShapeValid(SupabaseSession session)
        {
            return session != null &&
                   !string.IsNullOrWhiteSpace(session.accessToken) &&
                   !string.IsNullOrWhiteSpace(session.refreshToken) &&
                   Guid.TryParse(session.userId, out _);
        }

        private static bool TryParseConfig(string json, out Guid promptId, out string error)
        {
            promptId = Guid.Empty;
            try
            {
                AppConfigRows rows = JsonUtility.FromJson<AppConfigRows>($"{{\"items\":{json}}}");
                if (rows == null || rows.items == null || rows.items.Length != 1 ||
                    !Guid.TryParse(rows.items[0].active_prompt_id, out promptId))
                {
                    error = "Remote prompt config is missing or invalid.";
                    return false;
                }

                error = null;
                return true;
            }
            catch (ArgumentException ex)
            {
                error = $"Remote prompt config returned malformed JSON: {ex.Message}";
                return false;
            }
        }

        private static bool TryParsePrompt(
            string json,
            Guid expectedPromptId,
            out RemotePrompt prompt,
            out string error)
        {
            prompt = null;
            try
            {
                StudyPromptRows rows = JsonUtility.FromJson<StudyPromptRows>($"{{\"items\":{json}}}");
                if (rows == null || rows.items == null || rows.items.Length != 1 ||
                    !Guid.TryParse(rows.items[0].id, out Guid actualPromptId) ||
                    actualPromptId != expectedPromptId ||
                    string.IsNullOrWhiteSpace(rows.items[0].prompt_text))
                {
                    error = "The selected remote prompt is missing or invalid.";
                    return false;
                }

                prompt = new RemotePrompt
                {
                    Id = actualPromptId.ToString("D"),
                    Text = rows.items[0].prompt_text.Trim(),
                    Error = null
                };
                error = null;
                return true;
            }
            catch (ArgumentException ex)
            {
                error = $"Remote prompt returned malformed JSON: {ex.Message}";
                return false;
            }
        }

        private static string BuildHttpError(string operation, SupabaseResponse response)
        {
            string details = TryBuildSafeHttpErrorDetails(response.Body);
            if (string.IsNullOrEmpty(details))
            {
                details = SanitizeHttpErrorValue(response.Error, 160);
            }

            if (string.IsNullOrEmpty(details))
            {
                details = "request rejected";
            }

            return $"{operation} failed (HTTP {response.StatusCode}): {details}.";
        }

        private static string TryBuildSafeHttpErrorDetails(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return null;
            }

            try
            {
                SupabaseErrorBody error = JsonUtility.FromJson<SupabaseErrorBody>(responseBody);
                if (error == null)
                {
                    return null;
                }

                string code = SanitizeHttpErrorValue(error.code, 48);
                string message = SanitizeHttpErrorValue(error.message, 200);
                if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(message))
                {
                    return $"code={code}, message={message}";
                }

                if (!string.IsNullOrEmpty(code))
                {
                    return $"code={code}";
                }

                return string.IsNullOrEmpty(message)
                    ? null
                    : $"message={message}";
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static string SanitizeHttpErrorValue(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value) || ContainsSensitiveHttpErrorData(value))
            {
                return null;
            }

            var builder = new StringBuilder(Math.Min(value.Length, maximumLength));
            bool previousWasWhitespace = false;
            foreach (char character in value.Trim())
            {
                if (char.IsWhiteSpace(character) || char.IsControl(character))
                {
                    if (builder.Length > 0 && !previousWasWhitespace)
                    {
                        builder.Append(' ');
                        previousWasWhitespace = true;
                    }

                    continue;
                }

                builder.Append(character);
                previousWasWhitespace = false;
                if (builder.Length >= maximumLength)
                {
                    break;
                }
            }

            return builder.ToString().Trim();
        }

        private static bool ContainsSensitiveHttpErrorData(string value)
        {
            string lower = value.ToLowerInvariant();
            string[] sensitiveMarkers =
            {
                "://",
                "www.",
                "authorization",
                "bearer",
                "apikey",
                "api-key",
                "api_key",
                "api key",
                "jwt",
                "token",
                "secret key",
                "service_role",
                "service-role",
                "publishable key",
                "anon key",
                "eyj"
            };

            foreach (string marker in sensitiveMarkers)
            {
                if (lower.Contains(marker))
                {
                    return true;
                }
            }

            int credentialCharacterRun = 0;
            foreach (char character in value)
            {
                if (char.IsLetterOrDigit(character) ||
                    character == '_' ||
                    character == '-' ||
                    character == '.')
                {
                    credentialCharacterRun++;
                    if (credentialCharacterRun >= 32)
                    {
                        return true;
                    }
                }
                else
                {
                    credentialCharacterRun = 0;
                }
            }

            return false;
        }

        [Serializable]
        private sealed class AuthTokenResponse
        {
            public string access_token;
            public string refresh_token;
            public long expires_in;
            public long expires_at;
            public AuthUser user;
        }

        [Serializable]
        private sealed class AuthUser
        {
            public string id;
        }

        [Serializable]
        private sealed class RefreshTokenPayload
        {
            public string refresh_token;
        }

        [Serializable]
        private class JournalRecordPayload
        {
            public string id;
            public string owner_id;
            public string created_utc;
            public string text;
            public string image_path;
            public int image_width;
            public int image_height;
            public string prompt_text;
            public int client_schema_version;
        }

        [Serializable]
        private sealed class PromptJournalRecordPayload : JournalRecordPayload
        {
            public string prompt_id;
        }

        [Serializable]
        private sealed class SupabaseErrorBody
        {
            public string code;
            public string message;
        }

        [Serializable]
        private sealed class AppConfigRows
        {
            public AppConfigRow[] items;
        }

        [Serializable]
        private sealed class AppConfigRow
        {
            public string active_prompt_id;
        }

        [Serializable]
        private sealed class StudyPromptRows
        {
            public StudyPromptRow[] items;
        }

        [Serializable]
        private sealed class StudyPromptRow
        {
            public string id;
            public string prompt_text;
        }

        private sealed class RemoteSessionResult
        {
            public bool Success;
            public SupabaseSession Session;
            public string Error;

            public static RemoteSessionResult Ok(SupabaseSession session)
            {
                return new RemoteSessionResult
                {
                    Success = true,
                    Session = session,
                    Error = null
                };
            }

            public static RemoteSessionResult Fail(string error)
            {
                return new RemoteSessionResult
                {
                    Success = false,
                    Session = null,
                    Error = error
                };
            }
        }
    }
}
