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
        public string QuestionSection { get; set; }
        public string FccPart { get; set; }
        public string Subelement { get; set; }
        public string SubelementName { get; set; }
        public string SubelementDesc { get; set; }
    }
}