using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenThree.Database
{
    public class Credentials
    {
        [Key] 
        public int Id { get; set; }                
        public string CredName { get; set; }
        public string UserName { get; set;}
        public string CallSign { get; set; }
        public string Password { get; set;}
    }
}