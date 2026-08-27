using System;

namespace TraceJournal.Image
{
    public class ImagePickResult
    {
        public bool Success;
        public bool Cancelled;
        public string SourcePath; // absolute path to a readable file; never a provider URI
        public string Error;

        public static ImagePickResult Ok(string path) => new ImagePickResult { Success = true, SourcePath = path };
        public static ImagePickResult Cancel() => new ImagePickResult { Cancelled = true };
        public static ImagePickResult Fail(string error) => new ImagePickResult { Error = error };
    }

    /// <summary>
    /// Platform seam for picking a gallery image. Implementations must resolve to
    /// a locally readable file path (not a content:// URI) before invoking the
    /// callback — provider-URI normalization is NOT ImageProcessor's job, it must
    /// already be a real path by the time it reaches ImageProcessor.
    /// </summary>
    public interface IImageAcquisition
    {
        void PickImage(Action<ImagePickResult> onComplete);
    }
}
