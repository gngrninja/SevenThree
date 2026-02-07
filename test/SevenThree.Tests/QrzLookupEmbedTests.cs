using System.Linq;
using Discord;
using Moq;
using Xunit;
using SevenThree.Constants;
using SevenThree.Models;
using SevenThree.Modules;

namespace SevenThree.Tests
{
    public class QrzLookupEmbedTests
    {
        private readonly Mock<IUser> _mockUser;

        public QrzLookupEmbedTests()
        {
            _mockUser = new Mock<IUser>();
            _mockUser.Setup(u => u.Username).Returns("TestUser");
            _mockUser.Setup(u => u.GetAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>())).Returns("https://example.com/avatar.png");
        }

        [Fact]
        public void BuildCallEmbed_SuccessfulLookup_HasGreenColorAndAllFields()
        {
            var result = new QrzApiXml.QRZDatabase
            {
                Callsign = new QrzApiXml.Callsign
                {
                    Call = "W1AW",
                    Fname = "Hiram",
                    Name = "Maxim",
                    Class = "E",
                    U_views = "1000",
                    Lat = "41.714",
                    Lon = "-72.727",
                    Land = "United States",
                    TimeZone = "EST",
                    Efdate = "2020-01-01",
                    Expdate = "2030-01-01"
                },
                Session = new QrzApiXml.Session { Key = "test" }
            };

            var embed = QrzLookupSlashCommands.BuildCallEmbed("W1AW", result, _mockUser.Object);

            Assert.Equal(new Color(0, 255, 50), embed.Color);
            Assert.Contains("W1AW", embed.Title);
            Assert.True(embed.Fields.Length >= 7); // Name, Class, Views, Lat/Long, Country, Timezone, License dates, Profile link

            var fieldNames = embed.Fields.Select(f => f.Name).ToList();
            Assert.Contains("Name", fieldNames);
            Assert.Contains("Class", fieldNames);
            Assert.Contains("Lat/Long", fieldNames);
            Assert.Contains("QRZ Profile Link", fieldNames);
        }

        [Fact]
        public void BuildCallEmbed_ErrorResult_HasRedColorAndErrorField()
        {
            var result = new QrzApiXml.QRZDatabase
            {
                Session = new QrzApiXml.Session { Error = "Not found: INVALID" }
            };

            var embed = QrzLookupSlashCommands.BuildCallEmbed("INVALID", result, _mockUser.Object);

            Assert.Equal(new Color(255, 0, 0), embed.Color);
            Assert.Contains("Error", embed.Title);
            Assert.Contains("INVALID", embed.Title);

            var errorField = embed.Fields.First(f => f.Name == "Error Details");
            Assert.Equal("Not found: INVALID", errorField.Value);
        }

        [Fact]
        public void BuildCallEmbed_PartialFields_OnlyPresentFieldsAdded()
        {
            var result = new QrzApiXml.QRZDatabase
            {
                Callsign = new QrzApiXml.Callsign
                {
                    Call = "N0CALL",
                    Fname = "John",
                    Name = "Doe"
                    // No Lat, Lon, Class, etc.
                },
                Session = new QrzApiXml.Session { Key = "test" }
            };

            var embed = QrzLookupSlashCommands.BuildCallEmbed("N0CALL", result, _mockUser.Object);

            var fieldNames = embed.Fields.Select(f => f.Name).ToList();
            Assert.Contains("Name", fieldNames);
            Assert.Contains("QRZ Profile Link", fieldNames);
            Assert.DoesNotContain("Lat/Long", fieldNames);
            Assert.DoesNotContain("Class", fieldNames);
            Assert.DoesNotContain("Timezone", fieldNames);
        }

        [Fact]
        public void BuildCallEmbed_WithImage_SetsImageAndThumbnail()
        {
            var result = new QrzApiXml.QRZDatabase
            {
                Callsign = new QrzApiXml.Callsign
                {
                    Call = "W1AW",
                    Image = "https://example.com/photo.jpg"
                },
                Session = new QrzApiXml.Session { Key = "test" }
            };

            var embed = QrzLookupSlashCommands.BuildCallEmbed("W1AW", result, _mockUser.Object);

            Assert.Equal("https://example.com/photo.jpg", embed.Image.Value.Url);
            Assert.Equal("https://example.com/photo.jpg", embed.Thumbnail.Value.Url);
        }

        [Fact]
        public void BuildCallEmbed_WithoutImage_FallsBackToBotThumbnail()
        {
            var result = new QrzApiXml.QRZDatabase
            {
                Callsign = new QrzApiXml.Callsign
                {
                    Call = "W1AW"
                },
                Session = new QrzApiXml.Session { Key = "test" }
            };

            var embed = QrzLookupSlashCommands.BuildCallEmbed("W1AW", result, _mockUser.Object);

            Assert.Equal(QuizConstants.BOT_THUMBNAIL_URL, embed.Thumbnail.Value.Url);
            Assert.Null(embed.Image);
        }

        [Fact]
        public void BuildCallEmbed_AuthorAndFooter_AlwaysPresent()
        {
            var result = new QrzApiXml.QRZDatabase
            {
                Callsign = new QrzApiXml.Callsign { Call = "W1AW" },
                Session = new QrzApiXml.Session { Key = "test" }
            };

            var embed = QrzLookupSlashCommands.BuildCallEmbed("W1AW", result, _mockUser.Object);

            Assert.NotNull(embed.Author);
            Assert.Contains("TestUser", embed.Author.Value.Name);
            Assert.NotNull(embed.Footer);
            Assert.Contains("SevenThree", embed.Footer.Value.Text);
        }
    }
}
