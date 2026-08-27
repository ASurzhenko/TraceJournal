using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TraceJournal.Data;
using TraceJournal.Models;

namespace TraceJournal.Tests.EditMode
{
    public class SupabaseClientTests
    {
        private string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), $"TraceJournalRemoteTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }

        [Test]
        public async Task DeliverTwice_UsesOneStableUuidAndStoragePath()
        {
            Guid userId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            Guid recordId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
            var transport = new FakeTransport(
                JsonResponse(CreateAuthResponse(userId)),
                SuccessResponse(),
                SuccessResponse(),
                SuccessResponse(),
                SuccessResponse());
            var sessionStore = new MemorySessionStore();
            var client = new SupabaseClient(
                "https://example.supabase.co",
                "publishable-test-identifier",
                transport,
                sessionStore);

            string imagePath = Path.Combine(_tempRoot, "owned.jpg");
            File.WriteAllBytes(imagePath, new byte[] { 1, 2, 3, 4 });
            const string promptId = "11111111-1111-4111-8111-111111111111";
            JournalRecord record = JournalRecord.CreateNew(
                "entry",
                "owned.jpg",
                10,
                20,
                promptId,
                "Prompt snapshot");
            record.id = recordId.ToString("N");

            RemoteOperationResult first = await client.DeliverAsync(
                record,
                _tempRoot,
                CancellationToken.None);
            RemoteOperationResult second = await client.DeliverAsync(
                record,
                _tempRoot,
                CancellationToken.None);

            Assert.IsTrue(first.Success, first.Error);
            Assert.IsTrue(second.Success, second.Error);
            Assert.AreEqual(recordId.ToString("D"), first.RemoteRecordId);
            Assert.AreEqual(5, transport.Requests.Count);

            SupabaseRequest signup = transport.Requests[0];
            StringAssert.EndsWith("/auth/v1/signup", signup.Url);
            AssertPublicAuthHeaders(signup);

            SupabaseRequest firstUpload = transport.Requests[1];
            SupabaseRequest secondUpload = transport.Requests[3];
            Assert.AreEqual(firstUpload.Url, secondUpload.Url);
            StringAssert.EndsWith(
                $"/journal-images/{userId:D}/{recordId:D}.jpg",
                firstUpload.Url);
            Assert.AreEqual("true", firstUpload.Headers["x-upsert"]);
            AssertUserAuthHeaders(firstUpload);
            AssertUserAuthHeaders(secondUpload);

            SupabaseRequest firstUpsert = transport.Requests[2];
            SupabaseRequest secondUpsert = transport.Requests[4];
            Assert.AreEqual(firstUpsert.Url, secondUpsert.Url);
            StringAssert.EndsWith("/journal_records?on_conflict=id", firstUpsert.Url);
            Assert.AreEqual(
                "resolution=merge-duplicates,return=minimal",
                firstUpsert.Headers["Prefer"]);
            Assert.AreEqual(
                Encoding.UTF8.GetString(firstUpsert.Body),
                Encoding.UTF8.GetString(secondUpsert.Body));
            StringAssert.Contains($"\"id\":\"{recordId:D}\"", Encoding.UTF8.GetString(firstUpsert.Body));
            StringAssert.Contains($"\"prompt_id\":\"{promptId}\"", Encoding.UTF8.GetString(firstUpsert.Body));
            StringAssert.Contains("\"prompt_text\":\"Prompt snapshot\"", Encoding.UTF8.GetString(firstUpsert.Body));
            AssertUserAuthHeaders(firstUpsert);
            AssertUserAuthHeaders(secondUpsert);
            Assert.AreEqual(userId.ToString("D"), sessionStore.Session.userId);
        }

        [Test]
        public async Task Refresh_UsesApiKeyWithoutAuthorization_ThenDeliveryUsesUserToken()
        {
            Guid userId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            SupabaseSession expiredSession = CreateSession(userId);
            expiredSession.expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1;
            var sessionStore = new MemorySessionStore
            {
                Exists = true,
                Session = expiredSession
            };
            var transport = new FakeTransport(
                JsonResponse(CreateAuthResponse(userId)),
                SuccessResponse(),
                SuccessResponse());
            var client = new SupabaseClient(
                "https://example.supabase.co",
                "publishable-test-identifier",
                transport,
                sessionStore);

            File.WriteAllBytes(Path.Combine(_tempRoot, "refresh.jpg"), new byte[] { 1, 2, 3, 4 });
            JournalRecord record = JournalRecord.CreateNew("entry", "refresh.jpg", 10, 20);

            RemoteOperationResult result = await client.DeliverAsync(
                record,
                _tempRoot,
                CancellationToken.None);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3, transport.Requests.Count);

            SupabaseRequest refresh = transport.Requests[0];
            StringAssert.EndsWith("/auth/v1/token?grant_type=refresh_token", refresh.Url);
            AssertPublicAuthHeaders(refresh);
            AssertUserAuthHeaders(transport.Requests[1]);
            AssertUserAuthHeaders(transport.Requests[2]);
        }

        [Test]
        public async Task DeliverLegacyRecord_OmitsEmptyPromptIdFromJournalUpsertBody()
        {
            Guid userId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            Guid recordId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
            var sessionStore = new MemorySessionStore
            {
                Exists = true,
                Session = CreateSession(userId)
            };
            var transport = new FakeTransport(
                SuccessResponse(),
                SuccessResponse());
            var client = new SupabaseClient(
                "https://example.supabase.co",
                "publishable-test-identifier",
                transport,
                sessionStore);

            File.WriteAllBytes(Path.Combine(_tempRoot, "legacy.jpg"), new byte[] { 1, 2, 3, 4 });
            JournalRecord record = JournalRecord.CreateNew("legacy entry", "legacy.jpg", 10, 20);
            record.id = recordId.ToString("N");

            RemoteOperationResult result = await client.DeliverAsync(
                record,
                _tempRoot,
                CancellationToken.None);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, transport.Requests.Count);
            string requestBody = Encoding.UTF8.GetString(transport.Requests[1].Body);
            StringAssert.DoesNotContain("\"prompt_id\":\"\"", requestBody);
            StringAssert.DoesNotContain("\"prompt_id\"", requestBody);
            Assert.AreEqual(string.Empty, record.promptId);
            Assert.AreEqual(string.Empty, record.promptText);
        }

        [Test]
        public async Task FetchActivePrompt_UsesConfigSelectionAndFallsBackOnFailure()
        {
            Guid userId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            Guid promptAId = Guid.Parse("11111111-1111-4111-8111-111111111111");
            Guid promptBId = Guid.Parse("22222222-2222-4222-8222-222222222222");
            var sessionStore = new MemorySessionStore
            {
                Exists = true,
                Session = CreateSession(userId)
            };
            var transport = new FakeTransport(
                JsonResponse($"[{{\"active_prompt_id\":\"{promptAId:D}\"}}]"),
                JsonResponse($"[{{\"id\":\"{promptAId:D}\",\"prompt_text\":\"Prompt A\"}}]"),
                JsonResponse($"[{{\"active_prompt_id\":\"{promptBId:D}\"}}]"),
                JsonResponse($"[{{\"id\":\"{promptBId:D}\",\"prompt_text\":\"Prompt B\"}}]"),
                new SupabaseResponse
                {
                    StatusCode = 503,
                    Error = "offline",
                    Body = string.Empty
                });
            var client = new SupabaseClient(
                "https://example.supabase.co",
                "publishable-test-identifier",
                transport,
                sessionStore);

            RemotePrompt promptA = await client.FetchActivePromptAsync(CancellationToken.None);
            RemotePrompt promptB = await client.FetchActivePromptAsync(CancellationToken.None);
            RemotePrompt fallback = await client.FetchActivePromptAsync(CancellationToken.None);

            Assert.IsFalse(promptA.IsFallback);
            Assert.AreEqual(promptAId.ToString("D"), promptA.Id);
            Assert.AreEqual("Prompt A", promptA.Text);
            Assert.IsFalse(promptB.IsFallback);
            Assert.AreEqual(promptBId.ToString("D"), promptB.Id);
            Assert.AreEqual("Prompt B", promptB.Text);
            Assert.IsTrue(fallback.IsFallback);
            Assert.AreEqual(RemotePrompt.FallbackText, fallback.Text);
            StringAssert.Contains("HTTP 503", fallback.Error);
        }

        private static string CreateAuthResponse(Guid userId)
        {
            long expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
            return
                $"{{\"access_token\":\"access-test-value\",\"refresh_token\":\"refresh-test-value\"," +
                $"\"expires_in\":3600,\"expires_at\":{expiresAt},\"user\":{{\"id\":\"{userId:D}\"}}}}";
        }

        private static SupabaseSession CreateSession(Guid userId)
        {
            return new SupabaseSession
            {
                accessToken = "access-test-value",
                refreshToken = "refresh-test-value",
                userId = userId.ToString("D"),
                expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600
            };
        }

        private static SupabaseResponse JsonResponse(string body)
        {
            return new SupabaseResponse
            {
                StatusCode = 200,
                Body = body,
                Error = null
            };
        }

        private static SupabaseResponse SuccessResponse()
        {
            return new SupabaseResponse
            {
                StatusCode = 201,
                Body = string.Empty,
                Error = null
            };
        }

        private static void AssertPublicAuthHeaders(SupabaseRequest request)
        {
            Assert.AreEqual("publishable-test-identifier", request.Headers["apikey"]);
            Assert.IsFalse(request.Headers.ContainsKey("Authorization"));
        }

        private static void AssertUserAuthHeaders(SupabaseRequest request)
        {
            Assert.AreEqual("publishable-test-identifier", request.Headers["apikey"]);
            Assert.AreEqual("Bearer access-test-value", request.Headers["Authorization"]);
        }

        private sealed class FakeTransport : ISupabaseTransport
        {
            private readonly Queue<SupabaseResponse> _responses;

            public readonly List<SupabaseRequest> Requests = new List<SupabaseRequest>();

            public FakeTransport(params SupabaseResponse[] responses)
            {
                _responses = new Queue<SupabaseResponse>(responses);
            }

            public Task<SupabaseResponse> SendAsync(
                SupabaseRequest request,
                CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(_responses.Dequeue());
            }
        }

        private sealed class MemorySessionStore : ISupabaseSessionStore
        {
            public bool Exists;
            public SupabaseSession Session;

            public bool TryLoad(
                out SupabaseSession session,
                out bool fileExists,
                out string error)
            {
                session = Session;
                fileExists = Exists;
                error = null;
                return true;
            }

            public bool TrySave(SupabaseSession session, out string error)
            {
                Session = session;
                Exists = true;
                error = null;
                return true;
            }
        }
    }
}
