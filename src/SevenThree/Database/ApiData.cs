using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenThree.Database
{
    public class ApiData
    {
        [Key] 
        public int ApiDataId { get; set; }  

        public string AppName { get; set; }
        public string ApiKey { get; set; } 
        public string ApiBaseUrl { get; set; }       
    }
}