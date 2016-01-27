//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using System.IO;
using NachoCore;
using NachoCore.Utils;
using System.Collections.Generic;

namespace NachoCore.Wbxml
{
    // RedactionType describes the extend of redaction for XML
    // elements and attributes. Some interpretation is slightly
    // different for elements and attributes.
    //
    // NONE - No alteration from the original
    // LENGTH - Content is redacted by some hints are given
    //          For elements, the length is given. For attributes,
    //          a list of attributes are given.
    // SHORT_HASH - A truncated SHA-256 hash (to first 8-bytes) with
    //              the length of the original content.
    // FULL_HASH - A SHA-256 hash with the length of the original content.
    // FULL - Element content is removed. Attributes are removed and
    //        no hint of them ever being present.
    public enum RedactionType
    {
        NONE = 0,
        LENGTH = 1,
        SHORT_HASH = 2,
        FULL_HASH = 3,
        FULL = 4,
    };

    public class NcXmlFilterNode : XElement
    {
        public RedactionType ElementRedaction { get; set; }

        public RedactionType AttributeRedaction { get; set; }

        public NcXmlFilterNode (string name, RedactionType elementRedaction, RedactionType attributeRedaction) :
            base (name)
        {
            ElementRedaction = elementRedaction;
            AttributeRedaction = attributeRedaction;
        }

        public NcXmlFilterNode FindChildNode (XElement docElement)
        {
            XNode filterNode = this.FirstNode;
            while (null != filterNode) {
                NcAssert.True (XmlNodeType.Element == this.NodeType);
                XElement filterElement = (XElement)filterNode;
                if (filterElement.Name.LocalName == docElement.Name.LocalName) {
                    return (NcXmlFilterNode)filterElement;
                }
                filterNode = filterNode.NextNode;
            }
            return null; // no match
        }
    }

    public class NcXmlFilter
    {
        public static string[] DEFAULT_NO_REDACTION_VALUES = new string[] {
            "Inbox",
            "Contact:DEFAULT",
            "Event:DEFAULT",
            "Mail:^sync_gmail_group",
            "Mail:DEFAULT",
            "Mail:^k",
            "Mail:^r",
            "Mail:^f",
            "Mail:^t",
            "Mail:^s",
            "Mail:^all",
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000002",
            "00000000-0000-0000-0000-000000000003",
            "00000000-0000-0000-0000-000000000004",
            "00000000-0000-0000-0000-000000000005",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
        };

        public string[] NoRedactionValues = DEFAULT_NO_REDACTION_VALUES;


        public NcXmlFilterSet ParentSet { set; get; }

        public NcXmlFilterNode Root { set; get; }

        public string[] NameSpaces { set; get; }

        // Filtered XDocument
        private XDocument DocOut { get; set; }

        // Current filter node
        private NcXmlFilterNode CurrentFilterNode { get; set; }

        public NcXmlFilter (string nameSpace)
        {
            ParentSet = null;
            Root = null;
            NameSpaces = new [] {nameSpace};
        }

        public NcXmlFilter (string[] nameSpaces)
        {
            ParentSet = null;
            Root = null;
            NameSpaces = nameSpaces;
        }

        public bool ContainsNameSpace (string nameSpace)
        {
            foreach (var ns in NameSpaces) {
                if (ns.ToLowerInvariant () == nameSpace.ToLowerInvariant ()) {
                    return true;
                }
            }
            return false;
        }
    }

    public class NcXmlFilterSet
    {
        private List<NcXmlFilter> FilterList;

        public NcXmlFilterSet ()
        {
            FilterList = new List<NcXmlFilter> ();
        }

        public NcXmlFilter FindFilter (string[] nameSpaces)
        {
            foreach (var ns in nameSpaces) {
                var filter = FindFilter (ns);
                if (null != filter) {
                    return filter;
                }
            }
            return null;
        }

        public NcXmlFilter FindFilter (string nameSpace)
        {
            foreach (NcXmlFilter filter in FilterList) {
                foreach (var ns in filter.NameSpaces)
                if (ns.ToLowerInvariant () == nameSpace.ToLowerInvariant ()) {
                    return filter;
                }
            }
            return null;
        }

        private void Walk (XNode node, NcXmlFilterState filterState, int level)
        {
            filterState.Update (level, node);
            if (XmlNodeType.Element == node.NodeType) {
                XElement element = (XElement)node;
                for (XNode child = element.FirstNode; child != null; child = child.NextNode) {
                    Walk (child, filterState, level + 1);
                }
            }
        }

        public XDocument Filter (XDocument doc, CancellationToken cToken)
        {
            NcXmlFilterState filterState = new NcXmlFilterState (this, cToken);
            filterState.Start ();
            Walk (doc.Root, filterState, 0);
            return filterState.FinalizeXml ();
        }

        public void Add (NcXmlFilter filter)
        {
            NcAssert.True (null == FindFilter (filter.NameSpaces));
            FilterList.Add (filter);
            filter.ParentSet = this;
        }
    }

    public class NcXmlFilterState
    {
        public class Frame
        {
            public NcXmlFilter Filter;
            public NcXmlFilterNode ParentNode;
            public XNode XmlNode;

            public RedactionType ElementRedaction {
                get {
                    if (null == ParentNode) {
                        return RedactionType.FULL;
                    }
                    return ParentNode.ElementRedaction;
                }
            }

            public RedactionType AttributeRedaction {
                get {
                    if (null == ParentNode) {
                        return RedactionType.FULL;
                    }
                    return ParentNode.AttributeRedaction;
                }
            }

            public Frame ()
            {
                Filter = null;
                ParentNode = null;
                XmlNode = null;
            }

            // Copy constructor
            public Frame (Frame frame)
            {
                Filter = frame.Filter;
                ParentNode = frame.ParentNode;
                XmlNode = null;
            }

            // A frame is "redacted" if it has no valid ParentNode.
            // In this case, we cannot get redaction policy.
            public Boolean IsRedacted ()
            {
                return (null == ParentNode);
            }
        }

        // Hold temporary WBXML stream
        public GatedMemoryStream WbxmlBuffer;

        private NcXmlFilterSet FilterSet;

        private Stack<Frame> FilterStack;

        private Boolean GenerateWbxml;

        private List<byte> Wbxml;

        private XDocument XmlDoc;

        private const Boolean DEFAULT_GENERATE_WBXML = false;

        public RedactionType ElementRedaction {
            get {
                if (0 == FilterStack.Count) {
                    return RedactionType.NONE;
                }
                Frame current = FilterStack.Peek ();
                return current.ElementRedaction;
            }
        }

        private CancellationToken CToken;

        public NcXmlFilterState (NcXmlFilterSet filterSet, CancellationToken cToken, Boolean? generateWbxml = null)
        {
            FilterSet = filterSet;
            FilterStack = new Stack<Frame> ();
            GenerateWbxml = generateWbxml ?? DEFAULT_GENERATE_WBXML;
            if (GenerateWbxml) {
                // Output Wbxml
                Wbxml = new List<byte> ();
                XmlDoc = null;
            } else {
                // Output XML
                Wbxml = null;
                XmlDoc = new XDocument ();
            }
            WbxmlBuffer = new GatedMemoryStream ();
            CToken = cToken;
        }

        private static Boolean IsElement (XNode node)
        {
            return ((null != node) && (XmlNodeType.Element == node.NodeType));
        }

        private static Boolean IsContent (XNode node)
        {
            return ((XmlNodeType.Text == node.NodeType) || (XmlNodeType.CDATA == node.NodeType));
        }

        public void Start ()
        {
            if (0 < FilterStack.Count) {
                Log.Warn (Log.LOG_XML_FILTER, "Has previous state. Reset");
            }
            FilterStack.Clear ();
        }

        string GetNamespace (XElement element)
        {
            var ns = element.Name.NamespaceName;
            if (string.IsNullOrEmpty (ns) && element.HasAttributes) {
                foreach (var attr in element.Attributes ()) {
                    if (attr.IsNamespaceDeclaration) {
                        ns = attr.Value;
                        break;
                    }
                }
            }
            return ns;
        }

        private Frame InitializeFrame (XNode node)
        {
            Frame current = null;
            if (0 == FilterStack.Count) {
                current = new Frame ();

                NcAssert.True (IsElement (node));
                XElement element = (XElement)node;
                var ns = GetNamespace (element);
                // if we couldn't find a namespace, try using the element LocalName.
                if (string.IsNullOrEmpty (ns) && !string.IsNullOrEmpty (element.Name.LocalName)) {
                    ns = element.Name.LocalName;
                }
                current.Filter = FilterSet.FindFilter (ns);
                if (null == current.Filter) {
                    Log.Warn (Log.LOG_XML_FILTER, "No filter for namespace {0}", ns);
                } else {
                    current.ParentNode = current.Filter.Root.FindChildNode (element);
                    if (null == current.ParentNode) {
                        Log.Warn (Log.LOG_XML_FILTER, "Unexpected root element {0}", element.Name);
                    }
                }
            } else {
                current = new Frame (FilterStack.Peek ());

                if (IsElement (node)) {
                    XElement element = (XElement)node;
                    var ns = GetNamespace (element);

                    // Is there a namespace switch?
                    if (null != current.Filter) {
                        if (!string.IsNullOrEmpty (ns) && // if the namespace is empty, assume it's the same as the parent.
                            !current.Filter.ContainsNameSpace (ns) &&
                            // Do not do a namespace switch if we are already in redacted mode.
                            // This is because it will put a non-null parent node and make it think
                            // this is no longer in redacted mode. So, just ignore the namespace change
                            !current.IsRedacted ()) {
                            current.Filter = FilterSet.FindFilter (ns);
                            if (null != current.Filter) {
                                current.ParentNode = current.Filter.Root;
                                NcAssert.True (null != current.ParentNode);
                            } else {
                                Log.Warn (Log.LOG_XML_FILTER, "Switching to an unknown namespace {0}:{1}", element.Name, ns);
                                current.ParentNode = null;
                            }
                        }
                    }

                    // Look for the filter node for this element
                    if (null != current.ParentNode) {
                        if (RedactionType.FULL != current.ParentNode.ElementRedaction) {
                            var previousParent = current.ParentNode;
                            current.ParentNode = current.ParentNode.FindChildNode (element);
                            if (null == current.ParentNode) {
                                Log.Warn (Log.LOG_XML_FILTER, "Unknown element tag {0}:{1}\n{2}", previousParent.Name, element.Name, previousParent.ToString ());
                            }
                        } else {
                            current.ParentNode = null;
                        }
                    }
                }
            }
            return current;
        }

        private XElement AddElement (XElement element, byte[] wbxml, Frame frame)
        {
            XElement newElement = null;
            if (GenerateWbxml) {
                Wbxml.AddRange (wbxml);
            } else {
                newElement = new XElement (element.Name);
                if (element.HasAttributes && frame.AttributeRedaction != RedactionType.FULL) {
                    foreach (var attr in element.Attributes ()) {
                        string value;
                        if (attr.IsNamespaceDeclaration) {
                            value = attr.Value;
                        } else {
                            string hash;
                            switch (frame.AttributeRedaction) {
                            default:
                                NcAssert.CaseError ("Should not reach here");
                                return null;

                            case RedactionType.NONE:
                                value = attr.Value;
                                break;
                            case RedactionType.LENGTH:
                                value = String.Format ("[{0} redacted bytes]", attr.Value.Length);
                                break;
                            case RedactionType.SHORT_HASH:
                                hash = ShortHash (attr.Value);
                                value = String.Format ("[{0} redacted bytes] {1}", attr.Value.Length, hash);
                                break;
                            case RedactionType.FULL_HASH:
                                hash = FullHash (attr.Value);
                                value = String.Format ("[{0} redacted bytes] {1}", attr.Value.Length, hash);
                                break;
                            }
                        }
                        newElement.SetAttributeValue (attr.Name, value);
                    }
                }
                if (0 == FilterStack.Count) {
                    NcAssert.True (null == XmlDoc.Root);
                    XmlDoc.Add (newElement);
                } else {
                    Frame current = FilterStack.Peek ();
                    NcAssert.True (null != current.XmlNode);
                    NcAssert.True (IsElement (current.XmlNode));
                    XElement parentElement = (XElement)current.XmlNode;
                    parentElement.Add (newElement);
                }
                frame.XmlNode = newElement;
            }
            return newElement;
        }

        private static string GetContentValue (XNode content)
        {
            NcAssert.True (IsContent (content));
            if (XmlNodeType.Text == content.NodeType) {
                XText text = (XText)content;
                return text.Value;
            } else if (XmlNodeType.CDATA == content.NodeType) {
                XCData data = (XCData)content;
                return data.Value;
            } else {
                NcAssert.True (false);
            }
            return null; // unreachable. but keep compiler happy
        }

        private static int GetContentLength (XNode content)
        {
            var value = GetContentValue (content);
            return value.Length;
        }

        static ConcurrentDictionary<string,string> HashCache = new ConcurrentDictionary<string, string> ();

        public static string ShortHash (XNode content, out int contentLen)
        {
            var value = GetContentValue (content);
            contentLen = value.Length;
            return ShortHash (value);
        }

        public static string ShortHash (string value)
        {
            var hash = FullHash (value).Substring (0, 6);
            return hash;
        }

        public static string FullHash (XNode content, out int contentLen)
        {
            var value = GetContentValue (content);
            contentLen = value.Length;
            return FullHash (value);
        }

        public static string FullHash (string value)
        {
            string hash;
            if (!HashCache.TryGetValue (value, out hash)) {
                hash = HashHelper.Sha256 (value);
                HashCache.TryAdd (value, hash);
            }
            return hash;
        }

        private void RedactElement (XElement newElement, XNode origContent, RedactionType type)
        {
            if (RedactionType.NONE == type) {
                return;
            }
            NcAssert.True (IsElement (newElement));

            // Determine the redaction string
            string value = null;
            int contentLen;
            string hash;
            switch (type) {
            case RedactionType.FULL:
                return;
            case RedactionType.LENGTH:
                value = String.Format ("[{0} redacted bytes]", GetContentLength (origContent));
                break;
            case RedactionType.SHORT_HASH:
                hash = ShortHash (origContent, out contentLen);
                value = String.Format ("[{0} redacted bytes] {1}", contentLen, hash);
                break;
            case RedactionType.FULL_HASH:
                hash = FullHash (origContent, out contentLen);
                value = String.Format ("[{0} redacted bytes] {1}", contentLen, hash);
                break;
            case RedactionType.NONE:
                break;
            default:
                Log.Error (Log.LOG_XML_FILTER, "Unknown redaction type {0}", type);
                NcAssert.True (false);
                break;
            }

            // Encode the redaction string
            if (GenerateWbxml) {
                Wbxml.Add ((byte)GlobalTokens.STR_I);
                Wbxml.AddRange (WBXML.EncodeString (value));
            } else {
                newElement.Value = value;
            }
        }

        private bool IsInNoRedactionList (NcXmlFilter filter, XNode node)
        {
            if (0 == filter.NoRedactionValues.Length) {
                return false;
            }
            var content = GetContentValue (node);
            foreach (string value in filter.NoRedactionValues) {
                if (content == value) {
                    return true;
                }
            }
            return false;
        }

        private void AddContent (XNode node, byte[] wbxml, RedactionType type)
        {
            // Check the latest redaction policy. That should be the parent element
            Frame current = FilterStack.Peek ();
            NcAssert.True (IsContent (node));
            NcAssert.True (null != current);
            NcAssert.True (IsElement (current.XmlNode));
            XElement element = (XElement)current.XmlNode;

            if (RedactionType.NONE != current.ParentNode.ElementRedaction) {
                if (!IsInNoRedactionList (current.Filter, node)) {
                    RedactElement (element, node, type);
                    return;
                }
            }

            if (GenerateWbxml) {
                Wbxml.AddRange (wbxml);
            } else {
                if (XmlNodeType.Text == node.NodeType) {
                    element.Add (new XText ((XText)node));
                } else if (XmlNodeType.CDATA == node.NodeType) {
                    element.Add (new XCData ((XCData)node));
                } else {
                    NcAssert.True (false);
                }
            }
        }

        public void Update (int level, XNode node)
        {
            if (CToken.IsCancellationRequested) {
                throw new OperationCanceledException ();
            }

            byte[] wbxml = WbxmlBuffer.ReadAll ();
            if (null == FilterSet) {
                // If the constructor is not given a valid filter set,
                // the state machine is disabled. We 
                return;
            }

            if ((XmlNodeType.Element != node.NodeType) &&
                (XmlNodeType.Text != node.NodeType) &&
                (XmlNodeType.CDATA != node.NodeType)) {
                return;
            }

            // We previously return from a level but did pop the stack. Do it now.
            while (level < FilterStack.Count) {
                Frame frame = FilterStack.Pop ();
                if (IsElement (frame.XmlNode)) {
                    if (GenerateWbxml) {
                        Wbxml.Add ((byte)GlobalTokens.END);
                    }
                }
            }

            Frame current = InitializeFrame (node);
            if (current.IsRedacted ()) {
                // Two cases - Either the parent frame is also redacted or it is not
                // If the parent is redacted, the redaction should occur in one of 
                // the ancestors. So, no action is taken here. If the parent is not
                // redacted, this is the first frame and we redact this.
                if ((0 == FilterStack.Count) || !FilterStack.Peek ().IsRedacted ()) {
                    if (IsElement (node)) {
                        XElement element = (XElement)node;
                        XElement newElement = AddElement (element, wbxml, current);
                        NcAssert.True (null != newElement);
                    }
                }
            } else {
                if (IsElement (node)) {
                    // Element - Find the redaction policy of this node from parent
                    XElement element = (XElement)node;

                    // Regardless of redaction policy. Always add the tag itself.
                    XElement newElement = AddElement (element, wbxml, current);
                    NcAssert.True (null != newElement);
                } else if ((XmlNodeType.Text == node.NodeType) ||
                           (XmlNodeType.CDATA == node.NodeType)) {
                    AddContent (node, wbxml, current.ElementRedaction);
                }
            }

            FilterStack.Push (current);
        }

        public byte[] Finalize ()
        {
            if (GenerateWbxml) {
                return Wbxml.ToArray ();
            }

            // Cheating!! Use ASWBXML class to encode. This is less efficient
            // because it walks the tree twice.
            ASWBXML wbxml = new ASWBXML (new CancellationToken (false));
            wbxml.XmlDoc = XmlDoc;
            return wbxml.GetBytes (false);
        }

        public XDocument FinalizeXml ()
        {
            if (!GenerateWbxml) {
                return new XDocument (XmlDoc);
            }

            // Cheating!! Use ASWBXL class to decode. This is less efficient
            // because it walks the tree twice.
            ASWBXML wbxml = new ASWBXML (new CancellationToken (false));
            wbxml.LoadBytes (0, Wbxml.ToArray ());
            return new XDocument (wbxml.XmlDoc);
        }
    }
}


