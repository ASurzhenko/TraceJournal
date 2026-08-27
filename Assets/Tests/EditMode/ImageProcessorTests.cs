using System;
using System.IO;
using NUnit.Framework;
using TraceJournal.Image;
using UnityEngine;

namespace TraceJournal.Tests.EditMode
{
    public class ImageProcessorTests
    {
        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                $"TraceJournalImageProcessorTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, recursive: true);
            }
        }

        [Test]
        public void TryCopyNormalizeAndResize_FlattensTransparentPngOntoWhite()
        {
            string sourcePath = Path.Combine(_temporaryDirectory, "transparent.png");
            Texture2D source = CreateHalfTransparentTexture();

            try
            {
                File.WriteAllBytes(sourcePath, source.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }

            bool succeeded = ImageProcessor.TryCopyNormalizeAndResize(
                sourcePath,
                _temporaryDirectory,
                out string relativeFileName,
                out string outputPath,
                out int width,
                out int height,
                out string error);

            Assert.IsTrue(succeeded, error);
            Assert.That(relativeFileName, Does.EndWith(".jpg"));
            Assert.That(width, Is.EqualTo(32));
            Assert.That(height, Is.EqualTo(32));

            Texture2D output = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(output.LoadImage(File.ReadAllBytes(outputPath)));

                Color32 opaqueRed = output.GetPixel(4, 16);
                Assert.That(opaqueRed.r, Is.GreaterThan(200));
                Assert.That(opaqueRed.g, Is.LessThan(50));
                Assert.That(opaqueRed.b, Is.LessThan(50));

                Color32 flattenedWhite = output.GetPixel(28, 16);
                Assert.That(flattenedWhite.r, Is.GreaterThan(240));
                Assert.That(flattenedWhite.g, Is.GreaterThan(240));
                Assert.That(flattenedWhite.b, Is.GreaterThan(240));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(output);
            }
        }

        private static Texture2D CreateHalfTransparentTexture()
        {
            const int Size = 32;
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = x < Size / 2
                        ? new Color32(255, 0, 0, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
