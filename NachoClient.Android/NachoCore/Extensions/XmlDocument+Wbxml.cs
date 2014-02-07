using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NachoCore.Wbxml;

namespace NachoCore.Utils
{
    public static partial class XDocument_Extension
    {
        public static byte[] ToWbxml (this XDocument doc)
        {
            ASWBXML encoder = new ASWBXML (new CancellationToken (false));
            encoder.XmlDoc = doc;
            return encoder.GetBytes ();
        }
    }
}

