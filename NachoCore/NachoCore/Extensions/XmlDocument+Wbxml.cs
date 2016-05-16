using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NachoCore.Wbxml;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public static partial class XDocument_Extension
    {
        public static byte[] ToWbxml (this XDocument doc, Boolean? doFiltering = null)
        {
            ASWBXML encoder = new ASWBXML (new CancellationToken (false));
            encoder.XmlDoc = doc;
            return encoder.GetBytes (doFiltering);
        }

        public static FileStream ToWbxmlStream (this XDocument doc, int accountId, CancellationToken cToken)
        {
            ASWBXML encoder = new ASWBXML (cToken);
            encoder.XmlDoc = doc;
            var tmp = NcModel.Instance.TmpPath (accountId, "wbxml-stream");
            using (var fileStream = new FileStream (tmp, FileMode.Create, FileAccess.Write, FileShare.None)) {
                using (var writer = new BinaryWriter (fileStream)) {
                    encoder.EmitToStream (writer);
                }
            }
            return new FileStream (tmp, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}

