namespace SevenThree.Constants
{
    public static class QuizConstants
    {
        /// <summary>
        /// Prefix for quiz answer button custom IDs.
        /// Format: {BUTTON_PREFIX}:{sessionId}:{answer}
        /// Example: quiz:123456789:A
        /// </summary>
        public const string BUTTON_PREFIX = "quiz";

        /// <summary>
        /// Prefix for quiz stop button custom IDs.
        /// Format: {STOP_BUTTON_PREFIX}:{sessionId}
        /// Example: quizstop:123456789
        /// </summary>
        public const string STOP_BUTTON_PREFIX = "quizstop";
    }
}
