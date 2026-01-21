using System.Collections.Generic;
using System.Xml.Serialization;

namespace SevenThree.Models
{
    /// <summary>
    /// Models for PSKReporter API XML responses
    /// Note: PSKReporter only supports XML output, not JSON
    /// </summary>
    public class PskReporterXml
    {
        [XmlRoot(ElementName = "receptionReports")]
        public class ReceptionReports
        {
            [XmlAttribute(AttributeName = "currentSeconds")]
            public long CurrentSeconds { get; set; }

            [XmlElement(ElementName = "lastSequenceNumber")]
            public SequenceNumber LastSequenceNumber { get; set; }

            [XmlElement(ElementName = "maxFlowStartSeconds")]
            public MaxFlowStart MaxFlowStartSeconds { get; set; }

            [XmlElement(ElementName = "receptionReport")]
            public List<ReceptionReport> ReceptionReportList { get; set; } = new();

            [XmlElement(ElementName = "activeReceiver")]
            public List<ActiveReceiver> ActiveReceivers { get; set; } = new();
        }

        public class SequenceNumber
        {
            [XmlAttribute(AttributeName = "value")]
            public long Value { get; set; }
        }

        public class MaxFlowStart
        {
            [XmlAttribute(AttributeName = "value")]
            public long Value { get; set; }
        }

        [XmlRoot(ElementName = "receptionReport")]
        public class ReceptionReport
        {
            [XmlAttribute(AttributeName = "receiverCallsign")]
            public string ReceiverCallsign { get; set; }

            [XmlAttribute(AttributeName = "receiverLocator")]
            public string ReceiverLocator { get; set; }

            [XmlAttribute(AttributeName = "senderCallsign")]
            public string SenderCallsign { get; set; }

            [XmlAttribute(AttributeName = "senderLocator")]
            public string SenderLocator { get; set; }

            [XmlAttribute(AttributeName = "frequency")]
            public long Frequency { get; set; }

            [XmlAttribute(AttributeName = "flowStartSeconds")]
            public long FlowStartSeconds { get; set; }

            [XmlAttribute(AttributeName = "mode")]
            public string Mode { get; set; }

            // XmlSerializer can't handle nullable value types as attributes
            // Use string and parse manually
            [XmlAttribute(AttributeName = "sNR")]
            public string SNRString { get; set; }

            [XmlIgnore]
            public int? SNR => int.TryParse(SNRString, out var snr) ? snr : null;

            [XmlAttribute(AttributeName = "receiverDXCC")]
            public string ReceiverDXCC { get; set; }

            [XmlAttribute(AttributeName = "receiverDXCCCode")]
            public string ReceiverDXCCCode { get; set; }

            [XmlAttribute(AttributeName = "senderDXCC")]
            public string SenderDXCC { get; set; }

            [XmlAttribute(AttributeName = "senderDXCCCode")]
            public string SenderDXCCCode { get; set; }

            [XmlAttribute(AttributeName = "isSender")]
            public string IsSender { get; set; }
        }

        [XmlRoot(ElementName = "activeReceiver")]
        public class ActiveReceiver
        {
            [XmlAttribute(AttributeName = "callsign")]
            public string Callsign { get; set; }

            [XmlAttribute(AttributeName = "locator")]
            public string Locator { get; set; }

            [XmlAttribute(AttributeName = "frequency")]
            public long Frequency { get; set; }

            [XmlAttribute(AttributeName = "region")]
            public string Region { get; set; }

            [XmlAttribute(AttributeName = "DXCC")]
            public string DXCC { get; set; }

            [XmlAttribute(AttributeName = "decoderSoftware")]
            public string DecoderSoftware { get; set; }

            [XmlAttribute(AttributeName = "mode")]
            public string Mode { get; set; }

            [XmlAttribute(AttributeName = "antennaInformation")]
            public string AntennaInformation { get; set; }

            [XmlAttribute(AttributeName = "rigInformation")]
            public string RigInformation { get; set; }

            [XmlAttribute(AttributeName = "bands")]
            public string Bands { get; set; }
        }
    }

    /// <summary>
    /// Processed spot data for display
    /// </summary>
    public class SpotInfo
    {
        public string ReceiverCallsign { get; set; }
        public string ReceiverLocator { get; set; }
        public string ReceiverDXCC { get; set; }
        public string SenderCallsign { get; set; }
        public string SenderLocator { get; set; }
        public long FrequencyHz { get; set; }
        public string Band { get; set; }
        public string Mode { get; set; }
        public int? SNR { get; set; }
        public System.DateTime Timestamp { get; set; }
        public double? DistanceKm { get; set; }
        public double? DistanceMi { get; set; }
    }

    /// <summary>
    /// Aggregated propagation statistics
    /// </summary>
    public class PropagationStats
    {
        public string Callsign { get; set; }
        public int TotalSpots { get; set; }
        public int UniqueReceivers { get; set; }
        public int UniqueCountries { get; set; }
        public Dictionary<string, int> SpotsByBand { get; set; } = new();
        public Dictionary<string, int> SpotsByCountry { get; set; } = new();
        public Dictionary<string, int> SpotsByMode { get; set; } = new();
        public SpotInfo FurthestSpot { get; set; }
        public System.DateTime QueryTime { get; set; }
        public int TimeWindowMinutes { get; set; }
    }
}
