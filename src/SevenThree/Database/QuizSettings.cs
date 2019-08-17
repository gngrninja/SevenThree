using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenThree.Database
{
    public class QuizSettings
    {
        [Key] 
        public int QuizSettingsId { get; set; }     

        public ulong? TechChannelId { get; set; }
        public ulong? ExtraChannelId { get; set; }
        public ulong? GeneralChannelId { get; set; }
        public ulong? DiscordGuildId { get; set; }
        public bool ClearAfterTaken { get; set; }
    }
}
