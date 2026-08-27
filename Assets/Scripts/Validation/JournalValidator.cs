namespace TraceJournal.Validation
{
    public static class JournalValidator
    {
        /// hasOwnedImage means: an image has been copied/normalized into app
        /// ownership already (see ImageProcessor) — not just "a picker path exists".
        public static bool Validate(string text, bool hasOwnedImage, out string error)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Please write something before saving.";
                return false;
            }

            if (!hasOwnedImage)
            {
                error = "Please choose an image before saving.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
