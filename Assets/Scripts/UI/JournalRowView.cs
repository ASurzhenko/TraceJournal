using TMPro;
using TraceJournal.Models;
using UnityEngine;
using UnityEngine.UI;

namespace TraceJournal.UI
{
    public class JournalRowView : MonoBehaviour
    {
        private const int MaxPreviewChars = 80;

        [SerializeField] private RawImage thumbnail;
        [SerializeField] private TMP_Text timestampText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text statusText;

        private Texture2D _thumbnailTexture;

        /// The record's stable id — callbacks/consumers must key off this, never
        /// off transform sibling index or list position.
        public string RecordId { get; private set; }

        public void Bind(JournalRecord record, Texture2D thumbnailTexture)
        {
            RecordId = record.id;
            ReleaseThumbnail();
            _thumbnailTexture = thumbnailTexture;

            if (thumbnail != null)
            {
                thumbnail.texture = _thumbnailTexture;
            }

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
                statusText.text = record.syncState.ToString();
            }
        }

        public void ReleaseThumbnail()
        {
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
            ReleaseThumbnail();
        }
    }
}
