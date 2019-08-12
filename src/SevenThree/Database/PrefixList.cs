using System.ComponentModel.DataAnnotations;

namespace SevenThree.Database
{
    public partial class PrefixList
    {
        [Key]
        public long ServerId { get; set; }
        public string ServerName { get; set; }
        public char Prefix { get; set; }
        public long SetById { get; set; }
    }    
}