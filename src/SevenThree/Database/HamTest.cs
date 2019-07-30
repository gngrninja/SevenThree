using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenThree.Database
{
    public class HamTest
    {
        [Key] 
        public int TestId { get; set; }                
        public string TestName { get; set; }
        public string TestDescription { get; set; }
    }
}