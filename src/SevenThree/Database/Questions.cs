using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenThree.Database
{
    public class Questions
    {
        [Key]
        public int QuestionId { get; set; }

        [ForeignKey("TestId")]
        public HamTest Test { get; set;}

        public string QuestionText { get; set; }

        /// <summary>
        /// FCC question ID (e.g., T1A01, G3B05). Stable across pool updates.
        /// </summary>
        public string QuestionSection { get; set; }

        public string FccPart { get; set; }
        public string Subelement { get; set; }
        public string SubelementName { get; set; }
        public string SubelementDesc { get; set; }
        public string FigureName { get; set; }

        /// <summary>
        /// True if this question was removed from the pool during a re-import.
        /// Archived questions are excluded from quizzes but UserAnswers are preserved.
        /// </summary>
        public bool IsArchived { get; set; } = false;

        /// <summary>
        /// Timestamp of the last import that included this question.
        /// </summary>
        public DateTime? LastImportedAt { get; set; }
    }
}