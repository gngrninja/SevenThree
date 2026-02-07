using Xunit;
using SevenThree.Modules;

namespace SevenThree.Tests
{
    public class QrzApiTests
    {
        [Fact]
        public void ConvertResultToXml_NullInput_ReturnsErrorMessage()
        {
            var result = QrzApi.ConvertResultToXml(null);

            Assert.NotNull(result);
            Assert.NotNull(result.Session);
            Assert.Equal("Empty response from QRZ API", result.Session.Error);
        }

        [Fact]
        public void ConvertResultToXml_EmptyString_ReturnsErrorMessage()
        {
            var result = QrzApi.ConvertResultToXml(string.Empty);

            Assert.NotNull(result);
            Assert.NotNull(result.Session);
            Assert.Equal("Empty response from QRZ API", result.Session.Error);
        }

        [Fact]
        public void ConvertResultToXml_ValidXml_ParsesCallsignAndSession()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<QRZDatabase version=""1.34"">
  <Callsign>
    <call>W1AW</call>
    <fname>Hiram</fname>
    <name>Maxim</name>
  </Callsign>
  <Session>
    <Key>test-key-123</Key>
    <Count>5</Count>
  </Session>
</QRZDatabase>";

            var result = QrzApi.ConvertResultToXml(xml);

            Assert.NotNull(result);
            Assert.NotNull(result.Callsign);
            Assert.Equal("W1AW", result.Callsign.Call);
            Assert.Equal("Hiram", result.Callsign.Fname);
            Assert.Equal("Maxim", result.Callsign.Name);
            Assert.NotNull(result.Session);
            Assert.Equal("test-key-123", result.Session.Key);
            Assert.Equal("5", result.Session.Count);
        }

        [Fact]
        public void ConvertResultToXml_SessionErrorXml_PreservesError()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<QRZDatabase version=""1.34"">
  <Session>
    <Error>Not found: INVALID</Error>
  </Session>
</QRZDatabase>";

            var result = QrzApi.ConvertResultToXml(xml);

            Assert.NotNull(result);
            Assert.NotNull(result.Session);
            Assert.Equal("Not found: INVALID", result.Session.Error);
        }
    }
}
