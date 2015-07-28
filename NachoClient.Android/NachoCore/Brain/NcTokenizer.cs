//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using MimeKit;
using Lucene.Net.Analysis.Standard;
using LuceneVersion = Lucene.Net.Util.Version;
using Lucene.Net.Util;
using HtmlAgilityPack;

using NachoCore.Utils;

namespace NachoCore.Brain
{
    public class NcTokenizer
    {
        public delegate void HtmlWalker (HtmlNode node);

        // All these fields use lazy initialization pattern because we do not necessarily
        // do all the processing (indexing and all analyzers in one shot). So,
        // we only extract whatever as they are requested.
        protected string _Content { get; set; }

        public string Content {
            get {
                if (null == _Content) {
                    ExtractContent ();
                }
                return _Content;
            }
        }

        protected List<string> _Words { get; set; }

        public List<string> WordS {
            get { 
                if (null == _Words) {
                    ExtractWords ();
                }
                return _Words;
            }
        }

        protected List<string> _Keywords { get; set; }

        public List<string> Keywords {
            get {
                if (null == _Keywords) {
                    ExtractKeywords ();
                }
                return _Keywords;
            }
        }

        public CancellationToken? Token { get; set; }

        public NcTokenizer ()
        {
        }

        public NcTokenizer (CancellationToken? token)
        {
            Token = token;
        }

        protected virtual void ExtractContent ()
        {
            _Content = "";
        }

        protected virtual void ExtractWords ()
        {
            _Words = WordsFromString (Content);
        }

        protected virtual void ExtractKeywords ()
        {
            _Keywords = new List<string> ();
        }

        protected void MayCancel ()
        {
            if (Token.HasValue) {
                Token.Value.ThrowIfCancellationRequested ();
            }
        }

        // Helper functions for derived classes
        public List<string> WordsFromString (string s)
        {
            var words = new List<string> ();
            using (var contentReader = new StringReader (s)) {
                using (var tokenizer = new StandardTokenizer (LuceneVersion.LUCENE_30, contentReader)) {
                    while (tokenizer.IncrementToken ()) {
                        MayCancel ();
                        var word = tokenizer.GetAttribute<Lucene.Net.Analysis.Tokenattributes.ITermAttribute> ().Term;
                        words.Add (word);
                    }
                }
            }
            return words;
        }

        public bool IsAllUpperCase (string word)
        {
            return word.All (Char.IsUpper);
        }

        public NcTokenizer Create (MimeMessage mimeMessage)
        {
            return new NcMimeTokenizer (mimeMessage, NcTask.Cts.Token);
        }

        protected void ExtractContentFromPlainText (string plainText)
        {
            try {
                var words = WordsFromString (plainText);
                _Content += plainText + ". ";
                foreach (var word in words) {
                    MayCancel ();
                    if (IsAllUpperCase (word)) {
                        _Keywords.Add (word);
                    }
                }
            } catch (Exception e) {
                Log.Error (Log.LOG_BRAIN, "fail to parse text (exception={0})", e.Message);
            }
        }

        protected bool IsEmphasisHtmlTag (string nodeName)
        {
            return ("b" == nodeName) || ("i" == nodeName) || ("u" == nodeName) || ("li" == nodeName);
        }

        protected void WalkHtmlNodes (HtmlNode node, HtmlWalker walker)
        {
            if (HtmlNodeType.Text == node.NodeType) {
                walker (node);
            }
            foreach (var child in node.ChildNodes) {
                if (("style" == child.Name) || ("script" == child.Name)) {
                    continue; // skip all css and javascript
                }
                WalkHtmlNodes (child, walker);
            }
        }

        protected void ExtractContentFromHtml (string rawHtml)
        {
            try {
                HtmlDocument html = new HtmlDocument ();
                html.LoadHtml (rawHtml);
                WalkHtmlNodes (html.DocumentNode, (HtmlNode node) => {
                    MayCancel ();
                    _Content += node.InnerText + ". ";
                    var words = WordsFromString (node.InnerText);

                    if (IsEmphasisHtmlTag (node.ParentNode.Name)) {
                        _Keywords.AddRange (words);
                    }
                });
            } catch (Exception e) {
                Log.Error (Log.LOG_BRAIN, "fail to parse HTML (execption={0})", e.Message);
            }
        }
    }

    public class NcPlainTextTokenizer : NcTokenizer
    {
        protected string Text;

        public NcPlainTextTokenizer (string text, CancellationToken token) : base (token)
        {
            _Keywords = new List<string> ();
            Text = text;
        }

        protected override void ExtractContent ()
        {
            ExtractContentFromPlainText (Text);
        }
    }

    public class NcHtmlTokenizer : NcTokenizer
    {
        protected string Html;

        public NcHtmlTokenizer (string html, CancellationToken token) : base (token)
        {
            _Keywords = new List<string> ();
            Html = html;
        }

        protected override void ExtractContent ()
        {
            ExtractContentFromHtml (Html);
        }
    }

    public class NcMimeTokenizer : NcTokenizer
    {
        const int MaxAttachmentSize = 1 * 1000 * 1000;

        public List<TextPart> Parts { get; protected set; }

        protected List<TextPart> ProcessMimeEntity (MimeEntity part)
        {
            MayCancel ();
            if (null == part) {
                return new List<TextPart> ();
            }
            if (part is MessagePart) {
                var message = (MessagePart)part;
                return ProcessMimeEntity (message.Message.Body);
            }
            if (part is Multipart) {
                var multipart = (Multipart)part;
                if (multipart.ContentType.Matches ("multipart", "alternative")) {
                    return ProcessAlternativeMultipart (multipart);
                } else if (multipart.ContentType.Matches ("multipart", "mixed")) {
                    return ProcessMixedMultipart (multipart);
                } else if (multipart.ContentType.Matches ("multipart", "related")) {
                    // The handling of multipart/related is the same as multipart/mixed.
                    // Just iterate through all its subparts and add the ones that can be
                    // indexed.
                    return ProcessMixedMultipart (multipart);
                } else {
                    // Unsupported multipart
                    Log.Warn (Log.LOG_BRAIN, "ProcessMimeEntity: unsupported multipart type {0}/{1}",
                        multipart.ContentType.MediaType, multipart.ContentType.MediaSubtype);
                }
                return new List<TextPart> ();
            }
            return ProcessMimePart ((MimePart)part);
        }

        protected List<TextPart> ProcessAlternativeMultipart (Multipart multipart)
        {
            // We start from the last part and iterate backward until we find something that works
            for (int n = multipart.Count - 1; n >= 0; n--) {
                var parts = ProcessMimeEntity (multipart [n]);
                if ((null != parts) && (0 < parts.Count)) {
                    return parts;
                }
            }
            Log.Warn (Log.LOG_BRAIN, "no suitable alternative part ({0} parts total)", multipart.Count);
            return new List<TextPart> ();
        }

        private List<TextPart> ProcessMixedMultipart (Multipart multipart)
        {
            var parts = new List<TextPart> ();
            foreach (var subpart in multipart) {
                parts.AddRange (ProcessMimeEntity (subpart));
            }
            return parts;
        }

        private List<TextPart> ProcessMimePart (MimePart part)
        {
            var parts = new List<TextPart> ();
            if ("text" == part.ContentType.MediaType) {
                if (("plain" == part.ContentType.MediaSubtype) ||
                    ("html" == part.ContentType.MediaSubtype)) {
                    TextPart body = (TextPart)part;
                    parts.Add (body);
                }
            }
            return parts;
        }

        public NcMimeTokenizer (MimeMessage message, CancellationToken? token) : base (token)
        {
            // Extract content from MIME messages. The rules are:
            // 1. For each multipart/mixed, iterate each subpart and concatenate the result.
            // 2. For each multipart/alternative, pick the most suitable subpart
            // 3. For text/html, use HAP to parse HTML, pick up all inner text.
            // 4. For text/plain, use the text part as is.
            Parts = ProcessMimeEntity (message.Body);
        }

        protected override void ExtractContent ()
        {
            _Content = "";
            _Keywords = new List<string> ();
            foreach (TextPart part in Parts) {
                if (part.IsAttachment && (MaxAttachmentSize < part.Text.Length)) {
                    continue;
                }
                if (part.IsPlain) {
                    ExtractContentFromPlainText (part.Text);
                } else if (part.IsHtml) {
                    ExtractContentFromHtml (part.Text);
                }
            }
        }
    }
}
