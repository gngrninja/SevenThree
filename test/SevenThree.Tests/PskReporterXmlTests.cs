using System.IO;
using System.Xml.Serialization;
using Xunit;
using SevenThree.Models;

namespace SevenThree.Tests
{
    /// <summary>
    /// Tests XML deserialization of PSKReporter API responses.
    /// PSKReporter only returns XML, so correct parsing is critical.
    /// </summary>
    public class PskReporterXmlTests
    {
        private static PskReporterXml.ReceptionReports Deserialize(string xml)
        {
            var serializer = new XmlSerializer(typeof(PskReporterXml.ReceptionReports));
            using var reader = new StringReader(xml);
            return (PskReporterXml.ReceptionReports)serializer.Deserialize(reader);
        }

        #region Deserialization Tests

        [Fact]
        public void Deserialize_ValidResponse_ParsesReports()
        {
            var xml = @"<?xml version='1.0' encoding='UTF-8'?>
<receptionReports currentSeconds='1700000000'>
  <lastSequenceNumber value='12345' />
  <maxFlowStartSeconds value='1700000000' />
  <receptionReport
    receiverCallsign='K1ABC'
    receiverLocator='FN42'
    senderCallsign='W1AW'
    senderLocator='FN31'
    frequency='14074000'
    flowStartSeconds='1700000000'
    mode='FT8'
    sNR='-10'
    receiverDXCC='United States'
    receiverDXCCCode='291' />
</receptionReports>";

            var result = Deserialize(xml);

            Assert.Single(result.ReceptionReportList);
            var report = result.ReceptionReportList[0];
            Assert.Equal("K1ABC", report.ReceiverCallsign);
            Assert.Equal("W1AW", report.SenderCallsign);
            Assert.Equal(14074000, report.Frequency);
            Assert.Equal("FT8", report.Mode);
            Assert.Equal(-10, report.SNR);
            Assert.Equal("United States", report.ReceiverDXCC);
        }

        [Fact]
        public void Deserialize_MultipleReports_ParsesAll()
        {
            var xml = @"<?xml version='1.0' encoding='UTF-8'?>
<receptionReports currentSeconds='1700000000'>
  <receptionReport receiverCallsign='K1ABC' senderCallsign='W1AW' frequency='14074000' flowStartSeconds='1700000000' mode='FT8' />
  <receptionReport receiverCallsign='DL1ABC' senderCallsign='W1AW' frequency='7074000' flowStartSeconds='1700000050' mode='FT8' />
  <receptionReport receiverCallsign='JA1ABC' senderCallsign='W1AW' frequency='21074000' flowStartSeconds='1700000100' mode='FT8' />
</receptionReports>";

            var result = Deserialize(xml);
            Assert.Equal(3, result.ReceptionReportList.Count);
        }

        [Fact]
        public void Deserialize_EmptyResponse_ParsesWithoutError()
        {
            var xml = @"<?xml version='1.0' encoding='UTF-8'?>
<receptionReports currentSeconds='1700000000'>
  <lastSequenceNumber value='0' />
</receptionReports>";

            var result = Deserialize(xml);
            Assert.Empty(result.ReceptionReportList);
        }

        [Fact]
        public void Deserialize_WithActiveReceivers_ParsesReceivers()
        {
            var xml = @"<?xml version='1.0' encoding='UTF-8'?>
<receptionReports currentSeconds='1700000000'>
  <activeReceiver callsign='K1ABC' locator='FN42' frequency='14074000' mode='FT8' DXCC='United States' />
  <activeReceiver callsign='DL1ABC' locator='JN58' frequency='7074000' mode='CW' DXCC='Germany' />
</receptionReports>";

            var result = Deserialize(xml);
            Assert.Equal(2, result.ActiveReceivers.Count);
            Assert.Equal("K1ABC", result.ActiveReceivers[0].Callsign);
            Assert.Equal("FN42", result.ActiveReceivers[0].Locator);
        }

        #endregion

        #region SNR Parsing Tests

        [Fact]
        public void SNR_ValidValue_ParsesCorrectly()
        {
            var report = new PskReporterXml.ReceptionReport { SNRString = "-15" };
            Assert.Equal(-15, report.SNR);
        }

        [Fact]
        public void SNR_PositiveValue_ParsesCorrectly()
        {
            var report = new PskReporterXml.ReceptionReport { SNRString = "5" };
            Assert.Equal(5, report.SNR);
        }

        [Fact]
        public void SNR_Null_ReturnsNull()
        {
            var report = new PskReporterXml.ReceptionReport { SNRString = null };
            Assert.Null(report.SNR);
        }

        [Fact]
        public void SNR_EmptyString_ReturnsNull()
        {
            var report = new PskReporterXml.ReceptionReport { SNRString = "" };
            Assert.Null(report.SNR);
        }

        [Fact]
        public void SNR_NonNumeric_ReturnsNull()
        {
            var report = new PskReporterXml.ReceptionReport { SNRString = "N/A" };
            Assert.Null(report.SNR);
        }

        #endregion

        #region CurrentSeconds Tests

        [Fact]
        public void CurrentSeconds_ParsedFromAttribute()
        {
            var xml = @"<?xml version='1.0' encoding='UTF-8'?>
<receptionReports currentSeconds='1700000000'>
</receptionReports>";

            var result = Deserialize(xml);
            Assert.Equal(1700000000L, result.CurrentSeconds);
        }

        #endregion

        #region ActiveReceiver Detail Tests

        [Fact]
        public void ActiveReceiver_AllFields_Parsed()
        {
            var xml = @"<?xml version='1.0' encoding='UTF-8'?>
<receptionReports currentSeconds='1700000000'>
  <activeReceiver
    callsign='K1ABC'
    locator='FN42'
    frequency='14074000'
    region='United States'
    DXCC='United States'
    decoderSoftware='WSJT-X 2.6.1'
    mode='FT8'
    antennaInformation='Vertical'
    rigInformation='IC-7300'
    bands='20m,40m' />
</receptionReports>";

            var result = Deserialize(xml);
            var rx = result.ActiveReceivers[0];

            Assert.Equal("K1ABC", rx.Callsign);
            Assert.Equal("FN42", rx.Locator);
            Assert.Equal(14074000, rx.Frequency);
            Assert.Equal("United States", rx.Region);
            Assert.Equal("United States", rx.DXCC);
            Assert.Equal("WSJT-X 2.6.1", rx.DecoderSoftware);
            Assert.Equal("FT8", rx.Mode);
            Assert.Equal("Vertical", rx.AntennaInformation);
            Assert.Equal("IC-7300", rx.RigInformation);
            Assert.Equal("20m,40m", rx.Bands);
        }

        #endregion
    }
}
