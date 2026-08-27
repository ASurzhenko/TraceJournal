using System;
using System.Collections;
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
        private int _renderGeneration;

        /// Renders the full list newest-first. imagesDir is used to resolve each
        /// record's relative image path to load a thumbnail texture.
        public void Render(
            List<JournalRecord> records,
            string imagesDir,
            Action<string> onRetry)
        {
            int thisGeneration = ++_renderGeneration;
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
                JournalRowView row = Instantiate(rowPrefab, contentRoot);
                row.Bind(record, onRetry);
                _spawnedRows.Add(row);
                StartCoroutine(LoadThumbnailNextFrame(
                    row,
                    record,
                    imagesDir,
                    thisGeneration));
            }
        }

        private IEnumerator LoadThumbnailNextFrame(
            JournalRowView row,
            JournalRecord record,
            string imagesDir,
            int renderGeneration)
        {
            yield return null;
            if (renderGeneration != _renderGeneration || row == null)
            {
                yield break;
            }

            Texture2D thumbnail = LoadThumbnail(record, imagesDir);
            if (renderGeneration != _renderGeneration || row == null)
            {
                if (thumbnail != null)
                {
                    Destroy(thumbnail);
                }

                yield break;
            }

            row.SetThumbnail(thumbnail);
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
            _renderGeneration++;
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

            try
            {
                return NativeGallery.LoadImageAtPath(
                    absolute,
                    ThumbnailMaxLongEdge,
                    markTextureNonReadable: true,
                    generateMipmaps: false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"{nameof(JournalListView)}.{nameof(LoadThumbnail)} [Thumbnail] id={record.id}, error={ex.Message}");
                return null;
            }
        }
    }
}
