using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenThree.Database
{
    public class Quiz
    {
        [Key] 
        public int QuizId { get; set; }     
        public ulong ServerId { get; set; }        
        public string ServerName { get; set; }
        public bool IsActive { get; set; }
        public DateTime TimeStarted { get; set; }
        public DateTime TimeEnded { get; set; }

    }
}