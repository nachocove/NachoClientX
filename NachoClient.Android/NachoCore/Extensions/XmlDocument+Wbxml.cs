using System;
using System.IO;
using System.Xml.Linq;

namespace NachoCore.Wbxml
{
    public static class XDocument_Wbxml
    {
        public static byte[] ToWbxml (this XDocument doc)
        {
            ASWBXML encoder = new ASWBXML ();
            encoder.XmlDoc = doc;
            return encoder.GetBytes ();
        }

        public static XDocument LoadWbxml (this byte[] wbxml)
        {
            ASWBXML decoder = new ASWBXML ();
            decoder.LoadBytes (wbxml);
            return decoder.XmlDoc;
        }
    }
}

