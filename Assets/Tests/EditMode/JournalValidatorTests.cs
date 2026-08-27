using NUnit.Framework;
using TraceJournal.Validation;

namespace TraceJournal.Tests.EditMode
{
    public class JournalValidatorTests
    {
        [Test]
        public void Validate_FailsOnBlankText()
        {
            bool ok = JournalValidator.Validate("   ", hasOwnedImage: true, out string error);
            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void Validate_FailsWithoutOwnedImage()
        {
            bool ok = JournalValidator.Validate("hello", hasOwnedImage: false, out string error);
            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void Validate_PassesWithTextAndImage()
        {
            bool ok = JournalValidator.Validate("hello", hasOwnedImage: true, out string error);
            Assert.IsTrue(ok);
            Assert.IsNull(error);
        }
    }
}
