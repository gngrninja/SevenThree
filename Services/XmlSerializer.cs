using System;
using System.IO;
using System.Xml.Serialization;

namespace SevenThree.Services
{
    public class XmlSerializerService
    {
        private Type type;

        public XmlSerializerService(Type type)
        {
            this.type = type;
        }

        public T Deserialize<T>(string input) where T : class
        {
            System.Xml.Serialization.XmlSerializer ser = new System.Xml.Serialization.XmlSerializer(typeof(T));

            using (StringReader sr = new StringReader(input))
            {
                return (T)ser.Deserialize(sr);
            }
        }
    }
}