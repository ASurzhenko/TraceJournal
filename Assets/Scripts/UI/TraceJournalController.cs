using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TraceJournal.Data;
using TraceJournal.Image;
using TraceJournal.Models;
using TraceJournal.Validation;
using UnityEngine;
using UnityEngine.UI;

namespace TraceJournal.UI
{
    public class TraceJournalController : MonoBehaviour
    {
        private static readonly int PreviewMaxLongEdge = 800;

        [SerializeField] private ComposerView composerView;
        [SerializeField] private JournalListView listView;
        [SerializeField] private Button newEntryButton;
        [SerializeField] private string supabaseUrl;
        [SerializeField] private string supabasePublishableKey;

        private readonly HashSet<string> _recordsInFlight = new HashSet<string>();

        private JournalRepository _repository;
        private IImageAcquisition _imageAcquisition;
        private SupabaseClient _supabaseClient;
        private CancellationTokenSource _lifetimeCancellation;
        private RemotePrompt _activePrompt;

        private int _pickGeneration;
        private int _promptGeneration;
        private bool _isDestroying;

        private string _pendingImageRelativeFileName;
        private string _pendingImageAbsolutePath;
        private int _pendingImageWidth;
        private int _pendingImageHeight;
        private Texture2D _pendingPreviewTexture;

        private void Awake()
        {
            _repository = new JournalRepository();
            _lifetimeCancellation = new CancellationTokenSource();
            _activePrompt = RemotePrompt.Fallback();
            _supabaseClient = new SupabaseClient(
                supabaseUrl,
                supabasePublishableKey,
                new SupabaseUnityTransport(),
                new SupabaseSessionFileStore(Application.persistentDataPath));

#if UNITY_EDITOR
            _imageAcquisition = new ImageAcquisitionEditor();
#elif UNITY_ANDROID
            _imageAcquisition = new ImageAcquisitionAndroid();
#endif

            composerView.ChooseImageButton.onClick.AddListener(OnChooseImageClicked);
            composerView.SaveButton.onClick.AddListener(OnSaveClicked);
            composerView.CancelButton.onClick.AddListener(OnCancelClicked);
            composerView.CloseButton.onClick.AddListener(OnCancelClicked);
            newEntryButton.onClick.AddListener(OnNewEntryClicked);

            ApplyPrompt(_activePrompt);
            composerView.Hide();
        }

        private async void Start()
        {
            RefreshList();
            await RefreshPromptAsync();
            await DeliverPendingAtStartupAsync();
        }

        private void OnDestroy()
        {
            _isDestroying = true;
            _pickGeneration++;
            _promptGeneration++;
            _lifetimeCancellation.Cancel();

            composerView.ChooseImageButton.onClick.RemoveListener(OnChooseImageClicked);
            composerView.SaveButton.onClick.RemoveListener(OnSaveClicked);
            composerView.CancelButton.onClick.RemoveListener(OnCancelClicked);
            composerView.CloseButton.onClick.RemoveListener(OnCancelClicked);
            newEntryButton.onClick.RemoveListener(OnNewEntryClicked);

            DiscardPendingImage(deleteOwnedFile: true, resetComposer: false);
            _lifetimeCancellation.Dispose();
        }

        private async void OnNewEntryClicked()
        {
            _pickGeneration++;
            ClearPendingImage();
            composerView.Show();
            await RefreshPromptAsync();
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
            composerView.BeginPreviewLoading();

            _imageAcquisition.PickImage(result =>
            {
                if (this == null || thisGeneration != _pickGeneration)
                {
                    return;
                }

                if (result.Cancelled)
                {
                    composerView.EndPreviewLoading();
                    return;
                }

                if (!result.Success)
                {
                    composerView.EndPreviewLoading();
                    composerView.SetError(result.Error ?? "Could not read the selected image.");
                    return;
                }

                StartCoroutine(HandlePickedImageNextFrame(result.SourcePath, thisGeneration));
            });
        }

        private IEnumerator HandlePickedImageNextFrame(string sourcePath, int pickGeneration)
        {
            yield return null;
            if (this == null || pickGeneration != _pickGeneration)
            {
                yield break;
            }

            HandlePickedImage(sourcePath);
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
                composerView.EndPreviewLoading();
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
            catch (Exception ex)
            {
                TryDeleteFile(absoluteOutputPath);
                composerView.EndPreviewLoading();
                Debug.LogWarning(
                    $"{nameof(TraceJournalController)}.{nameof(HandlePickedImage)} [Preview] error={ex.Message}");
                composerView.SetError("Could not preview the selected image.");
                return;
            }

            if (newPreviewTexture == null)
            {
                TryDeleteFile(absoluteOutputPath);
                composerView.EndPreviewLoading();
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

        private async void OnSaveClicked()
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
                text,
                _pendingImageRelativeFileName,
                _pendingImageWidth,
                _pendingImageHeight,
                _activePrompt.Id,
                _activePrompt.Text);

            bool saved = _repository.TryAppend(record, _pendingImageAbsolutePath, out string saveError);
            if (!saved)
            {
                DiscardPendingImage(deleteOwnedFile: true, resetComposer: false);
                composerView.SetError(
                    $"{saveError ?? "Could not save entry."} Choose the image again before retrying.");
                return;
            }

            _pickGeneration++;
            _pendingImageRelativeFileName = null;
            _pendingImageAbsolutePath = null;

            ClearPendingImage();
            composerView.Hide();
            RefreshList();
            await TryDeliverRecordAsync(record.id, isExplicitRetry: false);
        }

        private void OnCancelClicked()
        {
            _pickGeneration++;
            ClearPendingImage();
            composerView.Hide();
        }

        private async Task RefreshPromptAsync()
        {
            _promptGeneration++;
            int thisGeneration = _promptGeneration;

            try
            {
                RemotePrompt prompt = await _supabaseClient.FetchActivePromptAsync(
                    _lifetimeCancellation.Token);
                if (this == null ||
                    _isDestroying ||
                    thisGeneration != _promptGeneration)
                {
                    return;
                }

                ApplyPrompt(prompt);
                if (prompt.IsFallback && !string.IsNullOrEmpty(prompt.Error))
                {
                    Debug.LogWarning(
                        $"{nameof(TraceJournalController)}.{nameof(RefreshPromptAsync)} [Fallback] {prompt.Error}");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (this != null && thisGeneration == _promptGeneration)
                {
                    ApplyPrompt(RemotePrompt.Fallback("The prompt request failed unexpectedly."));
                    Debug.LogWarning(
                        $"{nameof(TraceJournalController)}.{nameof(RefreshPromptAsync)} [Request] error={ex.Message}");
                }
            }
        }

        private void ApplyPrompt(RemotePrompt prompt)
        {
            _activePrompt = prompt;
            composerView.SetPrompt(prompt.Text);
        }

        private async Task DeliverPendingAtStartupAsync()
        {
            List<JournalRecord> records = _repository.LoadAll();
            foreach (JournalRecord record in records)
            {
                if (_isDestroying)
                {
                    return;
                }

                if (record.syncState == SyncState.Pending)
                {
                    await TryDeliverRecordAsync(record.id, isExplicitRetry: false);
                }
            }
        }

        private async void OnRetryRequested(string recordId)
        {
            await TryDeliverRecordAsync(recordId, isExplicitRetry: true);
        }

        private async Task TryDeliverRecordAsync(string recordId, bool isExplicitRetry)
        {
            if (_recordsInFlight.Contains(recordId))
            {
                Debug.LogWarning(
                    $"{nameof(TraceJournalController)}.{nameof(TryDeliverRecordAsync)} [Duplicate] id={recordId}");
                return;
            }

            if (!_repository.TryGet(recordId, out JournalRecord record, out string loadError))
            {
                Debug.LogError(
                    $"{nameof(TraceJournalController)}.{nameof(TryDeliverRecordAsync)} [Load] id={recordId}, error={loadError}");
                return;
            }

            if (record.syncState == SyncState.Synced)
            {
                Debug.LogWarning(
                    $"{nameof(TraceJournalController)}.{nameof(TryDeliverRecordAsync)} [AlreadySynced] id={recordId}");
                return;
            }

            if (isExplicitRetry &&
                !_repository.TryUpdateRemoteState(
                    recordId,
                    SyncState.Pending,
                    record.remoteId,
                    string.Empty,
                    out string pendingError))
            {
                Debug.LogError(
                    $"{nameof(TraceJournalController)}.{nameof(TryDeliverRecordAsync)} [Pending] id={recordId}, error={pendingError}");
                return;
            }

            _recordsInFlight.Add(recordId);
            RefreshList();

            try
            {
                RemoteOperationResult result = await _supabaseClient.DeliverAsync(
                    record,
                    _repository.ImagesDirectory,
                    _lifetimeCancellation.Token);
                if (this == null || _isDestroying)
                {
                    return;
                }

                SyncState state = result.Success ? SyncState.Synced : SyncState.Failed;
                string remoteId = result.Success ? result.RemoteRecordId : record.remoteId;
                string syncError = result.Success ? string.Empty : result.Error;
                if (!_repository.TryUpdateRemoteState(
                        recordId,
                        state,
                        remoteId,
                        syncError,
                        out string updateError))
                {
                    Debug.LogError(
                        $"{nameof(TraceJournalController)}.{nameof(TryDeliverRecordAsync)} [Persist] id={recordId}, error={updateError}");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (this != null && !_isDestroying)
                {
                    if (!_repository.TryUpdateRemoteState(
                            recordId,
                            SyncState.Failed,
                            record.remoteId,
                            "The remote request failed unexpectedly.",
                            out string persistError))
                    {
                        Debug.LogError(
                            $"{nameof(TraceJournalController)}.{nameof(TryDeliverRecordAsync)} [PersistFailure] id={recordId}, error={persistError}");
                    }

                    Debug.LogWarning(
                        $"{nameof(TraceJournalController)}.{nameof(TryDeliverRecordAsync)} [Request] id={recordId}, error={ex.Message}");
                }
            }
            finally
            {
                _recordsInFlight.Remove(recordId);
                if (this != null && !_isDestroying)
                {
                    RefreshList();
                }
            }
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
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"{nameof(TraceJournalController)}.{nameof(TryDeleteFile)} [Cleanup] path={absolutePath}, error={ex.Message}");
            }
        }

        private void RefreshList()
        {
            List<JournalRecord> records = _repository.LoadAll();
            listView.Render(records, _repository.ImagesDirectory, OnRetryRequested);
        }
    }
}
