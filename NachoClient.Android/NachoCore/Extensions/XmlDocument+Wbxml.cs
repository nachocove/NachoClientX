using System;
using System.IO;
using System.Xml.Linq;
using NachoCore.Wbxml;

namespace NachoCore.Utils
{
    public static partial class XDocument_Extension
    {
        public static byte[] ToWbxml (this XDocument doc)
        {
            ASWBXML encoder = new ASWBXML ();
            encoder.XmlDoc = doc;
            return encoder.GetBytes ();
        }
    }
}

