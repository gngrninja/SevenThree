using System;
using Xunit;
using SevenThree.Modules;
using System.IO;
using System.Linq;

namespace SevenThree.Tests
{
    public class ParseName
    {
        [Fact]
        public void GetDatesFromFileName()
        {
            //arrange
            string[] tests = new string[] 
            {
                "tech",
                "general",
                "extra"
            };
            var files = Directory.GetFiles($"{Environment.CurrentDirectory}/import");
            DateTime techStartDate = DateTime.MinValue;
            DateTime techEndDate = DateTime.MinValue;
            DateTime generalStartDate = DateTime.MinValue;
            DateTime generalEndDate = DateTime.MinValue;
            DateTime extraStartDate = DateTime.MinValue;
            DateTime extraEndDate = DateTime.MinValue;

            //act
            foreach (var test in tests)
            {
                var curFile = files.Where(f => f.Contains(test)).FirstOrDefault();
                if (curFile != null)
                {
                    var fileName = Path.GetFileNameWithoutExtension(curFile);
                    switch (test)
                    {
                        case "tech":
                        {
                            techStartDate = DateTime.Parse(fileName.Split('_')[1]);
                            techEndDate  = DateTime.Parse(fileName.Split('_')[2]);
                            break;
                        }
                        case "general":
                        {
                            generalStartDate = DateTime.Parse(fileName.Split('_')[1]);
                            generalEndDate = DateTime.Parse(fileName.Split('_')[2]);
                            break;
                        }
                        case "extra":
                        {
                            extraStartDate = DateTime.Parse(fileName.Split('_')[1]);
                            extraEndDate = DateTime.Parse(fileName.Split('_')[2]);
                            break;
                        }
                    }
                }           
            }

            //assert            
            Assert.Equal(techStartDate, DateTime.Parse("07-01-2018"));
            Assert.Equal(techEndDate, DateTime.Parse("06-30-2022"));

            Assert.Equal(generalStartDate, DateTime.Parse("7-1-2019"));
            Assert.Equal(generalEndDate, DateTime.Parse("6-30-2023"));

            Assert.Equal(extraStartDate, DateTime.Parse("7-1-2016"));
            Assert.Equal(extraEndDate, DateTime.Parse("6-30-2020"));
        }
    }
}
