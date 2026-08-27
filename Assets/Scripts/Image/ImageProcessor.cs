using System;
using System.IO;
using UnityEngine;

namespace TraceJournal.Image
{
    public static class ImageProcessor
    {
        private const int MaxLongEdge = 1600;
        private const int JpegQuality = 85;

        /// Loads the file at sourcePath, resizes it (long edge capped at ~1600px),
        /// encodes it as JPEG, and writes it into imagesDir under a new unique
        /// filename. Always destroys every intermediate Texture2D it creates,
        /// including on the failure path.
        public static bool TryCopyNormalizeAndResize(
            string sourcePath,
            string imagesDir,
            out string relativeFileName,
            out string absoluteOutputPath,
            out int width,
            out int height,
            out string error)
        {
            relativeFileName = null;
            absoluteOutputPath = null;
            width = 0;
            height = 0;
            error = null;

            Texture2D sourceTex = null;
            Texture2D scaledTex = null;
            string candidateOutputPath = null;

            try
            {
                if (!File.Exists(sourcePath))
                {
                    error = "Selected image could not be found.";
                    return false;
                }

#if UNITY_ANDROID && !UNITY_EDITOR
                sourceTex = NativeGallery.LoadImageAtPath(
                    sourcePath,
                    MaxLongEdge,
                    markTextureNonReadable: false,
                    generateMipmaps: false);
                if (sourceTex == null)
                {
                    error = "Selected file is not a readable image.";
                    return false;
                }
#else
                byte[] fileBytes = File.ReadAllBytes(sourcePath);

                sourceTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!sourceTex.LoadImage(fileBytes))
                {
                    error = "Selected file is not a readable image.";
                    return false;
                }
#endif

                (int targetW, int targetH) = ComputeTargetSize(sourceTex.width, sourceTex.height);

                Texture2D encodeSource = sourceTex;
                if (targetW != sourceTex.width || targetH != sourceTex.height)
                {
                    scaledTex = ScaleTexture(sourceTex, targetW, targetH);
                    encodeSource = scaledTex;
                }

                byte[] jpegBytes = encodeSource.EncodeToJPG(JpegQuality);
                if (jpegBytes == null || jpegBytes.Length == 0)
                {
                    error = "Failed to encode image.";
                    return false;
                }

                string fileName = Guid.NewGuid().ToString("N") + ".jpg";
                candidateOutputPath = Path.Combine(imagesDir, fileName);

                Directory.CreateDirectory(imagesDir);
                File.WriteAllBytes(candidateOutputPath, jpegBytes);

                relativeFileName = fileName;
                absoluteOutputPath = candidateOutputPath;
                width = encodeSource.width;
                height = encodeSource.height;
                return true;
            }
            catch (Exception ex)
            {
                TryDeleteCandidate(candidateOutputPath);
                error = $"Image processing failed: {ex.Message}";
                return false;
            }
            finally
            {
                if (sourceTex != null) UnityEngine.Object.Destroy(sourceTex);
                if (scaledTex != null) UnityEngine.Object.Destroy(scaledTex);
            }
        }

        private static void TryDeleteCandidate(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"{nameof(ImageProcessor)}.{nameof(TryDeleteCandidate)} [Cleanup] path={path}, error={ex.Message}");
            }
        }

        private static (int, int) ComputeTargetSize(int w, int h)
        {
            int longEdge = Mathf.Max(w, h);
            if (longEdge <= MaxLongEdge) return (w, h);

            float scale = MaxLongEdge / (float)longEdge;
            int newW = Mathf.Max(1, Mathf.RoundToInt(w * scale));
            int newH = Mathf.Max(1, Mathf.RoundToInt(h * scale));
            return (newW, newH);
        }

        private static Texture2D ScaleTexture(Texture2D source, int targetW, int targetH)
        {
            var rt = RenderTexture.GetTemporary(targetW, targetH, 0, RenderTextureFormat.ARGB32);
            RenderTexture prevActive = RenderTexture.active;
            Texture2D result = null;

            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                result = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
                result.Apply();
                return result;
            }
            catch
            {
                if (result != null)
                {
                    UnityEngine.Object.Destroy(result);
                }

                throw;
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
