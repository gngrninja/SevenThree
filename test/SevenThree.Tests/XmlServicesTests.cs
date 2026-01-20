using System;
using System.IO;
using System.Text;
using Xunit;
using SevenThree.Services;
using SevenThree.Models;

namespace SevenThree.Tests
{
    public class XmlServicesTests
    {
        private readonly XmlServices _sut;

        public XmlServicesTests()
        {
            _sut = new XmlServices();
        }

        #region GetQrzResultFromString Tests

        [Fact]
        public void GetQrzResultFromString_ValidCallsignXml_ParsesCorrectly()
        {
            // arrange
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<QRZDatabase version=""1.34"">
  <Callsign>
    <call>W1AW</call>
    <fname>Hiram</fname>
    <name>Maxim</name>
    <addr1>225 Main St</addr1>
    <addr2>Newington</addr2>
    <state>CT</state>
    <zip>06111</zip>
    <country>United States</country>
    <lat>41.714775</lat>
    <lon>-72.727260</lon>
    <grid>FN31pr</grid>
    <class>C</class>
  </Callsign>
  <Session>
    <Key>abc123</Key>
    <Count>42</Count>
  </Session>
</QRZDatabase>";
            using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

            // act
            var result = _sut.GetQrzResultFromString(reader);

            // assert
            Assert.NotNull(result);
            Assert.NotNull(result.Callsign);
            Assert.Equal("W1AW", result.Callsign.Call);
            Assert.Equal("Hiram", result.Callsign.Fname);
            Assert.Equal("Maxim", result.Callsign.Name);
            Assert.Equal("CT", result.Callsign.State);
            Assert.Equal("06111", result.Callsign.Zip);
            Assert.Equal("United States", result.Callsign.Country);
            Assert.Equal("FN31pr", result.Callsign.Grid);
            Assert.Equal("C", result.Callsign.Class);
        }

        [Fact]
        public void GetQrzResultFromString_ValidSessionXml_ParsesSessionData()
        {
            // arrange
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<QRZDatabase version=""1.34"">
  <Session>
    <Key>session-key-12345</Key>
    <Count>100</Count>
    <SubExp>2025-12-31</SubExp>
    <GMTime>2024-01-15 12:30:00</GMTime>
  </Session>
</QRZDatabase>";
            using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

            // act
            var result = _sut.GetQrzResultFromString(reader);

            // assert
            Assert.NotNull(result);
            Assert.NotNull(result.Session);
            Assert.Equal("session-key-12345", result.Session.Key);
            Assert.Equal("100", result.Session.Count);
            Assert.Equal("2025-12-31", result.Session.SubExp);
        }

        [Fact]
        public void GetQrzResultFromString_ErrorResponse_ParsesErrorMessage()
        {
            // arrange
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<QRZDatabase version=""1.34"">
  <Session>
    <Error>Not found: INVALID</Error>
  </Session>
</QRZDatabase>";
            using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

            // act
            var result = _sut.GetQrzResultFromString(reader);

            // assert
            Assert.NotNull(result);
            Assert.NotNull(result.Session);
            Assert.Equal("Not found: INVALID", result.Session.Error);
        }

        [Fact]
        public void GetQrzResultFromString_DxccInfo_ParsesCorrectly()
        {
            // arrange
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<QRZDatabase version=""1.34"">
  <DXCC>
    <dxcc>291</dxcc>
    <cc>US</cc>
    <ccc>USA</ccc>
    <name>United States</name>
    <continent>NA</continent>
    <ituzone>8</ituzone>
    <cqzone>5</cqzone>
    <timezone>-5</timezone>
    <lat>37.701207</lat>
    <lon>-97.316895</lon>
  </DXCC>
  <Session>
    <Key>test</Key>
  </Session>
</QRZDatabase>";
            using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

            // act
            var result = _sut.GetQrzResultFromString(reader);

            // assert
            Assert.NotNull(result);
            Assert.NotNull(result.DXCC);
            Assert.Equal("291", result.DXCC.Dxcc);
            Assert.Equal("US", result.DXCC.Cc);
            Assert.Equal("USA", result.DXCC.Ccc);
            Assert.Equal("United States", result.DXCC.Name);
            Assert.Equal("NA", result.DXCC.Continent);
        }

        [Fact]
        public void GetQrzResultFromString_VersionAttribute_ParsesCorrectly()
        {
            // arrange
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<QRZDatabase version=""1.34"">
  <Session>
    <Key>test</Key>
  </Session>
</QRZDatabase>";
            using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

            // act
            var result = _sut.GetQrzResultFromString(reader);

            // assert
            Assert.NotNull(result);
            Assert.Equal("1.34", result.Version);
        }

        [Fact]
        public void GetQrzResultFromString_MinimalXml_ParsesWithoutError()
        {
            // arrange
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<QRZDatabase version=""1.0"">
  <Session></Session>
</QRZDatabase>";
            using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

            // act
            var result = _sut.GetQrzResultFromString(reader);

            // assert
            Assert.NotNull(result);
        }

        [Fact]
        public void GetQrzResultFromString_CompleteCallsign_AllFieldsPopulated()
        {
            // arrange
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<QRZDatabase version=""1.34"">
  <Callsign>
    <call>N0CALL</call>
    <aliases>KA0ABC,WB0XYZ</aliases>
    <dxcc>291</dxcc>
    <fname>John</fname>
    <name>Doe</name>
    <addr1>123 Main St</addr1>
    <addr2>Anytown</addr2>
    <state>KS</state>
    <zip>66101</zip>
    <country>United States</country>
    <ccode>291</ccode>
    <lat>39.114053</lat>
    <lon>-94.627464</lon>
    <grid>EM29</grid>
    <county>Wyandotte</county>
    <fips>20209</fips>
    <efdate>2020-01-01</efdate>
    <expdate>2030-01-01</expdate>
    <class>E</class>
    <email>test@example.com</email>
    <url>http://example.com</url>
    <cqzone>4</cqzone>
    <ituzone>7</ituzone>
  </Callsign>
  <Session>
    <Key>test</Key>
  </Session>
</QRZDatabase>";
            using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

            // act
            var result = _sut.GetQrzResultFromString(reader);

            // assert
            Assert.NotNull(result.Callsign);
            Assert.Equal("N0CALL", result.Callsign.Call);
            Assert.Equal("KA0ABC,WB0XYZ", result.Callsign.Aliases);
            Assert.Equal("291", result.Callsign.Dxcc);
            Assert.Equal("John", result.Callsign.Fname);
            Assert.Equal("Doe", result.Callsign.Name);
            Assert.Equal("E", result.Callsign.Class);
            Assert.Equal("test@example.com", result.Callsign.Email);
            Assert.Equal("EM29", result.Callsign.Grid);
            Assert.Equal("Wyandotte", result.Callsign.County);
        }

        #endregion
    }
}
