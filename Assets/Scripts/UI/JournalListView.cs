using System.Collections.Generic;
using System.IO;
using System.Linq;
using TraceJournal.Models;
using UnityEngine;

namespace TraceJournal.UI
{
    public class JournalListView : MonoBehaviour
    {
        private static readonly int ThumbnailMaxLongEdge = 160;

        [SerializeField] private Transform contentRoot;
        [SerializeField] private JournalRowView rowPrefab;
        [SerializeField] private GameObject emptyStateRoot;

        private readonly List<JournalRowView> _spawnedRows = new List<JournalRowView>();

        /// Renders the full list newest-first. imagesDir is used to resolve each
        /// record's relative image path to load a thumbnail texture.
        public void Render(List<JournalRecord> records, string imagesDir)
        {
            ClearRows();

            List<JournalRecord> ordered = records
                .OrderByDescending(r => r.GetCreatedUtcDateTime())
                .ToList();

            if (emptyStateRoot != null)
            {
                emptyStateRoot.SetActive(ordered.Count == 0);
            }

            foreach (JournalRecord record in ordered)
            {
                Texture2D thumb = LoadThumbnail(record, imagesDir);
                JournalRowView row = Instantiate(rowPrefab, contentRoot);
                row.Bind(record, thumb);
                _spawnedRows.Add(row);
            }
        }

        private void ClearRows()
        {
            foreach (JournalRowView row in _spawnedRows)
            {
                if (row != null)
                {
                    row.ReleaseThumbnail();
                    Destroy(row.gameObject);
                }
            }

            _spawnedRows.Clear();
        }

        private void OnDestroy()
        {
            ClearRows();
        }

        private Texture2D LoadThumbnail(JournalRecord record, string imagesDir)
        {
            if (string.IsNullOrEmpty(record.imageRelativePath))
            {
                return null;
            }

            string absolute = Path.Combine(imagesDir, record.imageRelativePath);
            if (!File.Exists(absolute))
            {
                return null;
            }

            return NativeGallery.LoadImageAtPath(
                absolute,
                ThumbnailMaxLongEdge,
                markTextureNonReadable: true,
                generateMipmaps: false);
        }
    }
}
