using System.Collections.Generic;
using Discord;
using SevenThree.Constants;
using SevenThree.Models;

namespace SevenThree.Modules.Study
{
    /// <summary>
    /// Shared embed and button builders for study flashcard and retry modes.
    /// Used by both StudySlashCommands (initial render) and StudyButtonHandler (navigation updates).
    /// </summary>
    public static class StudyEmbedBuilder
    {
        public static EmbedBuilder BuildFlashcardEmbed(StudySession session)
        {
            var question = session.Questions[session.CurrentIndex];
            var embed = new EmbedBuilder();

            embed.Title = $"Flashcard Review ({session.CurrentIndex + 1}/{session.Questions.Count})";
            embed.WithColor(StudyConstants.COLOR_STUDY);

            embed.AddField($"Question [{question.QuestionSection}]",
                question.QuestionText ?? "Question not available");

            if (!string.IsNullOrEmpty(question.SubelementName))
            {
                embed.AddField("Topic", $"**{question.SubelementName}**: {question.SubelementDesc ?? ""}");
            }

            if (session.ShowingAnswer)
            {
                embed.AddField("✅ Correct Answer", question.CorrectAnswer ?? "Not available");

                if (question.TimesMissed > 1)
                {
                    embed.WithFooter($"You've missed this question {question.TimesMissed} times");
                }
            }
            else
            {
                embed.WithFooter("Click 'Show Answer' to reveal the correct answer");
            }

            embed.ThumbnailUrl = QuizConstants.BOT_THUMBNAIL_URL;

            return embed;
        }

        public static MessageComponent BuildFlashcardButtons(StudySession session)
        {
            var builder = new ComponentBuilder();
            var hasPrev = session.CurrentIndex > 0;
            var hasNext = session.CurrentIndex < session.Questions.Count - 1;

            if (!session.ShowingAnswer)
            {
                builder.WithButton(
                    "Show Answer",
                    $"{StudyConstants.BUTTON_PREFIX}:show:{session.SessionId}:{session.CurrentIndex}",
                    ButtonStyle.Success,
                    row: 0);
            }
            else
            {
                builder.WithButton(
                    "Hide Answer",
                    $"{StudyConstants.BUTTON_PREFIX}:hide:{session.SessionId}:{session.CurrentIndex}",
                    ButtonStyle.Secondary,
                    row: 0);
            }

            builder.WithButton(
                "◀ Previous",
                $"{StudyConstants.BUTTON_PREFIX}:prev:{session.SessionId}:{session.CurrentIndex}",
                ButtonStyle.Primary,
                disabled: !hasPrev,
                row: 1);

            builder.WithButton(
                "Next ▶",
                $"{StudyConstants.BUTTON_PREFIX}:next:{session.SessionId}:{session.CurrentIndex}",
                ButtonStyle.Primary,
                disabled: !hasNext,
                row: 1);

            builder.WithButton(
                "Done",
                $"{StudyConstants.BUTTON_PREFIX}:done:{session.SessionId}:{session.CurrentIndex}",
                ButtonStyle.Danger,
                row: 1);

            return builder.Build();
        }

        public static EmbedBuilder BuildRetryQuestionEmbed(StudySession session, List<AnswerOption> shuffledAnswers)
        {
            var question = session.Questions[session.CurrentIndex];
            var embed = new EmbedBuilder();
            var letters = new[] { 'A', 'B', 'C', 'D' };

            embed.Title = $"Retry Mode ({session.CurrentIndex + 1}/{session.Questions.Count})";
            embed.WithColor(StudyConstants.COLOR_STUDY);

            embed.AddField($"Question [{question.QuestionSection}]",
                question.QuestionText ?? "Question not available");

            if (!string.IsNullOrEmpty(question.SubelementName))
            {
                embed.AddField("Topic", $"**{question.SubelementName}**: {question.SubelementDesc ?? ""}");
            }

            for (int i = 0; i < shuffledAnswers.Count && i < letters.Length; i++)
            {
                embed.AddField($"{letters[i]}.", shuffledAnswers[i].AnswerText ?? "No answer text", inline: true);
            }

            if (question.TimesMissed > 1)
            {
                embed.WithFooter($"You've missed this question {question.TimesMissed} times before");
            }

            embed.ThumbnailUrl = QuizConstants.BOT_THUMBNAIL_URL;

            return embed;
        }

        public static MessageComponent BuildRetryAnswerButtons(StudySession session, List<AnswerOption> shuffledAnswers)
        {
            var builder = new ComponentBuilder();
            var letters = new[] { 'A', 'B', 'C', 'D' };

            for (int i = 0; i < shuffledAnswers.Count && i < letters.Length; i++)
            {
                builder.WithButton(
                    letters[i].ToString(),
                    $"{StudyConstants.RETRY_BUTTON_PREFIX}:{session.SessionId}:{letters[i]}:{shuffledAnswers[i].AnswerId}",
                    ButtonStyle.Primary,
                    row: 0);
            }

            builder.WithButton(
                "Skip",
                $"{StudyConstants.RETRY_BUTTON_PREFIX}:{session.SessionId}:skip:0",
                ButtonStyle.Secondary,
                row: 1);

            builder.WithButton(
                "Stop",
                $"{StudyConstants.RETRY_BUTTON_PREFIX}:{session.SessionId}:stop:0",
                ButtonStyle.Danger,
                row: 1);

            return builder.Build();
        }
    }
}
