namespace SevenThree.Models
{
    /// <summary>
    /// Defines the quiz interaction mode
    /// </summary>
    public enum QuizMode
    {
        /// <summary>
        /// Single-user ephemeral quiz (default) - only the user can see questions and answers
        /// </summary>
        Private,

        /// <summary>
        /// Multi-user public quiz - visible in channel with leaderboards
        /// </summary>
        Public
    }
}
