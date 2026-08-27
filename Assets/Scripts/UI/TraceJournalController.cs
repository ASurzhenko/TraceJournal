using System.IO;
using TraceJournal.Data;
using TraceJournal.Image;
using TraceJournal.Models;
using TraceJournal.Validation;
using UnityEngine;
using UnityEngine.UI;

namespace TraceJournal.UI
{
    /// <summary>
    /// Single composition/controller MonoBehaviour for F1. No DI container by
    /// design — everything is wired directly in Awake.
    /// </summary>
    public class TraceJournalController : MonoBehaviour
    {
        private static readonly int PreviewMaxLongEdge = 800;

        [SerializeField] private ComposerView composerView;
        [SerializeField] private JournalListView listView;
        [SerializeField] private Button newEntryButton;

        private JournalRepository _repository;
        private IImageAcquisition _imageAcquisition;

        // Guards against a cancelled/failed/superseded pick result being applied
        // after a newer pick has started.
        private int _pickGeneration;

        // State for the image currently attached to the open composer, not yet
        // committed to a saved record.
        private string _pendingImageRelativeFileName;
        private string _pendingImageAbsolutePath;
        private int _pendingImageWidth;
        private int _pendingImageHeight;
        private Texture2D _pendingPreviewTexture;

        private void Awake()
        {
            _repository = new JournalRepository();

#if UNITY_EDITOR
            _imageAcquisition = new ImageAcquisitionEditor();
#elif UNITY_ANDROID
            _imageAcquisition = new ImageAcquisitionAndroid();
#endif

            composerView.ChooseImageButton.onClick.AddListener(OnChooseImageClicked);
            composerView.SaveButton.onClick.AddListener(OnSaveClicked);
            composerView.CancelButton.onClick.AddListener(OnCancelClicked);
            newEntryButton.onClick.AddListener(OnNewEntryClicked);

            composerView.Hide();
        }

        private void Start()
        {
            RefreshList();
        }

        private void OnDestroy()
        {
            _pickGeneration++;

            composerView.ChooseImageButton.onClick.RemoveListener(OnChooseImageClicked);
            composerView.SaveButton.onClick.RemoveListener(OnSaveClicked);
            composerView.CancelButton.onClick.RemoveListener(OnCancelClicked);
            newEntryButton.onClick.RemoveListener(OnNewEntryClicked);

            DiscardPendingImage(deleteOwnedFile: true, resetComposer: false);
        }

        private void OnNewEntryClicked()
        {
            _pickGeneration++;
            ClearPendingImage();
            composerView.Show();
        }

        private void OnChooseImageClicked()
        {
            if (_imageAcquisition == null)
            {
                composerView.SetError("Image picking is not available on this platform.");
                return;
            }

            _pickGeneration++;
            int thisGeneration = _pickGeneration;

            _imageAcquisition.PickImage(result =>
            {
                // A newer pick started, or this composer session ended — ignore.
                if (this == null || thisGeneration != _pickGeneration)
                {
                    return;
                }

                if (result.Cancelled)
                {
                    return;
                }

                if (!result.Success)
                {
                    composerView.SetError(result.Error ?? "Could not read the selected image.");
                    return;
                }

                HandlePickedImage(result.SourcePath);
            });
        }

        private void HandlePickedImage(string sourcePath)
        {
            bool ok = ImageProcessor.TryCopyNormalizeAndResize(
                sourcePath,
                _repository.ImagesDirectory,
                out string relativeFileName,
                out string absoluteOutputPath,
                out int width,
                out int height,
                out string error);

            if (!ok)
            {
                composerView.SetError(error);
                return;
            }

            Texture2D newPreviewTexture;
            try
            {
                newPreviewTexture = NativeGallery.LoadImageAtPath(
                    absoluteOutputPath,
                    PreviewMaxLongEdge,
                    markTextureNonReadable: true,
                    generateMipmaps: false);
            }
            catch (System.Exception ex)
            {
                TryDeleteFile(absoluteOutputPath);
                Debug.LogWarning(
                    $"{nameof(TraceJournalController)}.{nameof(HandlePickedImage)} [Preview] error={ex.Message}");
                composerView.SetError("Could not preview the selected image.");
                return;
            }

            if (newPreviewTexture == null)
            {
                TryDeleteFile(absoluteOutputPath);
                composerView.SetError("Could not preview the selected image.");
                return;
            }

            string replacedImagePath = _pendingImageAbsolutePath;
            Texture2D replacedPreviewTexture = _pendingPreviewTexture;

            _pendingImageRelativeFileName = relativeFileName;
            _pendingImageAbsolutePath = absoluteOutputPath;
            _pendingImageWidth = width;
            _pendingImageHeight = height;
            _pendingPreviewTexture = newPreviewTexture;

            composerView.SetPreview(newPreviewTexture);
            composerView.SetError(null);

            TryDeleteFile(replacedImagePath);
            if (replacedPreviewTexture != null)
            {
                Destroy(replacedPreviewTexture);
            }
        }

        private void OnSaveClicked()
        {
            string text = composerView.CurrentText;
            bool hasOwnedImage = HasPendingOwnedImage();
            if (!hasOwnedImage && HasPendingImageMetadata())
            {
                Debug.LogWarning(
                    $"{nameof(TraceJournalController)}.{nameof(OnSaveClicked)} [Image] pending owned file is missing");
                DiscardPendingImage(deleteOwnedFile: true, resetComposer: false);
            }

            if (!JournalValidator.Validate(text, hasOwnedImage, out string error))
            {
                composerView.SetError(error);
                return;
            }

            JournalRecord record = JournalRecord.CreateNew(
                text, _pendingImageRelativeFileName, _pendingImageWidth, _pendingImageHeight);

            bool saved = _repository.TryAppend(record, _pendingImageAbsolutePath, out string saveError);
            if (!saved)
            {
                DiscardPendingImage(deleteOwnedFile: true, resetComposer: false);
                composerView.SetError(
                    $"{saveError ?? "Could not save entry."} Choose the image again before retrying.");
                return;
            }

            _pickGeneration++;

            // Ownership of the image file transfers to the saved record; don't
            // delete it on subsequent composer resets.
            _pendingImageRelativeFileName = null;
            _pendingImageAbsolutePath = null;

            ClearPendingImage();
            composerView.Hide();
            RefreshList();
        }

        private void OnCancelClicked()
        {
            // Invalidate any in-flight pick so a late callback can't reopen state.
            _pickGeneration++;
            ClearPendingImage();
            composerView.Hide();
        }

        private void ClearPendingImage()
        {
            DiscardPendingImage(deleteOwnedFile: true, resetComposer: true);
        }

        private void DiscardPendingImage(bool deleteOwnedFile, bool resetComposer)
        {
            if (deleteOwnedFile)
            {
                TryDeleteFile(_pendingImageAbsolutePath);
            }

            if (_pendingPreviewTexture != null)
            {
                Destroy(_pendingPreviewTexture);
                _pendingPreviewTexture = null;
            }

            _pendingImageRelativeFileName = null;
            _pendingImageAbsolutePath = null;
            _pendingImageWidth = 0;
            _pendingImageHeight = 0;

            if (resetComposer)
            {
                composerView.ResetFields();
            }
            else
            {
                composerView.SetPreview(null);
            }
        }

        private bool HasPendingOwnedImage()
        {
            return !string.IsNullOrEmpty(_pendingImageRelativeFileName) &&
                   !string.IsNullOrEmpty(_pendingImageAbsolutePath) &&
                   File.Exists(_pendingImageAbsolutePath);
        }

        private bool HasPendingImageMetadata()
        {
            return !string.IsNullOrEmpty(_pendingImageRelativeFileName) ||
                   !string.IsNullOrEmpty(_pendingImageAbsolutePath);
        }

        private void TryDeleteFile(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return;
            }

            try
            {
                if (File.Exists(absolutePath))
                {
                    File.Delete(absolutePath);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"{nameof(TraceJournalController)}.{nameof(TryDeleteFile)} [Cleanup] path={absolutePath}, error={ex.Message}");
            }
        }

        private void RefreshList()
        {
            var records = _repository.LoadAll();
            listView.Render(records, _repository.ImagesDirectory);
        }
    }
}
