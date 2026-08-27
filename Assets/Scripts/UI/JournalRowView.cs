using System;
using System.Collections;
using TMPro;
using TraceJournal.Models;
using UnityEngine;
using UnityEngine.UI;

namespace TraceJournal.UI
{
    public class JournalRowView : MonoBehaviour
    {
        private const int MaxPreviewChars = 80;
        private static readonly float ThumbnailFadeDurationSeconds = 0.5f;

        [SerializeField] private RawImage thumbnail;
        [SerializeField] private GameObject loadingSpinner;
        [SerializeField] private TMP_Text timestampText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text statusText;

        private Texture2D _thumbnailTexture;
        private Coroutine _thumbnailFadeCoroutine;
        private Action<string> _onRetry;
        private Button _retryButton;

        /// The record's stable id — callbacks/consumers must key off this, never
        /// off transform sibling index or list position.
        public string RecordId { get; private set; }

        private void Awake()
        {
            _retryButton = statusText.GetComponent<Button>();
            if (_retryButton == null)
            {
                _retryButton = statusText.gameObject.AddComponent<Button>();
                _retryButton.targetGraphic = statusText;
            }

            _retryButton.onClick.AddListener(OnRetryClicked);
        }

        public void Bind(
            JournalRecord record,
            Action<string> onRetry)
        {
            RecordId = record.id;
            _onRetry = onRetry;
            ReleaseThumbnail();
            loadingSpinner.SetActive(true);

            if (timestampText != null)
            {
                System.DateTime utc = record.GetCreatedUtcDateTime();
                System.DateTime local = utc.ToLocalTime();
                timestampText.text = local.ToString("yyyy-MM-dd HH:mm");
            }

            if (bodyText != null)
            {
                string text = record.text ?? string.Empty;
                bodyText.text = text.Length > MaxPreviewChars
                    ? text.Substring(0, MaxPreviewChars) + "…"
                    : text;
            }

            if (statusText != null)
            {
                statusText.text = record.syncState == SyncState.Failed
                    ? "Failed · Retry"
                    : record.syncState.ToString();
            }

            _retryButton.interactable = record.syncState == SyncState.Failed;
        }

        public void SetThumbnail(Texture2D thumbnailTexture)
        {
            ReleaseThumbnail();
            _thumbnailTexture = thumbnailTexture;
            thumbnail.texture = _thumbnailTexture;
            SetThumbnailAlpha(0f);
            loadingSpinner.SetActive(false);

            if (_thumbnailTexture != null)
            {
                _thumbnailFadeCoroutine = StartCoroutine(FadeThumbnailIn());
            }
        }

        public void ReleaseThumbnail()
        {
            StopThumbnailFade();
            loadingSpinner.SetActive(false);
            SetThumbnailAlpha(0f);

            if (thumbnail != null && thumbnail.texture == _thumbnailTexture)
            {
                thumbnail.texture = null;
            }

            if (_thumbnailTexture != null)
            {
                Destroy(_thumbnailTexture);
                _thumbnailTexture = null;
            }
        }

        private void OnDestroy()
        {
            _retryButton.onClick.RemoveListener(OnRetryClicked);
            ReleaseThumbnail();
        }

        private void OnRetryClicked()
        {
            _onRetry?.Invoke(RecordId);
        }

        private IEnumerator FadeThumbnailIn()
        {
            float elapsed = 0f;
            while (elapsed < ThumbnailFadeDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetThumbnailAlpha(Mathf.Clamp01(elapsed / ThumbnailFadeDurationSeconds));
                yield return null;
            }

            SetThumbnailAlpha(1f);
            _thumbnailFadeCoroutine = null;
        }

        private void SetThumbnailAlpha(float alpha)
        {
            Color color = thumbnail.color;
            color.a = alpha;
            thumbnail.color = color;
        }

        private void StopThumbnailFade()
        {
            if (_thumbnailFadeCoroutine == null)
            {
                return;
            }

            StopCoroutine(_thumbnailFadeCoroutine);
            _thumbnailFadeCoroutine = null;
        }
    }
}
