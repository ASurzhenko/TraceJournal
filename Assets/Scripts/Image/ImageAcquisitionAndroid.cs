#if UNITY_ANDROID && !UNITY_EDITOR
using System;

namespace TraceJournal.Image
{
    /// <summary>
    /// Requires NativeGallery (yasirkula, MIT license) imported at
    /// Assets/Plugins/NativeGallery. See THIRD_PARTY_NOTICES.md.
    /// NativeGallery.GetImageFromGallery already copies the picked image into an
    /// app-readable temp path and handles the runtime storage-permission request,
    /// so no content:// URI ever reaches this class.
    /// </summary>
    public class ImageAcquisitionAndroid : IImageAcquisition
    {
        public void PickImage(Action<ImagePickResult> onComplete)
        {
            NativeGallery.GetImageFromGallery(path =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    onComplete(ImagePickResult.Cancel());
                    return;
                }
                onComplete(ImagePickResult.Ok(path));
            }, "Choose an image", "image/*");
        }
    }
}
#endif
