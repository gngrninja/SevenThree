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

        /// <summary>
        /// Default delay between questions in milliseconds
        /// </summary>
        public const int DEFAULT_QUESTION_DELAY_MS = 60000;

        /// <summary>
        /// Delay after answer before showing next question in milliseconds
        /// </summary>
        public const int POST_ANSWER_DELAY_MS = 3000;

        /// <summary>
        /// Maximum number of questions allowed per quiz
        /// </summary>
        public const int MAX_QUESTIONS = 100;

        /// <summary>
        /// Minimum delay between questions in seconds
        /// </summary>
        public const int MIN_DELAY_SECONDS = 15;

        /// <summary>
        /// Maximum delay between questions in seconds
        /// </summary>
        public const int MAX_DELAY_SECONDS = 120;

        /// <summary>
        /// Bot thumbnail URL
        /// </summary>
        public const string BOT_THUMBNAIL_URL = "https://github.com/gngrninja/SevenThree/raw/master/media/73.png?raw=true";
    }
}
