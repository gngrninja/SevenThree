using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenThree.Database
{
    public class Cred
    {
        [Key] 
        public int Id { get; set; }                        
        public string User { get; set;}        
        public string Pass { get; set;}
    }
}