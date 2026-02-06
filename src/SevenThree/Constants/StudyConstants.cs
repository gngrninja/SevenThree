using Discord;

namespace SevenThree.Constants
{
    public static class StudyConstants
    {
        /// <summary>
        /// Prefix for study flashcard button custom IDs.
        /// Format: {BUTTON_PREFIX}:{action}:{sessionId}:{index}
        /// Example: study:next:abc123:0
        /// </summary>
        public const string BUTTON_PREFIX = "study";

        /// <summary>
        /// Prefix for study retry (re-quiz) button custom IDs.
        /// Format: {RETRY_BUTTON_PREFIX}:{sessionId}:{answer}
        /// Example: studyretry:abc123:A
        /// </summary>
        public const string RETRY_BUTTON_PREFIX = "studyretry";

        /// <summary>
        /// Color for study/review embeds
        /// </summary>
        public static readonly Color COLOR_STUDY = new(255, 165, 0); // Orange

        /// <summary>
        /// Color for stats embeds
        /// </summary>
        public static readonly Color COLOR_STATS = new(100, 149, 237); // Cornflower blue

        /// <summary>
        /// Cache duration for study sessions in minutes
        /// </summary>
        public const int SESSION_CACHE_MINUTES = 30;

        /// <summary>
        /// Minimum times a question must be missed to appear in "weak" review
        /// </summary>
        public const int WEAK_THRESHOLD = 2;
    }
}
