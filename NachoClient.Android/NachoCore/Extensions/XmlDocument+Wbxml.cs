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

        public static Stream ToWbxmlStream (this XDocument doc, int accountId, bool isLarge, CancellationToken cToken)
        {
            ASWBXML encoder = new ASWBXML (cToken);
            encoder.XmlDoc = doc;
            if (isLarge) {
                var tmp = NcModel.Instance.TmpPath (accountId);
                var fileStream = new FileStream (tmp, FileMode.Create);
                var writer = new BinaryWriter (fileStream);
                Log.Info (Log.LOG_HTTP, "ToWbxmlStream: EmitToStream(L) (#1313)");
                encoder.EmitToStream (writer);
                Log.Info (Log.LOG_HTTP, "ToWbxmlStream: EmitToStream(L) done (#1313)");
                writer.Close ();
                return new FileStream (tmp, FileMode.Open);
            } else {
                var writer = new BinaryWriter (new MemoryStream ());
                Log.Info (Log.LOG_HTTP, "ToWbxmlStream: EmitToStream (#1313)");
                encoder.EmitToStream (writer);
                Log.Info (Log.LOG_HTTP, "ToWbxmlStream: EmitToStream done (#1313)");
                writer.Flush ();
                writer.BaseStream.Seek (0, SeekOrigin.Begin);
                var encoded = new MemoryStream ();
                writer.BaseStream.CopyTo (encoded);
                writer.Close ();
                encoded.Seek (0, SeekOrigin.Begin);
                return encoded;
            }
        }
    }
}

