//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using HtmlAgilityPack;

namespace NachoCore.Utils
{

    public static class HtmlDocumentExtensions
    {

        public static string TextContents (this HtmlDocument doc)
        {
            var serializer = new HtmlTextSerializer (doc);
            return serializer.Serialize ();
        }

        public static HtmlTextNode CreateTextNodeWithEscaping (this HtmlDocument doc, string text)
        {
            // HtmlAgilityPack doesn't do any escaping of the text like a fully-featured DOM implementation would.
            // So this is a convenience function to do it.
            var escaped = text.Replace ("&", "&amp;").Replace ("<", "&lt;");
            var node = doc.CreateTextNode (escaped);
            return node;
        }

        public static HtmlDocument CopyUntilNode (this HtmlDocument doc, HtmlNode targetNode)
        {
            var copyDoc = new HtmlDocument ();
            var node = doc.DocumentNode;
            var stack = new List<HtmlNode> (node.ChildNodes);
            var copyParentStack = new List<HtmlNode> ();
            copyParentStack.Add (copyDoc.DocumentNode);
            var childCountStack = new List<int> ();
            childCountStack.Add (node.ChildNodes.Count);
            HtmlNode copy;
            while (stack.Count > 0) {
                node = stack [0];
                stack.RemoveAt (0);
                if (node.NodeType == HtmlNodeType.Element) {
                    copy = copyDoc.CreateElement (node.OriginalName);
                    foreach (var attr in node.Attributes) {
                        copy.SetAttributeValue (attr.OriginalName, attr.Value);
                    }
                } else if (node.NodeType == HtmlNodeType.Text) {
                    copy = copyDoc.CreateTextNode ((node as HtmlTextNode).Text);
                } else {
                    copy = null;
                }
                if (copy != null) {
                    copyParentStack [copyParentStack.Count - 1].AppendChild (copy);
                }
                childCountStack [childCountStack.Count - 1] -= 1;
                if (childCountStack [childCountStack.Count - 1] == 0) {
                    copyParentStack.RemoveAt (copyParentStack.Count - 1);
                    childCountStack.RemoveAt (childCountStack.Count - 1);
                }
                if (node.NodeType == HtmlNodeType.Element && node.ChildNodes.Count > 0) {
                    copyParentStack.Add (copy);
                    childCountStack.Add (node.ChildNodes.Count);
                    var index = 0;
                    foreach (var child in node.ChildNodes) {
                        stack.Insert (index, child);
                        index += 1;
                    }
                }
                if (System.Object.ReferenceEquals (node, targetNode)) {
                    break;
                }
            }
            return copyDoc;
        }

    }

    public class HtmlTextSerializer {

        #region Properties

        class HtmlState {

            public HtmlState Parent;
            public string LinePrefix = "";
            public string FirstLinePrefix = null;
            public bool IsStarted = false;
            public ListCounter Counter = null;

            public HtmlState Copy ()
            {
                var state = new HtmlState ();
                state.Parent = this;
                state.LinePrefix = LinePrefix;
                state.FirstLinePrefix = FirstLinePrefix;
                state.IsStarted = false;
                state.Counter = Counter;
                return state;
            }

            public void ResetCounter ()
            {
                if (Counter == null) {
                    Counter = new ListCounter ();
                } else {
                    Counter.Index = 0;
                }
            }

        }

        class ListCounter {

            public int Index = 0;

        }

        HtmlNode Node;
        bool AtLineStart = true;
        bool AtDocumentStart = true;
        bool AtParagraphBreak = true;
        bool AtStatePush = true;
        string Text;
        public string TopText;
        string Line;
        HtmlState State;
        public HtmlNode LastTopTextNode;
        HtmlNode LastTextNode;
        public bool FoundTop;
        bool NeedsTopCheck;

        #endregion

        #region Constructors

        public HtmlTextSerializer (string html)
        {
            var document = new HtmlDocument ();
            document.LoadHtml (html);
            Node = document.DocumentNode;
            CommonInit ();

        }

        public HtmlTextSerializer (HtmlDocument document)
        {
            Node = document.DocumentNode;
            CommonInit ();
        }

        public HtmlTextSerializer (HtmlNode node)
        {
            Node = node;
            CommonInit ();
        }

        void CommonInit ()
        {
            PushState ();
            Text = "";
            TopText = "";
            Line = "";
        }

        #endregion

        #region Public Interface

        public string Serialize ()
        {
            Line = "";
            Text = "";
            TopText = "";
            LastTextNode = null;
            LastTopTextNode = null;
            FoundTop = false;
            NeedsTopCheck = false;
            VisitNode (Node);
            return Text;
        }

        #endregion

        #region State Stack Management

        private void PushState ()
        {
            if (State == null) {
                State = new HtmlState ();
                State.Parent = State;
            } else {
                State = State.Copy ();
            }
            AtStatePush = true;
        }

        private void PopState (){
            State.Parent.IsStarted = State.Parent.IsStarted || State.IsStarted;
            State = State.Parent;
            AtStatePush = false;
        }

        #endregion

        #region Top Splitting

        void CheckLastLineForTopSplit ()
        {
            if (!FoundTop && NeedsTopCheck) {
                if (EmailHelper.IsQuoteLine(Line)) {
                    SetTop (true);
                }
            }
            if (!FoundTop) {
                LastTopTextNode = LastTextNode;
            }
        }

        void SetTop (bool stripLastLine = false)
        {
            if (!FoundTop) {
                FoundTop = true;
                if (stripLastLine) {
                    var index = Text.Length - Line.Length;
                    if (index >= 0) {
                        TopText = Text.Substring (0, index).Trim ();
                    } else {
                        TopText = "";
                    }
                } else {
                    TopText = Text.Trim ();
                }
            }
        }

        #endregion

        #region Html Traversal

        private void VisitNode (HtmlNode node)
        {
            if (node.NodeType == HtmlNodeType.Document) {
                VisitDocument (node);
            } else if (node.NodeType == HtmlNodeType.Element) {
                VisitElement (node);
            } else if (node.NodeType == HtmlNodeType.Text) {
                VisitText (node as HtmlTextNode);
            }
        }

        private void VisitDocument (HtmlNode node)
        {
            VisitChildren (node);
        }

        private void VisitElement (HtmlNode node)
        {
            var displayStyle = GetElementDisplayStyle (node);
            if (displayStyle.Equals ("none")) {
                return;
            }
            var methodName = "Visit_" + node.Name.ToUpper ();
            var methodInfo = this.GetType ().GetMethod (methodName);
            if (methodInfo != null) {
                methodInfo.Invoke(this, new object[]{node});
            } else {
                var isBlock = displayStyle.Equals ("block");
                if (isBlock) {
                    VisitBlockElement (node);
                } else {
                    VisitInlineElement (node);
                }
            }
        }

        public void Visit_BR (HtmlNode node){
            CheckLastLineForTopSplit ();
            if (AtLineStart) {
                OutputText ("");
            }
            AtLineStart = true;
        }

        public void Visit_HR (HtmlNode Node)
        {
            SetTop ();
            AtLineStart = true;
            OutputText ("----------------");
            AtLineStart = true;
        }

        public void Visit_BLOCKQUOTE (HtmlNode node)
        {
            SetTop ();
            PushState ();
            State.LinePrefix = State.LinePrefix.TrimEnd () + "> ";
            VisitParagraphElement (node, true);
            PopState ();
        }

        public void Visit_P (HtmlNode node)
        {
            VisitParagraphElement (node);
        }

        public void Visit_PRE (HtmlNode node)
        {
            VisitParagraphElement (node);
        }

        public void Visit_FIGURE (HtmlNode node)
        {
            VisitParagraphElement (node);
        }

        public void Visit_LISTING (HtmlNode node)
        {
            VisitParagraphElement (node);
        }

        public void Visit_PLAINTEXT (HtmlNode node)
        {
            VisitParagraphElement (node);
        }

        public void Visit_XMP (HtmlNode node)
        {
            VisitParagraphElement (node);
        }

        public void Visit_H1 (HtmlNode node)
        {
            VisitParagraphElement (node);
        }

        public void Visit_H2 (HtmlNode node)
        {
            VisitParagraphElement (node);
        }

        public void Visit_H3 (HtmlNode node)
        {
            VisitParagraphElement (node);
        }

        public void Visit_H4 (HtmlNode node)
        {
            VisitParagraphElement (node);
        }

        public void Visit_H5 (HtmlNode node)
        {
            VisitParagraphElement (node);
        }

        public void Visit_H6 (HtmlNode node)
        {
            VisitParagraphElement (node);
        }

        public void Visit_UL (HtmlNode node)
        {
            PushState ();
            State.Counter = null;
            VisitBlockElement (node);
            PopState ();
        }

        public void Visit_OL (HtmlNode node)
        {
            PushState ();
            State.ResetCounter ();
            VisitBlockElement (node);
            PopState ();
        }

        public void Visit_LI (HtmlNode node)
        {
            var itemPrefix = "* ";
            if (State.Counter != null) {
                itemPrefix = String.Format ("{0}. ", State.Counter.Index + 1);
                State.Counter.Index += 1;
            }
            PushState ();
            State.Counter = null;
            var baseLinePrefix = String.Copy (State.LinePrefix);
            State.FirstLinePrefix = baseLinePrefix + itemPrefix;
            State.LinePrefix = baseLinePrefix;
            for (int i = 0; i < itemPrefix.Length; ++i) {
                State.LinePrefix += " ";
            }
            VisitBlockElement (node);
            if (!State.IsStarted) {
                OutputText ("");
            }
            PopState ();
        }

        public void Visit_TABLE (HtmlNode node)
        {
            VisitParagraphElement (node);
        }

        private void VisitParagraphElement (HtmlNode node, bool statePushedByCaller = false)
        {
            if (!statePushedByCaller) {
                PushState ();
            }
            bool wasAtParagraphBreak = AtParagraphBreak;
            AtParagraphBreak = true;
            VisitBlockElement (node);
            AtParagraphBreak = State.IsStarted || wasAtParagraphBreak;
            if (!statePushedByCaller) {
                PopState ();
            }
        }

        private void VisitBlockElement (HtmlNode node)
        {
            CheckLastLineForTopSplit ();
            AtLineStart = true;
            VisitChildren (node);
            AtLineStart = true;
        }

        private void VisitInlineElement (HtmlNode node)
        {
            VisitChildren (node);
        }

        private void VisitChildren (HtmlNode node)
        {
            foreach (var child in node.ChildNodes) {
                VisitNode (child);
            }
        }

        private void VisitText (HtmlTextNode node)
        {
            // first let's remove consecutive whitespace because it's not meaningful in HTML
            var consecutiveWhitespace = new Regex (@"[ \f\n\r\t\v]+");
            var normalized = consecutiveWhitespace.Replace (node.Text, " ");
            // Then let's convert entities to their text values
            normalized = HtmlEntity.DeEntitize (normalized);
            // Finally, since some entities may have been whitespace like &nbsp;, let's do another
            // pass and standardize all whitespace as regular spaces.  That way the resulting text doesn't
            // have any special kind of spaces, and the plaintext spacing is still as-indented
            var anyWhitespace = new Regex (@"\s");
            normalized = anyWhitespace.Replace (normalized, " ");
            if (normalized.Length > 0) {
                if (AtLineStart) {
                    normalized = normalized.TrimStart ();
                }
                if (normalized.Length > 0) {
                    LastTextNode = node;
                    OutputText (normalized);
                }
            }
        }

        #endregion

        #region Helpers

        private void OutputText (string text)
        {
            if (!AtDocumentStart) {
                if (AtLineStart) {
                    if (AtParagraphBreak) {
                        if (AtStatePush) {
                            Line = State.Parent.LinePrefix;
                            Text += "\n" + State.Parent.LinePrefix;
                        } else {
                            Line = State.LinePrefix;
                            Text += "\n" + State.LinePrefix;
                        }
                    }
                    if (State.IsStarted || State.FirstLinePrefix == null) {
                        Line = State.LinePrefix;
                        Text += "\n" + State.LinePrefix;
                    } else {
                        Line = State.FirstLinePrefix;
                        Text += "\n" + State.FirstLinePrefix;
                    }
                }
            } else {
                AtDocumentStart = false;
            }
            Line += text;
            Text += text;
            AtLineStart = false;
            AtParagraphBreak = false;
            AtStatePush = false;
            State.IsStarted = true;
            NeedsTopCheck = true;
        }

        private string GetElementDisplayStyle (HtmlNode node)
        {
            // There are a few tags that can never be displayed
            if (node.Name.Equals ("head")) {
                return "none";
            }
            if (node.Name.Equals ("input")) {
                var types = node.Attributes.AttributesWithName ("type");
                foreach (var attr in types) {
                    if (attr.Value.ToLower().Trim().Equals ("hidden")) {
                        return "none";
                    }
                }
            }
            // Special cases aside, we'll look up the default display style based on element name
            string defaultDisplay;
            ElementDefaultDisplayStyles.TryGetValue (node.Name, out defaultDisplay);
            if (defaultDisplay == null) {
                defaultDisplay = "inline";
            }
            // TODO: check style attribute for display override
            return defaultDisplay;
        }

        #endregion

        #region HTML Tables

        private Dictionary<string, string> ElementDefaultDisplayStyles = new Dictionary<string, string> {

            // 10.3.1 Hidden elements
            { "area", "none" },
            { "base", "none" },
            { "basefont", "none" },
            { "datalist", "none" },
            { "head", "none" },
            { "link", "none" },
            { "meta", "none" },
            { "noembed", "none" },
            { "noframes", "none" },
            { "param", "none" },
            { "rp", "none" },
            { "script", "none" },
            { "source", "none" },
            { "style", "none" },
            { "template", "none" },
            { "track", "none" },
            { "title", "none" },

            // 10.3.2 The page
            {"html", "block"},
            {"body", "block"},

            // 10.3.3 Flow content
            {"address", "block"},
            {"blockquote", "block"},
            {"center", "block"},
            {"div", "block"},
            {"figure", "block"},
            {"figcaption", "block"},
            {"footer", "block"},
            {"form,", "block"},
            {"header", "block"},
            {"hr", "block"},
            {"legend", "block"},
            {"listing", "block"},
            {"p", "block"},
            {"plaintext", "block"},
            {"pre", "block"},
            {"xmp", "block"},

            // 10.3.7 Sections and headings
            {"article", "block"},
            {"aside", "block"},
            {"h1", "block"},
            {"h2", "block"},
            {"h3", "block"},
            {"h4", "block"},
            {"h5", "block"},
            {"h6", "block"},
            {"hgroup", "block"},
            {"nav", "block"},
            {"section", "block"},

            // 10.3.8 Lists
            {"dir", "block"},
            {"dd", "block"},
            {"dl", "block"},
            {"dt", "block"},
            {"ol", "block"},
            {"ul", "block"},
            {"li", "block"}, // really should be list-item

            // 10.3.9 Tables
            // For our purposes converting to text, we can consider tables to be blocks
            {"table", "block"},
            {"caption", "block"},
            {"colgroup", "none"},
            {"col", "none"},
            {"thead", "block"},
            {"tfoot", "block"},
            {"tbody", "block"},
            {"tr", "block"},
            {"th", "block"},
            {"td", "block"},

            {"fieldset", "block"}
        };

        #endregion
    }
        
    public class HtmlTextDeserializer {

        HtmlDocument Document;
        HtmlNode Node;
        StringReader Reader;
        string LinePrefix = "";

        public HtmlTextDeserializer ()
        {
        }

        public HtmlDocument Deserialize (string text)
        {
            var document = new HtmlDocument ();
            var html = document.CreateElement ("html");
            var head = document.CreateElement ("head");
            var charset = document.CreateElement ("meta");
            charset.SetAttributeValue ("charset", "utf8");
            head.AppendChild (charset);
            html.AppendChild (head);
            var body = document.CreateElement ("body");
            html.AppendChild (body);
            document.DocumentNode.AppendChild (html);

            DeserializeInto (text, body);

            return document;
        }

        public void DeserializeInto (string text, HtmlNode node)
        {
            if (String.IsNullOrEmpty (text)) {
                return;
            }
            
            LinePrefix = "";

            Document = node.OwnerDocument;
            Node = node;

            Reader = new StringReader (text);
            var readMore = true;
            while (readMore) {
                readMore = ReadLine ();
            }
        }

        public void PushNode (string name)
        {
            var node = Document.CreateElement (name);
            Node.AppendChild (node);
            Node = node;
        }

        public void PopNode ()
        {
            Node = Node.ParentNode;
        }

        private bool ReadLine ()
        {
            var line = Reader.ReadLine ();
            if (line != null) {
                if (!String.IsNullOrEmpty (LinePrefix)) {
                    if (!line.StartsWith (LinePrefix)) {
                        while (!line.StartsWith (LinePrefix) && LinePrefix.Length > 0) {
                            PopNode ();
                            LinePrefix = LinePrefix.Substring (0, LinePrefix.Length - 1);
                        }
                        if (!String.IsNullOrEmpty (LinePrefix)) {
                            line = line.Substring (LinePrefix.Length);
                        }
                    } else {
                        line = line.Substring (LinePrefix.Length);
                    }
                }
                while (line.StartsWith (">")) {
                    PushNode ("blockquote");
                    Node.SetAttributeValue ("type", "cite");
                    line = line.Substring (1);
                    LinePrefix += ">";
                }
                if (!String.IsNullOrEmpty(LinePrefix)) {
                    line = line.TrimStart ();
                }
                PushNode ("div");
                ConsumeText (line);
                if (String.IsNullOrWhiteSpace (line)) {
                    Node.AppendChild (Document.CreateElement ("br"));
                }
                PopNode ();
                return true;
            }
            return false;
        }

        private void ConsumeText (string text)
        {
            Node.AppendChild (Document.CreateTextNodeWithEscaping (text));
        }

    }
}

