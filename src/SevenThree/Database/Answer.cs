using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenThree.Database
{
    public class Answer
    {
        [Key] 
        public int AnswerId { get; set; }     

        [ForeignKey("QuestionId")]
        public Questions Question { get; set;}
        
        public string AnswerText { get; set; }
        public bool IsAnswer { get; set; }
    }
}