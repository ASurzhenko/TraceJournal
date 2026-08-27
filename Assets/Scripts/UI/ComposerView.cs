using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TraceJournal.UI
{
    public class ComposerView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_InputField textInput;
        [SerializeField] private RawImage previewImage;
        [SerializeField] private GameObject previewPlaceholder;
        [SerializeField] private Button chooseImageButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text errorText;

        public Button ChooseImageButton => chooseImageButton;
        public Button SaveButton => saveButton;
        public Button CancelButton => cancelButton;

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

            previewImage.texture = texture;
            previewImage.gameObject.SetActive(texture != null);
            if (previewPlaceholder != null) previewPlaceholder.SetActive(texture == null);
        }

        public void SetError(string message)
        {
            if (errorText == null) return;
            bool has = !string.IsNullOrEmpty(message);
            errorText.gameObject.SetActive(has);
            errorText.text = has ? message : string.Empty;
        }
    }
}
