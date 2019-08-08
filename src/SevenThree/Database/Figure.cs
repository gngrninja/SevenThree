using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenThree.Database
{
    public class Figure
    {
        [Key] 
        public int FigureId { get; set; }     

        [ForeignKey("TestId")]
        public HamTest Test { get; set;}

        public string FigureName { get; set; }
        public byte[] FigureImage { get; set; }
    }
}