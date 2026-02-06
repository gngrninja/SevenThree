using System;
using System.Collections.Generic;

namespace SevenThree.Models
{
    public enum StudyScope
    {
        Last,
        All
    }

    public class MissedQuestion
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string QuestionSection { get; set; }
        public string SubelementName { get; set; }
        public string SubelementDesc { get; set; }
        public string FccPart { get; set; }
        public string FigureName { get; set; }
        public string TestName { get; set; }
        public string CorrectAnswer { get; set; }
        public string UserAnswer { get; set; }
        public int TimesAsked { get; set; }
        public int TimesMissed { get; set; }
    }

    public class AnswerOption
    {
        public int AnswerId { get; set; }
        public string AnswerText { get; set; }
        public bool IsCorrect { get; set; }
    }

    public class StudySession
    {
        public string SessionId { get; set; }
        public ulong UserId { get; set; }
        public List<MissedQuestion> Questions { get; set; }
        public int CurrentIndex { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool ShowingAnswer { get; set; }
    }

    public class UserStudyStats
    {
        public int TotalAnswered { get; set; }
        public int TotalCorrect { get; set; }
        public double OverallPercent { get; set; }
        public List<SubelementStat> SubelementStats { get; set; } = new();
    }

    public class SubelementStat
    {
        public string TestName { get; set; }
        public string SubelementName { get; set; }
        public string SubelementDesc { get; set; }
        public int TotalAnswered { get; set; }
        public int TotalCorrect { get; set; }
        public double PercentCorrect { get; set; }
    }
}
