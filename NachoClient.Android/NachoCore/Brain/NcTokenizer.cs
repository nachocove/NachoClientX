//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        protected List<string> _Sentences { get; set; }

        public List<string> Sentences {
            get {
                if (null == _Sentences) {
                    ExtractSentences ();
                }
                return _Sentences;
            }
        }

        protected List<string> _Keyphrases { get; set; }

        public List<string> Keyphrases {
            get {
                if (null == _Keyphrases) {
                    ExtractKeyphrases ();
                }
                return _Keyphrases;
            }
        }

        public NcTokenizer ()
        {
        }

        protected virtual void ExtractContent ()
        {
            _Content = "";
        }

        protected virtual void ExtractWords ()
        {
            _Words = new List<string> ();
        }

        protected virtual void ExtractSentences ()
        {
            _Sentences = new List<string> ();
        }

        protected virtual void ExtractKeywords ()
        {
            _Keywords = new List<string> ();
        }

        protected virtual void ExtractKeyphrases ()
        {
            _Keyphrases = new List<string> ();
        }

        // Helper functions for derived classes
        public List<string> WordsFromString (string s)
        {
            var words = new List<string> ();
            using (var contentReader = new StringReader (s)) {
                using (var tokenizer = new StandardTokenizer (LuceneVersion.LUCENE_30, contentReader)) {
                    while (tokenizer.IncrementToken ()) {
                        var word = tokenizer.GetAttribute<Lucene.Net.Analysis.Token> ().ToString ();
                        words.Add (word);
                    }
                }
            }
            return words;
        }

        public List<string> SentencesFromString (string s)
        {
            var sentences = new List<string> ();
            return sentences;
        }

        public bool IsAllUpperCase (string word)
        {
            return word.All (Char.IsUpper);
        }
    }

    public class NcMimeTokenizer : NcTokenizer
    {
        public delegate void HtmlWalker (HtmlNode node);

        private List<TextPart> Parts;

        public NcMimeTokenizer (MimeMessage message)
        {
            Parts = new List<TextPart> ();

            // Extract content from MIME messages. The rules are:
            // 1. For each multipart/mixed, iterate each subpart and concatenate the result.
            // 2. For each multipart/alternative, pick the most suitable subpart
            // 3. For text/html, use HAP to parse HTML, pick up all inner text.
            // 4. For text/plain, use the text part as is.
        }

        protected override void ExtractWords ()
        {
            _Words = new List<string> ();
            _Keywords = new List<string> ();

            foreach (TextPart part in Parts) {
                if (part.ContentType.Matches ("text", "plain")) {
                    ExtractWordsFromPlainText (part);
                } else if (part.ContentType.Matches ("text", "html")) {
                    ExtractWordsFromHtml (part);
                }
            }
        }

        protected override void ExtractKeywords ()
        {
            ExtractWords (); // words and keywords are extracted simultaneously
        }

        private void ExtractWordsFromPlainText (TextPart part)
        {
            var words = WordsFromString (part.Text);
            foreach (var word in words) {
                if (IsAllUpperCase (word)) {
                    _Keywords.Add (word);
                } else {
                    _Words.Add (word);
                }
            }
        }

        private void ExtractWordsFromHtml (TextPart part)
        {
            HtmlDocument html = new HtmlDocument ();
            html.LoadHtml (part.Text);
            WalkHtmlNode (html.DocumentNode, (HtmlNode node) => {
                NcAssert.True (HtmlNodeType.Text == node.NodeType);
                NcAssert.True (null != node.ParentNode);

                var words = WordsFromString (node.InnerText);
                List<string> destination = null;

                switch (node.ParentNode.Name) {
                case "b":
                    destination = _Keywords;
                    break;
                case "i":
                    destination = _Keywords;
                    break;
                case "u":
                    destination = _Keywords;
                    break;
                case "li":
                    destination = _Keywords;
                    break;
                default:
                    destination = _Words;
                    break;
                }
                destination.AddRange (words);
            });
        }

        protected override void ExtractSentences ()
        {
            // Lucene 3.0.3 does not have a sentence analyzer. So, we have to write our own
            // A sentence is simple a list of character separated by ". ". This works for most
            // European based language. For Chinese / Japanese, will need to add the circle.
            _Sentences = new List<string> ();
            _Keyphrases = new List<string> ();

            foreach (TextPart part in Parts) {
                if (part.ContentType.Matches ("text", "plain")) {
                    ExtractSentencesFromPlainText (part);
                } else if (part.ContentType.Matches ("text", "html")) {
                    ExtractSentencesFromHtml (part);
                }
            }
        }

        protected override void ExtractKeyphrases ()
        {
            ExtractSentences ();
        }

        private void ExtractSentencesFromPlainText (TextPart part)
        {
            var sentences = SentencesFromString (part.Text);
            foreach (var sentence in sentences) {
                if (IsAllUpperCase (sentence)) {
                    _Sentences.Add (sentence);
                } else {
                    _Keyphrases.Add (sentence);
                }
            }
        }

        private void ExtractSentencesFromHtml (TextPart part)
        {
        }

        private void WalkHtmlNode (HtmlNode node, HtmlWalker walker)
        {
            if (HtmlNodeType.Text == node.NodeType) {
                walker (node);
            }
            foreach (var child in node.ChildNodes) {
                WalkHtmlNode (node, walker);
            }
        }
    }
}
