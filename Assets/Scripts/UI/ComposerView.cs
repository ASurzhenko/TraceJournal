using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TraceJournal.UI
{
    public class ComposerView : MonoBehaviour
    {
        private static readonly float PreviewFadeDurationSeconds = 0.5f;
        private static readonly float PreviewFrameInset = 12f;
        private static readonly Vector2 PreviewFallbackMaximumSize = new Vector2(488f, 488f);

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_InputField textInput;
        [SerializeField] private RawImage previewImage;
        [SerializeField] private GameObject loadingSpinner;
        [SerializeField] private GameObject previewPlaceholder;
        [SerializeField] private Button chooseImageButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text errorText;

        private Coroutine _previewFadeCoroutine;

        public Button ChooseImageButton => chooseImageButton;
        public Button SaveButton => saveButton;
        public Button CancelButton => cancelButton;
        public Button CloseButton => closeButton;

        public string CurrentText => textInput != null ? textInput.text : string.Empty;

        public void Show()
        {
            panelRoot.SetActive(true);
            ResetFields();
        }

        public void Hide()
        {
            panelRoot.SetActive(false);
        }

        public void ResetFields()
        {
            if (textInput != null) textInput.text = string.Empty;
            SetPreview(null);
            SetError(null);
        }

        public void SetPreview(Texture2D texture)
        {
            if (previewImage == null) return;

            StopPreviewFade();
            loadingSpinner.SetActive(false);
            previewImage.texture = texture;
            FitPreviewInsideBounds(texture);
            previewImage.gameObject.SetActive(texture != null);
            if (previewPlaceholder != null) previewPlaceholder.SetActive(texture == null);

            SetPreviewAlpha(0f);
            if (texture != null)
            {
                _previewFadeCoroutine = StartCoroutine(FadePreviewIn());
            }
        }

        public void BeginPreviewLoading()
        {
            StopPreviewFade();
            SetPreviewAlpha(0f);
            loadingSpinner.SetActive(true);
            if (previewPlaceholder != null) previewPlaceholder.SetActive(false);
        }

        public void EndPreviewLoading()
        {
            loadingSpinner.SetActive(false);
            bool hasPreview = previewImage.texture != null;
            previewImage.gameObject.SetActive(hasPreview);
            SetPreviewAlpha(hasPreview ? 1f : 0f);
            if (previewPlaceholder != null) previewPlaceholder.SetActive(!hasPreview);
        }

        public void SetError(string message)
        {
            if (errorText == null) return;
            bool has = !string.IsNullOrEmpty(message);
            errorText.gameObject.SetActive(has);
            errorText.text = has ? message : string.Empty;
        }

        public void SetPrompt(string value)
        {
            if (textInput.placeholder is TMP_Text placeholder)
            {
                placeholder.text = value;
            }
        }

        private IEnumerator FadePreviewIn()
        {
            float elapsed = 0f;
            while (elapsed < PreviewFadeDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetPreviewAlpha(Mathf.Clamp01(elapsed / PreviewFadeDurationSeconds));
                yield return null;
            }

            SetPreviewAlpha(1f);
            _previewFadeCoroutine = null;
        }

        private void SetPreviewAlpha(float alpha)
        {
            Color color = previewImage.color;
            color.a = alpha;
            previewImage.color = color;
        }

        private void FitPreviewInsideBounds(Texture2D texture)
        {
            RectTransform previewRect = previewImage.rectTransform;
            Vector2 maximumSize = GetPreviewMaximumSize(previewRect);
            if (texture == null ||
                texture.width <= 0 ||
                texture.height <= 0)
            {
                previewRect.sizeDelta = maximumSize;
                return;
            }

            float textureAspect = (float)texture.width / texture.height;
            float boundsAspect = maximumSize.x / maximumSize.y;
            Vector2 fittedSize = maximumSize;

            if (textureAspect > boundsAspect)
            {
                fittedSize.y = fittedSize.x / textureAspect;
            }
            else
            {
                fittedSize.x = fittedSize.y * textureAspect;
            }

            previewRect.sizeDelta = fittedSize;
        }

        private static Vector2 GetPreviewMaximumSize(RectTransform previewRect)
        {
            if (previewRect.parent is not RectTransform parentRect)
            {
                return PreviewFallbackMaximumSize;
            }

            Vector2 parentSize = parentRect.rect.size;
            if (parentSize.x <= PreviewFrameInset || parentSize.y <= PreviewFrameInset)
            {
                return PreviewFallbackMaximumSize;
            }

            return new Vector2(
                parentSize.x - PreviewFrameInset,
                parentSize.y - PreviewFrameInset);
        }

        private void StopPreviewFade()
        {
            if (_previewFadeCoroutine == null)
            {
                return;
            }

            StopCoroutine(_previewFadeCoroutine);
            _previewFadeCoroutine = null;
        }

        private void OnDisable()
        {
            StopPreviewFade();
        }
    }
}
