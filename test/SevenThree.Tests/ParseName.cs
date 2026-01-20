using System;
using System.IO;
using Xunit;

namespace SevenThree.Tests
{
    public class ParseNameTests
    {
        [Theory]
        [InlineData("tech_07-01-2022_06-30-2026", "07-01-2022", "06-30-2026")]
        [InlineData("general_07-01-2023_06-30-2027", "07-01-2023", "06-30-2027")]
        [InlineData("extra_07-01-2024_06-30-2028", "07-01-2024", "06-30-2028")]
        public void ParseDatesFromFileName_ValidFormat_ReturnsCorrectDates(
            string fileName, string expectedStart, string expectedEnd)
        {
            // arrange
            var parts = fileName.Split('_');

            // act
            var startDate = DateTime.Parse(parts[1]);
            var endDate = DateTime.Parse(parts[2]);

            // assert
            Assert.Equal(DateTime.Parse(expectedStart), startDate);
            Assert.Equal(DateTime.Parse(expectedEnd), endDate);
        }

        [Theory]
        [InlineData("tech_07-01-2022_06-30-2026.json", "tech")]
        [InlineData("general_07-01-2023_06-30-2027.json", "general")]
        [InlineData("extra_07-01-2024_06-30-2028.json", "extra")]
        public void ParseTestTypeFromFileName_ValidFormat_ReturnsCorrectType(
            string fileName, string expectedType)
        {
            // arrange
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            // act
            var testType = fileNameWithoutExt.Split('_')[0];

            // assert
            Assert.Equal(expectedType, testType);
        }

        [Fact]
        public void ParseFileName_InvalidFormat_ThrowsException()
        {
            // arrange
            var invalidFileName = "invalid";
            var parts = invalidFileName.Split('_');

            // act & assert
            Assert.Single(parts);
            Assert.Throws<IndexOutOfRangeException>(() => parts[1]);
        }
    }
}
