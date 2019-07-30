using System;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace SevenThree.Models
{
	public class QrzApiXml
	{
		[XmlRoot(ElementName="Callsign")]
		public class Callsign {
			[XmlElement(ElementName="call")]
			public string Call { get; set; }
			[XmlElement(ElementName="aliases")]
			public string Aliases { get; set; }
			[XmlElement(ElementName="dxcc")]
			public string Dxcc { get; set; }
			[XmlElement(ElementName="fname")]
			public string Fname { get; set; }
			[XmlElement(ElementName="name")]
			public string Name { get; set; }
			[XmlElement(ElementName="addr1")]
			public string Addr1 { get; set; }
			[XmlElement(ElementName="addr2")]
			public string Addr2 { get; set; }
			[XmlElement(ElementName="state")]
			public string State { get; set; }
			[XmlElement(ElementName="zip")]
			public string Zip { get; set; }
			[XmlElement(ElementName="country")]
			public string Country { get; set; }
			[XmlElement(ElementName="ccode")]
			public string Ccode { get; set; }
			[XmlElement(ElementName="lat")]
			public string Lat { get; set; }
			[XmlElement(ElementName="lon")]
			public string Lon { get; set; }
			[XmlElement(ElementName="grid")]
			public string Grid { get; set; }
			[XmlElement(ElementName="county")]
			public string County { get; set; }
			[XmlElement(ElementName="fips")]
			public string Fips { get; set; }
			[XmlElement(ElementName="land")]
			public string Land { get; set; }
			[XmlElement(ElementName="efdate")]
			public string Efdate { get; set; }
			[XmlElement(ElementName="expdate")]
			public string Expdate { get; set; }
			[XmlElement(ElementName="p_call")]
			public string P_call { get; set; }
			[XmlElement(ElementName="class")]
			public string Class { get; set; }
			[XmlElement(ElementName="codes")]
			public string Codes { get; set; }
			[XmlElement(ElementName="qslmgr")]
			public string Qslmgr { get; set; }
			[XmlElement(ElementName="email")]
			public string Email { get; set; }
			[XmlElement(ElementName="url")]
			public string Url { get; set; }
			[XmlElement(ElementName="u_views")]
			public string U_views { get; set; }
			[XmlElement(ElementName="bio")]
			public string Bio { get; set; }
			[XmlElement(ElementName="image")]
			public string Image { get; set; }
			[XmlElement(ElementName="serial")]
			public string Serial { get; set; }
			[XmlElement(ElementName="moddate")]
			public string Moddate { get; set; }
			[XmlElement(ElementName="MSA")]
			public string MSA { get; set; }
			[XmlElement(ElementName="AreaCode")]
			public string AreaCode { get; set; }
			[XmlElement(ElementName="TimeZone")]
			public string TimeZone { get; set; }
			[XmlElement(ElementName="GMTOffset")]
			public string GMTOffset { get; set; }
			[XmlElement(ElementName="DST")]
			public string DST { get; set; }
			[XmlElement(ElementName="eqsl")]
			public string Eqsl { get; set; }
			[XmlElement(ElementName="mqsl")]
			public string Mqsl { get; set; }
			[XmlElement(ElementName="cqzone")]
			public string Cqzone { get; set; }
			[XmlElement(ElementName="ituzone")]
			public string Ituzone { get; set; }
			[XmlElement(ElementName="geoloc")]
			public string Geoloc { get; set; }
			[XmlElement(ElementName="born")]
			public string Born { get; set; }
		}

		[XmlRoot(ElementName="Session")]
		public class Session {
			[XmlElement(ElementName="Key")]
			public string Key { get; set; }
			[XmlElement(ElementName="Count")]
			public string Count { get; set; }
			[XmlElement(ElementName="Error")]
			public string Error { get; set; }
			[XmlElement(ElementName="SubExp")]
			public string SubExp { get; set; }
			[XmlElement(ElementName="GMTime")]
			public string GMTime { get; set; }
		}

		[XmlRoot(ElementName="QRZDatabase")]
		public class QRZDatabase {
			[XmlElement(ElementName="Callsign")]
			public Callsign Callsign { get; set; }
			[XmlElement(ElementName="Session")]
			public Session Session { get; set; }
			[XmlAttribute(AttributeName="version")]
			public string Version { get; set; }
		}
	}
}