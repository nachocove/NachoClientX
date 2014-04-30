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
        public NcXmlFilterSet ParentSet { set; get; }

        protected NcXmlFilterNode Root { set; get; }

        public string NameSpace { set; get; }

        public NcXmlFilter (string nameSpace)
        {
            ParentSet = null;
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

        private Boolean FilterNode (XElement doc, NcXmlFilter filter, NcXmlFilterNode parentFilterNode,
            out NcXmlFilter newFilter, out NcXmlFilterNode newFilterNode)
        {
            newFilter = null;
            newFilterNode = null;

            if (null == filter) {
                NachoAssert.True (null == parentFilterNode);
                return false;
            }

            if (System.Xml.XmlNodeType.Element != doc.NodeType) {
                return false;
            }
                
            if (doc.Name.NamespaceName != filter.NameSpace) {
                // There is a namespace switch. We need to find a new filter.
                NachoAssert.True (null != filter.ParentSet);
                newFilter = filter.ParentSet.FindFilter (doc.Name.NamespaceName);
                if (null == newFilter) {
                    // Somehow we are missing a filter for a namespace. Return false to stop
                    // the walk into this XML subtree and redact the entire thing.
                    Log.Error ("Unknown namespace {0}", doc.Name.NamespaceName);
                    RedactElement (doc, RedactionType.FULL);
                    RedactAttributes (doc, RedactionType.FULL);
                    return false;
                }
                filter = newFilter;
                parentFilterNode = filter.Root;
            } else {
                newFilter = filter;
            }

            // Determine the filtering action of this node
            RedactionType elementAction = RedactionType.FULL;
            RedactionType attributeAction = RedactionType.FULL;
            Boolean doRecurse = false;

            if (null != parentFilterNode) {
                newFilterNode = parentFilterNode.FindChildNode (doc);
  
            } else {
                if (filter.Root.Name.LocalName == doc.Name.LocalName) {
                    newFilterNode = filter.Root;
                }
            }
            if (null == newFilterNode) {
                // Unknown tag
                Log.Warn ("Unknown tag {0}", doc.Name);
            } else {
                elementAction = newFilterNode.ElementRedaction;
                attributeAction = newFilterNode.AttributeRedaction;
                doRecurse = (RedactionType.NONE == elementAction);
            }

            // Filter if necessary
            RedactAttributes (doc, attributeAction);
            RedactElement (doc, elementAction);

            // Recurse into children nodes if necessary
            if (!doRecurse) {
                newFilter = null;
                newFilterNode = null;
                return false;
            }
            return true;
        }

        public XDocument Filter (XDocument doc)
        {
            // Clone the entire XDocument. This is not efficient since we may
            // redact a lot of the elements we clone. Will optimize later if needed
            XDocument docOut = new XDocument (doc);

            Walk (docOut.Root, this, null);
            return docOut;
        }

        protected void Walk (XElement doc, NcXmlFilter filter, NcXmlFilterNode parentFilterNode)
        {
            NcXmlFilter newFilter = null;
            NcXmlFilterNode newFilterNode = null;
            FilterNode (doc, filter, parentFilterNode, out newFilter, out newFilterNode);
            for (XNode docNode = doc.FirstNode; null != docNode; docNode = docNode.NextNode) {
                if (System.Xml.XmlNodeType.Element != docNode.NodeType) {
                    continue;
                }
                Walk ((XElement)docNode, newFilter, newFilterNode);
            }
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
            filter.ParentSet = this;
        }
    }
}


