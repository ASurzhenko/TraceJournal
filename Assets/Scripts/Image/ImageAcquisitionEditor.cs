#if UNITY_EDITOR
using System;
using UnityEditor;

namespace TraceJournal.Image
{
    /// <summary>
    /// Editor-only stand-in for the Android gallery picker, so the flow is fully
    /// testable/smoke-testable in Play Mode inside the Editor.
    /// </summary>
    public class ImageAcquisitionEditor : IImageAcquisition
    {
        public void PickImage(Action<ImagePickResult> onComplete)
        {
            string path = EditorUtility.OpenFilePanelWithFilters(
                "Choose an image", "", new[] { "Images", "jpg,jpeg,png" });

            if (string.IsNullOrEmpty(path))
            {
                onComplete(ImagePickResult.Cancel());
                return;
            }

            onComplete(ImagePickResult.Ok(path));
        }
    }
}
#endif
