using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using TuneLab.Extensions.Formats.VSQX.vsq4;

namespace VsqxFormat.xml
{
    public class typeCDataPhonemes : IXmlSerializable
    {
        private typePhonemes _value;
        public static implicit operator typeCDataPhonemes(typePhonemes value)
        {
            return new typeCDataPhonemes(value);
        }
        public static implicit operator typePhonemes(typeCDataPhonemes cdata)
        {
            return cdata._value;
        }
        public typeCDataPhonemes() : this(null)
        {
        }

        public typeCDataPhonemes(typePhonemes value)
        {
            _value = value;
        }

        public typePhonemes ToObject()
        {
            return _value;
        }

        public override string ToString()
        {
            return _value.ToString();
        }

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public string Value
        {
            get { return _value.Value; }
            set { _value.Value = value; }
        }

        public byte @lock
        {
            get { return _value.@lock; }
            set { _value.@lock = value; }
        }

        public bool lockSpecified
        {
            get { return _value.lockSpecified; }
            set { _value.lockSpecified = value; }
        }


        public void ReadXml(XmlReader reader)
        {
            string phAttribute = reader.HasAttributes ? reader.GetAttribute("lock") : "0";
            _value = new typePhonemes() { @lock = (byte)int.Parse(phAttribute), lockSpecified = int.Parse(phAttribute) == 1, Value = reader.ReadElementString() };
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteCData(_value.Value);
            //            writer.WriteAttributeString("lock", "1");
        }
    }
    public class CData : IXmlSerializable
    {
        protected string _value;

        /// <summary>
        /// Allow direct assignment from string:
        /// CData cdata = "abc";
        /// </summary>
        /// <param name="value">The string being cast to CData.</param>
        /// <returns>A CData object</returns>
        public static implicit operator CData(string value)
        {
            return new CData(value);
        }

        /// <summary>
        /// Allow direct assignment to string:
        /// string str = cdata;
        /// </summary>
        /// <param name="cdata">The CData being cast to a string</param>
        /// <returns>A string representation of the CData object</returns>
        public static implicit operator string(CData cdata)
        {
            if (cdata == null) return null;
            return cdata._value;
        }

        public CData() : this(string.Empty)
        {
        }

        public CData(string value)
        {
            _value = value;
        }

        public override string ToString()
        {
            return _value;
        }

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            _value = reader.ReadElementString();
        }

        public virtual void WriteXml(XmlWriter writer)
        {
            writer.WriteCData(_value);
        }
    }
}
