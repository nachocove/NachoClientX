using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NachoCore.Wbxml
{
    public class ASWBXMLCodePage
    {
        private string strNamespace = "";
        private string strXmlns = "";
        private Dictionary<byte, string> tokenLookup = new Dictionary<byte,string> ();
        private Dictionary<string, byte> tagLookup = new Dictionary<string, byte> ();
        private Dictionary<string, bool> isOpaqueLookup = new Dictionary<string, bool> ();
        private Dictionary<string, bool> isOpaqueBase64Lookup = new Dictionary<string, bool> ();
        private Dictionary<string, bool> isPeelOff = new Dictionary<string, bool> ();

        public string Namespace {
            get {
                return strNamespace;
            }
            set {
                strNamespace = value;
            }
        }

        public string Xmlns {
            get {
                return strXmlns;
            }
            set {
                strXmlns = value;
            }
        }

        public void AddOpaqueToken (byte token, string tag)
        {
            isOpaqueLookup.Add (tag, true);
            AddToken (token, tag);
        }

        public void AddOpaqueBase64Token (byte token, string tag)
        {
            isOpaqueBase64Lookup.Add (tag, true);
            AddToken (token, tag);
        }

        public void AddPeelOffToken (byte token, string tag)
        {
            isPeelOff.Add (tag, true);
            AddToken (token, tag);
        }

        public void AddToken (byte token, string tag)
        {
            tokenLookup.Add (token, tag);
            tagLookup.Add (tag, token);
        }

        public byte GetToken (string tag)
        {
            if (tagLookup.ContainsKey (tag))
                return tagLookup [tag];

            return 0xFF;
        }

        public bool GetIsOpaque (string tag)
        {
            if (isOpaqueLookup.ContainsKey (tag)) {
                return isOpaqueLookup [tag];
            }
            return false;
        }

        public bool GetIsOpaqueBase64 (string tag)
        {
            if (isOpaqueBase64Lookup.ContainsKey (tag)) {
                return isOpaqueBase64Lookup [tag];
            }
            return false;
        }

        public bool GetIsPeelOff (string tag)
        {
            if (isPeelOff.ContainsKey (tag)) {
                return isPeelOff [tag];
            }
            return false;
        }

        public string GetTag (byte token)
        {
            if (tokenLookup.ContainsKey (token))
                return tokenLookup [token];

            return null;
        }
    }
}
