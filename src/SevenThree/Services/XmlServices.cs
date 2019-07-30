using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using SevenThree.Models;

namespace SevenThree.Services
{
    public class XmlServices
    {

        public QrzApiXml.QRZDatabase GetQrzResultFromString(StreamReader input)
        {
            var ser = new XmlSerializer(typeof(QrzApiXml.QRZDatabase), new XmlRootAttribute("QRZDatabase"));
            var converted = new QrzApiXml.QRZDatabase();

            using (XmlTextReader reader = new XmlTextReader(input))
            {
                reader.Namespaces = false;
                converted = (QrzApiXml.QRZDatabase)ser.Deserialize(reader);
            }

            return converted;
        }
        
    }
}