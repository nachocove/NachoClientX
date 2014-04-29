//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Xml;
using System.Xml.Linq;
using NachoCore;
using NachoCore.Utils;
using System.Collections.Generic;

namespace NachoCore.Utils
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
        protected NcXmlFilterNode Root { set; get; }

        public string NameSpace { set; get; }

        public NcXmlFilter (string nameSpace)
        {
            Root = null;
            NameSpace = nameSpace;
        }

        private void RedactElement (XElement docElement, RedactionType type)
        {
            if (RedactionType.NONE == type) {
                return;
            }

            // Redact the element's content
            if (null != docElement.Value) {
                if (RedactionType.FULL == type) {
                    docElement.Value = "-redacted-";
                } else if (RedactionType.PARTIAL == type) {
                    int contentLen = docElement.Value.Length;
                    docElement.Value = String.Format ("-redacted:{0} bytes-", contentLen);
                } else {
                    NachoAssert.True (false);
                }
            }

            // Remove all children nodes                
            while (docElement.HasElements) {
                docElement.FirstNode.Remove ();
            }
            NachoAssert.True (!docElement.HasElements);
        }

        private void RedactAttributes (XElement docElement, RedactionType type)
        {
            if (RedactionType.NONE == type) {
                return;
            }

            if (RedactionType.FULL == type) {
                // Just delete all attributes
                while (null != docElement.FirstAttribute) {
                    docElement.FirstAttribute.Remove ();
                }
                NachoAssert.True (!docElement.HasAttributes);
                return;
            }

            // Partial redaction for attributes means, it will form a list of attributes
            if (docElement.HasAttributes) {
                string attrList = null;
                while (docElement.HasAttributes) {
                    if (null == attrList) {
                        attrList = docElement.FirstAttribute.Name.ToString ();
                    } else {
                        attrList += "," + docElement.FirstAttribute.Name.ToString ();
                    }
                    docElement.FirstAttribute.Remove ();
                }
                docElement.Add (new XAttribute ("redacted", attrList.ToString ()));
            }
        }

        private void FilterNode (XElement doc, NcXmlFilterNode filter)
        {
            if (System.Xml.XmlNodeType.Element != doc.NodeType) {
                return;
            }

            // Determine the filtering action of this node
            RedactionType elementAction = RedactionType.FULL;
            RedactionType attributeAction = RedactionType.FULL;
            Boolean doRecurse = false;

            NcXmlFilterNode filterNode = filter.FindChildNode (doc);
            if (null == filterNode) {
                // Unknown tag
                Log.Warn("Unknown tag {0}", doc.Name);
            }  else {
                elementAction = filterNode.ElementRedaction;
                attributeAction = filterNode.AttributeRedaction;
                doRecurse = (RedactionType.NONE == elementAction);
            }

            // Filter if necessary
            RedactAttributes (doc, attributeAction);
            RedactElement (doc, elementAction);

            // Recurse into children nodes if necessary
            if (!doRecurse) {
                return;
            }
            for (XNode docNode = doc.FirstNode; null != docNode; docNode = docNode.NextNode) {
                if (XmlNodeType.Element != docNode.NodeType) {
                    continue;
                }
                FilterNode((XElement)docNode, (NcXmlFilterNode)filterNode);
            }
        }

        public XDocument Filter (XDocument doc)
        {
            // Clone the entire XDocument. This is not efficient since we may
            // redact a lot of the elements we clone. Will optimize later if needed
            XDocument docOut = new XDocument (doc);

            FilterNode (docOut.Root, Root);
            return docOut;
        }
    }

    public class NcXmlFilterSet
    {
        private List<NcXmlFilter> FilterList;

        public NcXmlFilterSet ()
        {
            FilterList = new List<NcXmlFilter> ();
        }

        private NcXmlFilter FindFilter (string nameSpace)
        {
            foreach (NcXmlFilter filter in FilterList) {
                if (filter.NameSpace == nameSpace) {
                    return filter;
                }
            }
            return null;
        }

        public XDocument Filter (XDocument doc)
        {
            NcXmlFilter filter = FindFilter (doc.Root.Name.NamespaceName);
            if (null == filter) {
                return null;
            }
            return filter.Filter (doc);
        }

        public void Add (NcXmlFilter filter)
        {
            NachoAssert.True (null == FindFilter (filter.NameSpace));
            FilterList.Add (filter);
        }
    }
}


