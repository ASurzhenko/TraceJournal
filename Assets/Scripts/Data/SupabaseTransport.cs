using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace TraceJournal.Data
{
    public sealed class SupabaseRequest
    {
        public string Method;
        public string Url;
        public byte[] Body;
        public readonly Dictionary<string, string> Headers = new Dictionary<string, string>();
    }

    public sealed class SupabaseResponse
    {
        public long StatusCode;
        public string Body;
        public string Error;

        public bool IsSuccess => StatusCode >= 200 && StatusCode <= 299;
    }

    public interface ISupabaseTransport
    {
        Task<SupabaseResponse> SendAsync(SupabaseRequest request, CancellationToken cancellationToken);
    }

    public sealed class SupabaseUnityTransport : ISupabaseTransport
    {
        private static readonly int RequestTimeoutSeconds = 15;

        public async Task<SupabaseResponse> SendAsync(
            SupabaseRequest requestData,
            CancellationToken cancellationToken)
        {
            using (var request = new UnityWebRequest(requestData.Url, requestData.Method))
            {
                request.timeout = RequestTimeoutSeconds;
                request.downloadHandler = new DownloadHandlerBuffer();
                if (requestData.Body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(requestData.Body);
                }

                foreach (KeyValuePair<string, string> header in requestData.Headers)
                {
                    request.SetRequestHeader(header.Key, header.Value);
                }

                var completion = new TaskCompletionSource<bool>();
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                operation.completed += _ => completion.TrySetResult(true);

                using (cancellationToken.Register(() =>
                       {
                           request.Abort();
                           completion.TrySetCanceled(cancellationToken);
                       }))
                {
                    await completion.Task;
                }

                return new SupabaseResponse
                {
                    StatusCode = request.responseCode,
                    Body = request.downloadHandler.text,
                    Error = request.result == UnityWebRequest.Result.Success
                        ? null
                        : request.error
                };
            }
        }
    }
}
