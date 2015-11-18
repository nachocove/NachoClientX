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
            var tmp = NcModel.Instance.TmpPath (accountId);
            var fileStream = new FileStream (tmp, FileMode.Create);
            var writer = new BinaryWriter (fileStream);
            Log.Debug (Log.LOG_HTTP, "ToWbxmlStream: EmitToStream(L) (#1313)");
            encoder.EmitToStream (writer);
            Log.Debug (Log.LOG_HTTP, "ToWbxmlStream: EmitToStream(L) done (#1313)");
            writer.Close ();
            return new FileStream (tmp, FileMode.Open, FileAccess.Read);
        }
    }
}

