using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TraceJournal.Data;
using TraceJournal.Models;
using UnityEngine;
using UnityEngine.TestTools;

namespace TraceJournal.Tests.EditMode
{
    public class JournalRepositoryTests
    {
        private string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "TraceJournalTests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
        }

        [Test]
        public void SaveThenLoad_RoundTripsRecord()
        {
            var repo = new JournalRepository(_tempRoot);
            string imagePath = CreateOwnedImage(repo, "round_trip.jpg");
            var record = JournalRecord.CreateNew("hello world", "round_trip.jpg", 100, 200);

            bool saved = repo.TryAppend(record, imagePath, out string error);
            Assert.IsTrue(saved, error);

            var reloaded = new JournalRepository(_tempRoot).LoadAll();

            Assert.AreEqual(1, reloaded.Count);
            Assert.AreEqual(record.id, reloaded[0].id);
            Assert.AreEqual("hello world", reloaded[0].text);
            Assert.AreEqual(SyncState.Pending, reloaded[0].syncState);
        }

        [Test]
        public void FailedSave_CleansUpNewlyCreatedImage()
        {
            var repo = new JournalRepository(_tempRoot);
            string fakeImagePath = CreateOwnedImage(repo, "orphan.jpg");

            // Force a failure by pointing the index at a path a File.Replace can't
            // use (a directory), simulating an unwritable index.
            string indexAsDirectory = Path.Combine(_tempRoot, "journal_index.json");
            Directory.CreateDirectory(indexAsDirectory);

            var record = JournalRecord.CreateNew("text", "orphan.jpg", 10, 10);
            bool saved = repo.TryAppend(record, fakeImagePath, out string error);

            Assert.IsFalse(saved);
            Assert.IsFalse(File.Exists(fakeImagePath), "Orphaned image should be cleaned up on failed save.");
        }

        [Test]
        public void CorruptIndex_AppendFailsWithoutOverwritingExistingData()
        {
            var repo = new JournalRepository(_tempRoot);
            string indexPath = Path.Combine(_tempRoot, "journal_index.json");
            const string corruptIndex = "{ this is not valid json";
            File.WriteAllText(indexPath, corruptIndex);

            string candidateImagePath = CreateOwnedImage(repo, "candidate.jpg");
            var record = JournalRecord.CreateNew("text", "candidate.jpg", 10, 10);

            LogAssert.Expect(
                LogType.Error,
                new Regex("JournalRepository\\.TryLoad \\[Index\\]"));

            bool saved = repo.TryAppend(record, candidateImagePath, out string error);

            Assert.IsFalse(saved);
            StringAssert.Contains("Could not load existing journal data", error);
            Assert.AreEqual(corruptIndex, File.ReadAllText(indexPath));
            Assert.IsFalse(
                File.Exists(candidateImagePath),
                "The rejected append's image should be cleaned up.");
        }

        private static string CreateOwnedImage(JournalRepository repo, string fileName)
        {
            string path = Path.Combine(repo.ImagesDirectory, fileName);
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            return path;
        }
    }
}
