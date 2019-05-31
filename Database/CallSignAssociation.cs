using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenThree.Database
{
    public class CallSignAssociation
    {
        [Key] 
        public int Id { get; set; }                
        public string DiscordUserName { get; set; }
        public long DiscordUserId { get; set;}
        public string CallSign { get; set; }
    }
}