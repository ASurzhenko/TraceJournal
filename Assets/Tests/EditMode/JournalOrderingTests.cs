using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TraceJournal.Data;
using TraceJournal.Models;

namespace TraceJournal.Tests.EditMode
{
    public class JournalOrderingTests
    {
        private string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "TraceJournalTests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
        }

        [Test]
        public void Records_SortNewestFirst_ByCreatedUtc()
        {
            var repo = new JournalRepository(_tempRoot);

            var older = JournalRecord.CreateNew("older", "a.jpg", 1, 1);
            older.createdUtc = DateTime.UtcNow.AddMinutes(-10).ToString("o");

            var newer = JournalRecord.CreateNew("newer", "b.jpg", 1, 1);
            newer.createdUtc = DateTime.UtcNow.ToString("o");

            string olderImagePath = CreateOwnedImage(repo, older.imageRelativePath);
            string newerImagePath = CreateOwnedImage(repo, newer.imageRelativePath);

            Assert.IsTrue(repo.TryAppend(older, olderImagePath, out _));
            Assert.IsTrue(repo.TryAppend(newer, newerImagePath, out _));

            var ordered = repo.LoadAll()
                .OrderByDescending(r => r.GetCreatedUtcDateTime())
                .ToList();

            Assert.AreEqual("newer", ordered[0].text);
            Assert.AreEqual("older", ordered[1].text);
        }

        [Test]
        public void Records_HaveStableUniqueIds()
        {
            var a = JournalRecord.CreateNew("a", "a.jpg", 1, 1);
            var b = JournalRecord.CreateNew("b", "b.jpg", 1, 1);

            Assert.IsFalse(string.IsNullOrEmpty(a.id));
            Assert.AreNotEqual(a.id, b.id);

            var repo = new JournalRepository(_tempRoot);
            string imagePath = CreateOwnedImage(repo, a.imageRelativePath);
            Assert.IsTrue(repo.TryAppend(a, imagePath, out _));

            var reloaded = repo.LoadAll().First();
            Assert.AreEqual(a.id, reloaded.id, "ID must survive a save/load round trip unchanged.");
        }

        private static string CreateOwnedImage(JournalRepository repo, string fileName)
        {
            string path = Path.Combine(repo.ImagesDirectory, fileName);
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            return path;
        }
    }
}
