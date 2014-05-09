//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Wbxml;

namespace NachoCore.Wbxml
{
    public class WBXML
    {
        const byte versionByte = 0x03;
        const byte publicIdentifierByte = 0x01;
        const byte characterSetByte = 0x6A;
        // UTF-8
        const byte stringTableLengthByte = 0x00;
        const Boolean DEFAULT_FILTERING = true;

        public XDocument XmlDoc { set; get; }

        protected ASWBXMLCodePage[] codePages;
        private int currentCodePage = 0;
        private int defaultCodePage = -1;
        private CancellationToken CToken;

        public WBXML (CancellationToken cToken)
        {
            CToken = cToken;
        }

        public string GetXml ()
        {
            return XmlDoc.ToString (SaveOptions.DisableFormatting);
        }

        public void LoadBytes (Stream byteWBXML, Boolean? doFiltering = null)
        {
            XmlDoc = new XDocument (new XDeclaration ("1.0", "utf-8", "yes"));
            int level = 0;

            ASWBXMLByteQueue bytes = new ASWBXMLByteQueue (byteWBXML, doFiltering ?? DEFAULT_FILTERING);

            NcXmlFilterState filter = null;
            if (doFiltering ?? DEFAULT_FILTERING) {
                filter = new NcXmlFilterState (AsXmlFilterSet.Responses);
            } else {
                filter = new NcXmlFilterState (null);
            }
            filter.Start ();

            // Version is ignored
            byte version = bytes.Dequeue ();
            if (versionByte != version) {
                Log.Warn ("Unexpected version {0}", (int)version);
            }


            // Public Identifier is ignored
            bytes.DequeueMultibyteInt ();

            // Character set
            // Currently only UTF-8 is supported, throw if something else
            int charset = bytes.DequeueMultibyteInt ();
            if (charset != 0x6A)
                throw new InvalidDataException ("ASWBXML only supports UTF-8 encoded XML.");

            // String table length
            // This should be 0, MS-ASWBXML does not use string tables
            int stringTableLength = bytes.DequeueMultibyteInt ();
            if (stringTableLength != 0)
                throw new InvalidDataException ("WBXML data contains a string table.");

            bytes.GetRedactCopy ();

            // Now we should be at the body of the data.
            // Add the declaration
            XElement currentNode = null;

            while (bytes.Peek () >= 0) {
                if (CToken.IsCancellationRequested) {
                    throw new TaskCanceledException ();
                }
                byte currentByte = bytes.Dequeue ();

                switch ((GlobalTokens)currentByte) {
                // Check for a global token that we actually implement
                case GlobalTokens.SWITCH_PAGE:
                    int newCodePage = (int)bytes.Dequeue ();
                    if (newCodePage >= 0 && newCodePage < 25) {
                        currentCodePage = newCodePage;
                    } else {
                        throw new InvalidDataException (string.Format ("Unknown code page ID 0x{0:X} encountered in WBXML", currentByte));
                    }
                    break;
                case GlobalTokens.END:
                    if (currentNode.Parent != null) {
                        currentNode = currentNode.Parent;
                        NachoAssert.True (0 < level);
                        level--;
                        bytes.RedactCopyEnabled = true;
                        bytes.GetRedactCopy ();
                    } else {
                        //throw new InvalidDataException("END global token encountered out of sequence");
                    }
                    break;
                case GlobalTokens.OPAQUE:
                    int OpaqueLength = bytes.DequeueMultibyteInt ();
                    var OpaqueBytes = bytes.DequeueOpaque (OpaqueLength);
                    XText newOpaqueNode;
                    if (codePages [currentCodePage].GetIsOpaqueBase64 (currentNode.Name.LocalName)) {
                        newOpaqueNode = new XText (Convert.ToBase64String (OpaqueBytes));
                    } else {
                        newOpaqueNode = new XText (System.Text.Encoding.UTF8.GetString (OpaqueBytes));
                    }
                    currentNode.Add (newOpaqueNode);
                    filter.Update (level, newOpaqueNode, bytes.GetRedactCopy ());

                    //XmlCDataSection newOpaqueNode = xmlDoc.CreateCDataSection(bytes.DequeueString(CDATALength));
                    //currentNode.AppendChild(newOpaqueNode);
                    break;
                case GlobalTokens.STR_I:
                    XText newTextNode;
                    if (codePages [currentCodePage].GetIsPeelOff (currentNode.Name.LocalName)) {
                        newTextNode = new XText ("");
                        switch (currentCodePage) {
                        case ASWBXML.KCodePage_AirSyncBase:
                            var content = bytes.DequeueString ();
                            var data = McBody.Save(content);
                            currentNode.Add (new XAttribute ("nacho-body-id", data.Id.ToString ()));
                            break;

                        case ASWBXML.KCodePage_ItemOperations:
                            try {
                                var guidString = Guid.NewGuid ().ToString ("N");
                                using (var fileStream = McAttachment.TempFileStream (guidString)) {
                                    using (var cryptoStream = new CryptoStream (new BufferedStream (fileStream), 
                                        new FromBase64Transform (), CryptoStreamMode.Write)) {
                                        if (bytes.DequeueStringToStream (cryptoStream)) {
                                            currentNode.Add (new XAttribute ("nacho-attachment-file", guidString));
                                        } else {
                                            Log.Error (Log.LOG_AS, "Failure while trying to write attachment.");
                                        }
                                    }
                                }
                            } catch (Exception ex) {
                                // If we can't write the file, don't add the attr.
                                Log.Error (Log.LOG_AS, "Exception while trying to write attachment {0}", ex.ToString ());
                            }
                            break;

                        default:
                            NachoAssert.True (false);
                            break;
                        }
                    } else {
                        newTextNode = new XText (bytes.DequeueString ());
                    }
                    currentNode.Add (newTextNode);
                    filter.Update (level, newTextNode, bytes.GetRedactCopy ());
                    break;
                // According to MS-ASWBXML, these features aren't used
                case GlobalTokens.ENTITY:
                case GlobalTokens.EXT_0:
                case GlobalTokens.EXT_1:
                case GlobalTokens.EXT_2:
                case GlobalTokens.EXT_I_0:
                case GlobalTokens.EXT_I_1:
                case GlobalTokens.EXT_I_2:
                case GlobalTokens.EXT_T_0:
                case GlobalTokens.EXT_T_1:
                case GlobalTokens.EXT_T_2:
                case GlobalTokens.LITERAL:
                case GlobalTokens.LITERAL_A:
                case GlobalTokens.LITERAL_AC:
                case GlobalTokens.LITERAL_C:
                case GlobalTokens.PI:
                case GlobalTokens.STR_T:
                    throw new InvalidDataException (string.Format ("Encountered unknown global token 0x{0:X}.", currentByte));

                // If it's not a global token, it should be a tag
                default:
                    bool hasAttributes = false;
                    bool hasContent = false;

                    hasAttributes = (currentByte & 0x80) > 0;
                    hasContent = (currentByte & 0x40) > 0;

                    byte token = (byte)(currentByte & 0x3F);

                    if (hasAttributes)
                        // Maybe use Trace.Assert here?
                        throw new InvalidDataException (string.Format ("Token 0x{0:X} has attributes.", token));

                    string strTag = codePages [currentCodePage].GetTag (token);
                    if (strTag == null) {
                        strTag = string.Format ("UNKNOWN_TAG_{0,2:X}", token);
                    }
                    XNamespace ns = codePages [currentCodePage].Namespace;
                    XElement newNode = new XElement (ns + strTag);
                    //XmlNode newNode = xmlDoc.CreateElement(codePages[currentCodePage].Xmlns, strTag, codePages[currentCodePage].Namespace);
                    //newNode.Prefix = codePages[currentCodePage].Xmlns;
                    if (null == currentNode) {
                        XmlDoc.Add (newNode);
                        filter.Update (level, newNode, bytes.GetRedactCopy ());
                    } else {
                        currentNode.Add (newNode);
                        filter.Update (level, newNode, bytes.GetRedactCopy ());
                    }
                    // TODO - Currently, we create a copy of all dequeued bytes for XML filter state update.
                    // The problem is that XText or XCDATA node may have a lot of bytes. Creating a 
                    // temporary copy of a 100 MB .pdf file in memory just to have it redacted away is not
                    // efficient. So, we should check the result after an update and disable redact copy if the result is full.
                    //if (RedactionType.NONE != filter.ElementRedaction) {
                    //    bytes.RedactCopyEnabled = false;
                    //}

                    if (hasContent) {
                        currentNode = newNode;
                        level++;
                    }
                    break;
                }
            }

            if (doFiltering ?? DEFAULT_FILTERING) {
                // TODO - Need to feed the redacted XML into a storage that can hold and
                // forward to the telemetry server.
                Log.Info ("response_debug_XML = \n{0}", filter.FinalizeXml ());
            }
        }

        public byte[] GetBytes (Boolean? doFiltering = null)
        {
            List<byte> byteList = new List<byte> ();

            NcXmlFilterState filter = null;
            if (doFiltering ?? DEFAULT_FILTERING) {
                filter = new NcXmlFilterState (AsXmlFilterSet.Requests);
            } else {
                filter = new NcXmlFilterState (null);
            }
            filter.Start ();

            byteList.Add (versionByte);
            byteList.Add (publicIdentifierByte);
            byteList.Add (characterSetByte);
            byteList.Add (stringTableLengthByte);
            byteList.AddRange (EncodeNode (XmlDoc.Root, 0, filter));

            if (doFiltering ?? DEFAULT_FILTERING) {
                // TODO - Need to feed the redacted XML into a storage that
                // can hold and forward to the telemetry server.
                Log.Info ("request_debug_XML = \n{0}", filter.FinalizeXml ());
            }

            return byteList.ToArray ();
        }

        private byte[] EncodeNode (XNode node, int level, NcXmlFilterState filter)
        {
            List<byte> byteList = new List<byte> ();

            switch (node.NodeType) {
            case XmlNodeType.Element:
                var element = (XElement)node;
                if (element.HasAttributes) {
                    ParseXmlnsAttributes (element);
                }

                if (SetCodePageByXmlns (element.Name.NamespaceName)) {
                    byteList.Add ((byte)GlobalTokens.SWITCH_PAGE);
                    byteList.Add ((byte)currentCodePage);
                }

                byte token = codePages [currentCodePage].GetToken (element.Name.LocalName);

                if (element.HasElements || !element.IsEmpty) {
                    token |= 0x40;
                }

                byteList.Add (token);

                if (null != filter) {
                    filter.Update (level, node, byteList.ToArray ());
                }

                if (element.HasElements || !element.IsEmpty) {
                    foreach (XNode child in element.Nodes()) {
                        byteList.AddRange (EncodeNode (child, level + 1, filter));
                    }

                    byteList.Add ((byte)GlobalTokens.END);
                }
                break;
            case XmlNodeType.Text:
                var text = (XText)node;
                if (codePages [currentCodePage].GetIsOpaque (text.Parent.Name.LocalName)) {
                    byteList.Add ((byte)GlobalTokens.OPAQUE);
                    byteList.AddRange (EncodeOpaque (text.Value));
                    filter.Update (level, node, byteList.ToArray ());
                } else if (codePages [currentCodePage].GetIsOpaqueBase64 (text.Parent.Name.LocalName)) {
                    byteList.Add ((byte)GlobalTokens.OPAQUE);
                    byteList.AddRange (EncodeOpaque (Convert.ToBase64String 
                        (System.Text.UTF8Encoding.UTF8.GetBytes (text.Value))));
                    filter.Update (level, node, byteList.ToArray ());
                } else {
                    byteList.Add ((byte)GlobalTokens.STR_I);
                    byteList.AddRange (EncodeString (text.Value));
                    filter.Update (level, node, byteList.ToArray ());
                }
                break;
            case XmlNodeType.CDATA:
                var cdata = (XCData)node;
                byteList.Add ((byte)GlobalTokens.OPAQUE);
                byteList.AddRange (EncodeOpaque (cdata.Value));
                filter.Update (level, node, byteList.ToArray ());
                break;
            default:
                break;
            }

            return byteList.ToArray ();
        }

        private int GetCodePageByXmlns (string xmlns)
        {
            for (int i = 0; i < codePages.Length; i++) {
                if (codePages [i].Xmlns.ToUpper () == xmlns.ToUpper ()) {
                    return i;
                }
            }

            return -1;
        }

        private int GetCodePageByNamespace (string nameSpace)
        {
            for (int i = 0; i < codePages.Length; i++) {
                if (codePages [i].Namespace.ToUpper () == nameSpace.ToUpper ()) {
                    return i;
                }
            }

            return -1;
        }

        private bool SetCodePageByXmlns (string xmlns)
        {
            if (xmlns == null || xmlns == "") {
                // Try default namespace
                if (currentCodePage != defaultCodePage) {
                    currentCodePage = defaultCodePage;
                    return true;
                }

                return false;
            }

            // Try current first
            if (codePages [currentCodePage].Xmlns.ToUpper () == xmlns.ToUpper ()) {
                return false;
            }

            for (int i = 0; i < codePages.Length; i++) {
                if (codePages [i].Xmlns.ToUpper () == xmlns.ToUpper ().TrimEnd (':')) {
                    currentCodePage = i;
                    return true;
                }
            }

            throw new InvalidDataException (string.Format ("Unknown Xmlns: {0}.", xmlns));
        }

        private void ParseXmlnsAttributes (XElement element)
        {
            foreach (XAttribute attribute in element.Attributes()) {
                int codePage = GetCodePageByNamespace (attribute.Value);
                if (attribute.Name.ToString ().ToUpper () == "XMLNS") {
                    // <foo xmlns="...">
                    defaultCodePage = codePage;
                } else if (attribute.Name.Namespace.ToString ().ToUpper () == "XMLNS") {
                    // <foo xmlns:bar="...">
                    codePages [codePage].Xmlns = attribute.Name.LocalName;
                }
            }
        }

        public static byte[] EncodeString (string value)
        {
            List<byte> byteList = new List<byte> ();

            char[] charArray = value.ToCharArray ();

            for (int i = 0; i < charArray.Length; i++) {
                byteList.Add ((byte)charArray [i]);
            }

            byteList.Add (0x00);

            return byteList.ToArray ();
        }

        private byte[] EncodeOpaque (string value)
        {
            List<byte> byteList = new List<byte> ();

            char[] charArray = value.ToCharArray ();

            byteList.AddRange (EncodeMultiByteInteger (charArray.Length));

            for (int i = 0; i < charArray.Length; i++) {
                byteList.Add ((byte)charArray [i]);
            }

            return byteList.ToArray ();
        }

        private byte[] EncodeMultiByteInteger (int value)
        {
            List<byte> byteList = new List<byte> ();

            while (value > 0) {
                byte addByte = (byte)(value & 0x7F);

                if (byteList.Count > 0) {
                    addByte |= 0x80;
                }

                byteList.Insert (0, addByte);

                value >>= 7;
            }

            return byteList.ToArray ();
        }
    }
}

