//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Xml;
using System.Xml.Linq;
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
    // PARTIAL - Content is redacted by some hints are given
    //           For elements, the length is given. For attributes,
    //           a list of attributes are given.
    // FULL - Element content is removed. Attributes are removed and
    //        no hint of them ever being present.
    public enum RedactionType {
        NONE = 0,
        PARTIAL = 1,
        FULL = 2, 
    };

    public class NcXmlFilterNode : XElement
    {
        public RedactionType ElementRedaction { get; set; }
        public RedactionType AttributeRedaction { get; set; }

        public NcXmlFilterNode (string name, RedactionType elementRedaction, RedactionType attributeRedaction) :
        base(name)
        {
            ElementRedaction = elementRedaction;
            AttributeRedaction = attributeRedaction;
        }

        public NcXmlFilterNode FindChildNode (XElement docElement)
        {
            XNode filterNode = this.FirstNode;
            while (null != filterNode) {
                NachoAssert.True (XmlNodeType.Element == this.NodeType);
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
        public NcXmlFilterSet ParentSet { set; get; }

        public NcXmlFilterNode Root { set; get; }

        public string NameSpace { set; get; }

        // Filtered XDocument
        private XDocument DocOut { get; set; }

        // Current filter node
        private NcXmlFilterNode CurrentFilterNode { get; set; }

        public NcXmlFilter (string nameSpace)
        {
            ParentSet = null;
            Root = null;
            NameSpace = nameSpace;
        }
    }

    public class NcXmlFilterSet
    {
        private List<NcXmlFilter> FilterList;

        public NcXmlFilterSet ()
        {
            FilterList = new List<NcXmlFilter> ();
        }

        public NcXmlFilter FindFilter (string nameSpace)
        {
            foreach (NcXmlFilter filter in FilterList) {
                if (filter.NameSpace == nameSpace) {
                    return filter;
                }
            }
            return null;
        }

        private void Walk (XNode node, NcXmlFilterState filterState, int level)
        {
            filterState.Update (level, node, null);
            if (XmlNodeType.Element == node.NodeType) {
                XElement element = (XElement)node;
                for (XNode child = element.FirstNode; child != null; child = child.NextNode) {
                    Walk (child, filterState, level+1);
                }
            }
        }

        public XDocument Filter (XDocument doc)
        {
            NcXmlFilterState filterState = new NcXmlFilterState (this);
            filterState.Start ();
            Walk (doc.Root, filterState, 0);
            return filterState.FinalizeXml ();
        }

        public void Add (NcXmlFilter filter)
        {
            NachoAssert.True (null == FindFilter (filter.NameSpace));
            FilterList.Add (filter);
            filter.ParentSet = this;
        }
    }

    public class NcXmlFilterState {
        public class Frame {
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

        public NcXmlFilterState (NcXmlFilterSet filterSet, Boolean? generateWbxml = null)
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
        }

        private Boolean IsElement (XNode node)
        {
            return ((null != node) && (XmlNodeType.Element == node.NodeType));
        }

        private Boolean IsContent (XNode node)
        {
            return ((XmlNodeType.Text == node.NodeType) || (XmlNodeType.CDATA == node.NodeType));
        }

        public void Start ()
        {
            if (0 < FilterStack.Count) {
                Log.Warn ("Has previous state. Reset");
            }
            FilterStack.Clear ();
        }

        private Frame InitializeFrame (XNode node)
        {
            Frame current = null;

            if (0 == FilterStack.Count) {
                current = new Frame ();

                NachoAssert.True (IsElement (node));
                XElement element = (XElement)node;
                current.Filter = FilterSet.FindFilter (element.Name.NamespaceName);
                if (null == current.Filter) {
                    Log.Warn ("No filter for namespace {0}", element.Name.NamespaceName);
                } else {
                    if (current.Filter.Root.Name.LocalName == element.Name.LocalName) {
                        current.ParentNode = current.Filter.Root;
                    } else {
                        Log.Warn ("Unexpected root element {0}", element.Name);
                    }
                }
            } else {
                current = new Frame (FilterStack.Peek ());

                if (IsElement(node)) {
                    XElement element = (XElement)node;

                    // Is there a namespace switch?
                    if (null != current.Filter) {
                        if (current.Filter.NameSpace != element.Name.NamespaceName) {
                            current.Filter = FilterSet.FindFilter (element.Name.NamespaceName);
                            if (null != current.Filter) {
                                current.ParentNode = current.Filter.Root;
                            } else {
                                Log.Warn ("Switching to an unknown namespace {0}", element.Name.NamespaceName);
                                current.ParentNode = null;
                            }
                        }
                    }

                    // Look for the filter node for this element
                    if (null != current.ParentNode) {
                        current.ParentNode = current.ParentNode.FindChildNode (element);
                        if (null == current.ParentNode) {
                            Log.Warn ("Unknown element tag {0}", element.Name);
                        }
                    }
                }
            }
            return current;
        }

        private XElement AddElement (XElement element, byte [] wbxml, Frame frame)
        {
            XElement newElement = null;
            if (GenerateWbxml) {
                Wbxml.AddRange (wbxml);
            } else {
                newElement = new XElement (element.Name);
                if (0 == FilterStack.Count) {
                    NachoAssert.True (null == XmlDoc.Root);
                    XmlDoc.Add (newElement);
                } else {
                    Frame current = FilterStack.Peek ();
                    NachoAssert.True (null != current.XmlNode);
                    NachoAssert.True (IsElement(current.XmlNode));
                    XElement parentElement = (XElement)current.XmlNode;
                    parentElement.Add(newElement);
                }
                frame.XmlNode = newElement;
            }
            return newElement;
        }

        private void RedactElement (XElement newElement, XElement origElement, RedactionType type)
        {
            if (RedactionType.NONE == type) {
                return;
            }

            // Determine the redaction string
            string value = null;
            if (RedactionType.FULL == type) {
                value = "-redacted-";
            } else if (RedactionType.FULL == type) {
                int contentLen = origElement.Value.Length;
                value = String.Format ("-redacted:{0} bytes-", contentLen);
            } else {
                Log.Error ("Unknown redaction type {0}", type);
                NachoAssert.True (false);
            }

            // Encode the redaction string
            if (GenerateWbxml) {
                Wbxml.Add ((byte)GlobalTokens.STR_I);
                Wbxml.AddRange (WBXML.EncodeString (value));
            } else {
                // Change the value of the element
                if (null != origElement.Value) {
                    newElement.Value = value;
                }
            }
        }

        private void AddContent (XNode node, byte[] wbxml)
        {
            // Check the latest redaction policy. That should be the parent element
            Frame current = FilterStack.Peek ();
            NachoAssert.True (!IsElement(node));
            if (RedactionType.NONE != current.ParentNode.ElementRedaction) {
                return;
            }

            if (GenerateWbxml) {
                Wbxml.AddRange (wbxml);
            } else {
                XElement element = (XElement)current.XmlNode;
                if (XmlNodeType.Text == node.NodeType) {
                    element.Add (new XText ((XText)node));
                } else if (XmlNodeType.CDATA == node.NodeType) {
                    element.Add (new XCData ((XCData)node));
                }
            }
        }

        public void Update (int level, XNode node, byte[] wbxml)
        {
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
                        RedactElement (newElement, element, RedactionType.FULL);
                    }
                }
            } else {
                if (IsElement(node)) {
                    // Element - Find the redaction policy of this node from parent
                    XElement element = (XElement)node;

                    // Regardless of redaction policy. Always add the tag itself.
                    XElement newElement = AddElement (element, wbxml, current);

                    // If there is redaction of some kind, add the text here.
                    // The reason is that if this is a xs:complexType, there will not 
                    // be a Text or CDATA node for us to insert anything.
                    RedactElement (newElement, element, current.ElementRedaction);
                } else if ((XmlNodeType.Text == node.NodeType) ||
                           (XmlNodeType.CDATA == node.NodeType)) {
                    AddContent (node, wbxml);
                }
            }

            FilterStack.Push (current);
        }

        public byte[] Finalize ()
        {
            return Wbxml.ToArray ();
        }

        public XDocument FinalizeXml ()
        {
            return new XDocument (XmlDoc);
        }
    }
}


