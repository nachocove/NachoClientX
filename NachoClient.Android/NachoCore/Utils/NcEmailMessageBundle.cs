//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;
using MimeKit;
using NachoCore.Utils;
using NachoCore.Model;
using HtmlAgilityPack;
using NachoPlatform;
using System.Linq;

namespace NachoCore.Utils
{

    public class NcEmailMessageBundle : MimeVisitor
    {

        #region Property Classes

        public class BundleManifest : NcBundleStorageSerializable
        {
            public class Entry
            {
                public string Path { get; set; }
                public string ContentType { get; set; }

                public Entry ()
                {
                }
            }
                
            public int Version { get; set; }
            public Dictionary<string, Entry> Entries { get; set; }

            public BundleManifest ()
            {
                Version = 0;
                Entries = new Dictionary<string, Entry> ();
            }

            public BundleManifest (int version)
            {
                Version = version;
                Entries = new Dictionary<string, Entry> ();
            }

            public byte[] SerializeForBundleStorage ()
            {
                var lines = new List<string> ();
                lines.Add ("Version");
                lines.Add (Version.ToString ());
                lines.Add ("Entries");
                lines.Add (Entries.Count.ToString ());
                foreach (var k in Entries.Keys) {
                    var entry = Entries [k];
                    if (entry.Path != null) {
                        lines.Add ("Path");
                        lines.Add (entry.Path);
                    }
                    if (entry.ContentType != null) {
                        lines.Add ("ContentType");
                        lines.Add (entry.ContentType);
                    }
                    lines.Add ("key");
                    lines.Add (k);
                }
                var stringContents = String.Join ("\n", lines);
                var encoding = new System.Text.UTF8Encoding (false, false);
                var encodedContents = encoding.GetBytes (stringContents);
                return encodedContents;
            }

            public void DeserializeFromBundleStorage (byte[] contents)
            {
                var encoding = new System.Text.UTF8Encoding (false, false);
                var stringContents = encoding.GetString (contents);
                var lines = new List<string>(stringContents.Split ('\n'));
                var remainingEntries = 0;
                Entry entry = null;
                while (lines.Count > 0) {
                    var k = lines [0];
                    lines.RemoveAt (0);
                    if (lines.Count > 0) {
                        var v = lines [0];
                        lines.RemoveAt (0);
                        if (remainingEntries == 0) {
                            if (k == "Version") {
                                Version = Int32.Parse (v);
                            } else if (k == "Entries") {
                                remainingEntries = Int32.Parse (v);
                            }
                        } else {
                            if (entry == null) {
                                entry = new Entry ();
                            }
                            if (k == "key") {
                                Entries [v] = entry;
                                remainingEntries -= 1;
                                entry = null;
                            } else if (k == "Path") {
                                entry.Path = v;
                            } else if (k == "ContentType") {
                                entry.ContentType = v;
                            }
                        }
                    }
                }

            }

        }

        public class MemberInfo {

            public readonly string Filename;
            public readonly string ContentType;
            public readonly BinaryReader Reader;

            public MemberInfo (string filename, string contentType, BinaryReader reader)
            {
                Filename = filename;
                ContentType = contentType;
                Reader = reader;
            }
        }

        private class ParseResult {
            public HtmlDocument FullHtmlDocument = null;
            public HtmlDocument TopHtmlDocument = null;
            public string FullText = null;
            public string TopText = null;
            public List<MultipartRelated> RelatedStack;
            public AlternativeTypes AlternateTypeInfo;
            private bool populateHtml = true;
            private bool populateText = true;
            public Dictionary<string, string> ImageEntriesBySrc;

            public ParseResult ()
            {
                RelatedStack = new List<MultipartRelated> ();
                ImageEntriesBySrc = new Dictionary<string, string> ();
            }

            public bool PopulateHtml {
                get {
                    if (AlternateTypeInfo != null) {
                        return AlternateTypeInfo.PopulatingHtml;
                    }
                    return populateHtml;
                }
                set {
                    populateHtml = value;
                }
            }

            public bool PopulateText {
                get {
                    if (AlternateTypeInfo != null) {
                        return AlternateTypeInfo.PopulatingText;
                    }
                    return populateText;
                }
                set {
                    populateText = value;
                }
            }

        }

        private class AlternativeTypes
        {
            public bool PopulatingHtml;
            public bool PopulatingText;
            public bool ConsiderRtfAsHtml;
        }

        #endregion

        #region Properties

        public bool NeedsUpdate = false;
        public IPlatformRtfConverter RtfConverter = null;
        private bool HasHtmlUrl = true;
        private McEmailMessage Message = null;
        private McBody Body = null;
        private MimeMessage MimeMessage = null;
        private NcBundleStorage Storage;
        private BundleManifest Manifest;
        private int SubmessageCount = 0;

        private static int LastestVersion = 4;

        private static string FullTextEntryName = "full-text";
        private static string TopTextEntryName = "top-text";
        private static string FullHtmlEntryName = "full-html";
        private static string TopHtmlEntryName = "top-html";
        private static string FullLightlyStyledEntryName = "full-simple";
        private static string TopLightlyStyledEntryName = "top-simple";

        private static string ManifestPath = "manifest.nacho";
        private static string FullTextPath = "full.txt";
        private static string TopTextPath = "top.txt";
        private static string FullHtmlPath = "full.html";
        private static string TopHtmlPath = "top.html";
        private static string FullLightlyStyledPath = "full.rtf";
        private static string TopLightlyStyledPath = "top.rtf";

        ParseResult parsed;

        private static object UpdateLock = new object ();

        #endregion

        public static string FileStoragePathForBodyId (int accountId, int bodyId)
        {
            var dataRoot = NcApplication.GetDataDirPath ();
            return Path.Combine (dataRoot, "files", accountId.ToString(), "bundles", bodyId.ToString ());
        }

        #region Constructors

        public NcEmailMessageBundle (McEmailMessage message)
        {
            NcAssert.True (message.BodyId != 0);
            var bundleRoot = FileStoragePathForBodyId (message.AccountId, message.BodyId);
            Storage = new NcBundleFileStorage (bundleRoot);
            HasHtmlUrl = true;
            Message = message;
            ReadManifest ();
        }

        public NcEmailMessageBundle (McBody body)
        {
            var bundleRoot = FileStoragePathForBodyId (body.AccountId, body.Id);
            Storage = new NcBundleFileStorage (bundleRoot);
            HasHtmlUrl = true;
            Body = body;
            ReadManifest ();
        }

        public NcEmailMessageBundle (MimeMessage message, string storagePath = null)
        {
            if (storagePath == null) {
                Storage = new NcBundleMemoryStorage ();
                HasHtmlUrl = false;
            } else {
                Storage = new NcBundleFileStorage (storagePath);
                HasHtmlUrl = true;
            }
            MimeMessage = message;
            ReadManifest ();
        }

        void ReadManifest ()
        {
            Manifest = Storage.ObjectContentsForPath (ManifestPath, typeof(BundleManifest)) as BundleManifest;
            if (Manifest != null) {
                NeedsUpdate = Manifest.Version != LastestVersion;
            } else {
                Manifest = new BundleManifest (LastestVersion);
                NeedsUpdate = true;
            }
        }

        #endregion

        #region Public Content Properies

        public string TopText {
            get {
                return GetStringContentsOfManifestEntry (TopTextEntryName);
            }
        }

        public string FullText {
            get {
                return GetStringContentsOfManifestEntry (FullTextEntryName);
            }
        }

        public string TopLightlyStyledText {
            get {
                return GetStringContentsOfManifestEntry (TopLightlyStyledEntryName);
            }
        }

        public string FullLightlyStyledText {
            get {
                return GetStringContentsOfManifestEntry (FullLightlyStyledEntryName);
            }
        }

        public string TopHtml {
            get {
                return GetStringContentsOfManifestEntry (TopHtmlEntryName);
            }
        }

        public string FullHtml {
            get {
                return GetStringContentsOfManifestEntry (FullHtmlEntryName);
            }
        }

        public Uri TopHtmlUrl {
            get {
                if (HasHtmlUrl) {
                    return GetUrlOfManifestEntry (TopHtmlEntryName);
                }
                return null;
            }
        }

        public Uri FullHtmlUrl {
            get {
                if (HasHtmlUrl) {
                    return GetUrlOfManifestEntry (FullHtmlEntryName);
                }
                return null;
            }
        }

        public Uri BaseUrl {
            get {
                return Storage.BaseUrl ();
            }
        }

        public MemberInfo MemberForEntryName (string entryName)
        {
            var entry = Manifest.Entries [entryName];
            if (entry != null) {
                var reader = Storage.BinaryReaderForPath (entry.Path);
                return new MemberInfo (Path.GetFileName (entry.Path), entry.ContentType, reader);
            }
            return null;
        }

        #endregion

        #region Bundle/Storage Helpers

        private Uri GetUrlOfManifestEntry (string name)
        {
            if (Manifest != null && Manifest.Entries != null) {
                BundleManifest.Entry entry;
                if (Manifest.Entries.TryGetValue (name, out entry)) {
                    return Storage.UrlForPath (entry.Path, entry.ContentType);
                }
            }
            return null;
        }

        private string GetStringContentsOfManifestEntry (string name)
        {
            if (Manifest != null && Manifest.Entries != null) {
                BundleManifest.Entry entry;
                if (Manifest.Entries.TryGetValue (name, out entry)) {
                    string[] typeParts = entry.ContentType.Split ('/');
                    if (typeParts [0] == "text") {
                        return Storage.StringContentsForPath (entry.Path);
                    }
                    return null;
                }
            }
            return null;
        }

        private string SafeFilename (string unsafeFilename)
        {
            char[] invalid = { '/', '\\', ':', '\0' };
            string[] split = unsafeFilename.Split (invalid, StringSplitOptions.RemoveEmptyEntries);
            var basename = String.Join ("_", split);
            string filename = basename;
            bool exists = false;
            int i = 0;
            do {
                exists = false;
                if (i > 0){
                    filename = String.Format("{0}_{1}", basename, i);
                }
                foreach (var k in Manifest.Entries.Keys) {
                    if (Manifest.Entries[k].Path != null && Manifest.Entries [k].Path.Equals (filename)) {
                        exists = true;
                        break;
                    }
                }
                ++i;
            } while (exists);
            return filename;
        }

        protected HtmlDocument TemplateHtmlDocument ()
        {
            var documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            var htmlPath = Path.Combine (documentsPath, "nacho.html");
            var doc = new HtmlDocument ();
            using (var stream = new FileStream (htmlPath, FileMode.Open, FileAccess.Read)) {
                doc.Load (stream);
            }
            List<HtmlNode> nodes = new List<HtmlNode> ();
            nodes.Add (doc.DocumentNode);
            HtmlNode node;
            string resourcesScheme = "resources:";
            while (nodes.Count > 0) {
                node = nodes [0];
                nodes.RemoveAt (0);
                foreach (var attr in node.Attributes) {
                    if (attr.Value.StartsWith (resourcesScheme)) {
                        var resourceName = attr.Value.Substring (resourcesScheme.Length);
                        attr.Value = Storage.RelativeUrlForDocumentsPath (resourceName).ToString();
                    }
                }
                foreach (var child in node.ChildNodes) {
                    nodes.Add (child);
                }
            }
            return doc;
        }

        #endregion

        #region Populate/Update Bundle

        public void Update ()
        {
            lock (UpdateLock) {
                ReadManifest ();
                if (NeedsUpdate) {
                    ParseMessage ();
                    CompleteBundleAfterParse ();
                }
            }
        }

        public void Invalidate ()
        {
            Storage.Delete ();
            ReadManifest ();
        }

        public void SetFullText (string text)
        {
            SetParsed (fullText: text);
        }

        public void SetFullHtml (HtmlDocument doc, NcEmailMessageBundle sourceBundle)
        {
            parsed = new ParseResult ();
            parsed.FullHtmlDocument = doc;
            ResolveRelativeReferences (doc, sourceBundle);
            CompleteBundleAfterParse ();
        }

        public void SetParsed (HtmlDocument fullHtml = null, string fullText = null, HtmlDocument topHtml = null, string topText = null)
        {
            parsed = new ParseResult ();
            parsed.FullHtmlDocument = fullHtml;
            parsed.FullText = fullText;
            parsed.TopText = topText;
            parsed.TopHtmlDocument = topHtml;
            CompleteBundleAfterParse ();
        }

        private void CompleteBundleAfterParse ()
        {
            FillMissing ();
            StoreFullEntries ();
            StoreTopEntries ();
            StoreLightlyStyledEntries ();
            StoreManifest ();
            NeedsUpdate = false;
        }

        private void ParseMessage ()
        {
            RtfConverter = new NachoPlatform.RtfConverter ();
            parsed = new ParseResult ();

            if (Message != null) {
                Body = Message.GetBody ();
            }
            if (Body != null) {
                try {
                    if (Body.BodyType == McAbstrFileDesc.BodyTypeEnum.PlainText_1) {
                        parsed.FullText = Body.GetContentsString ();
                    } else if (Body.BodyType == McAbstrFileDesc.BodyTypeEnum.HTML_2) {
                        parsed.FullHtmlDocument = TemplateHtmlDocument ();
                        IncludeHtml (Body.GetContentsString ());
                    } else if (Body.BodyType == McAbstrFileDesc.BodyTypeEnum.MIME_4) {
                        MimeMessage = MimeMessage.Load (Body.GetFilePath ());
                    } else {
                        parsed.FullText = Body.GetContentsString ();
                    }
                } catch (IOException e) {
                    // This can happen if the message is deleted while this background task is in progress.
                    Log.Error (Log.LOG_UI, "I/O exception while parsing the body for an e-mail message or calendar item: {0}", e.Message);
                    parsed.FullText = "";
                }
            }

            if (MimeMessage != null) {
                MimeMessage.Accept (this);
            }
        }

        private void FillMissing ()
        {
            if (parsed.FullHtmlDocument == null) {
                if (parsed.FullText == null) {
                    parsed.FullText = "";
                }
                IncludeTextAsHtml (parsed.FullText);
            } else if (parsed.FullText == null) {
                IncludeHtmlDocumentAsText (parsed.FullHtmlDocument);
            }
            if (parsed.TopHtmlDocument == null) {
                if (parsed.TopText != null) {
                    var serializer = new HtmlTextDeserializer ();
                    parsed.TopHtmlDocument = serializer.Deserialize (parsed.TopText);
                } else {
                    var serializer = new HtmlTextSerializer (parsed.FullHtmlDocument);
                    serializer.Serialize ();
                    HtmlNode chatElement = null;
                    if (serializer.FoundTop) {
                        parsed.TopText = serializer.TopText;
                        parsed.TopHtmlDocument = parsed.FullHtmlDocument.CopyUntilNode (serializer.LastTopTextNode);
                        chatElement = parsed.TopHtmlDocument.FindElementWithId ("nacho-chat");
                    } else {
                        chatElement = parsed.FullHtmlDocument.FindElementWithId ("nacho-chat");
                        if (chatElement != null) {
                            var copyTarget = chatElement;
                            while (copyTarget.NodeType == HtmlNodeType.Element && copyTarget.ChildNodes.Count > 0) {
                                copyTarget = copyTarget.ChildNodes [copyTarget.ChildNodes.Count - 1];
                            }
                            parsed.TopHtmlDocument = parsed.FullHtmlDocument.CopyUntilNode (copyTarget);
                            chatElement = parsed.TopHtmlDocument.FindElementWithId ("nacho-chat");
                        }
                    }
                    if (chatElement != null) {
                        var body = parsed.TopHtmlDocument.DocumentNode.Element ("html").Element ("body");
                        chatElement.ParentNode.RemoveChild (chatElement);
                        body.AppendChild (chatElement);
                        for (int i = body.ChildNodes.Count - 2; i >= 0; --i) {
                            body.RemoveChild (body.ChildNodes [i]);
                        }
                        parsed.TopText = parsed.TopHtmlDocument.TextContents ();
                    }
                }
            } else if (parsed.TopText == null) {
                parsed.TopText = parsed.TopHtmlDocument.TextContents ();
            }
        }

        private void StoreFullEntries ()
        {
            var entry = new BundleManifest.Entry ();
            entry.Path = FullTextPath;
            entry.ContentType = "text/plain";
            Storage.StoreStringContentsForPath (parsed.FullText, entry.Path);
            Manifest.Entries [FullTextEntryName] = entry;

            entry = new BundleManifest.Entry ();
            entry.Path = FullHtmlPath;
            entry.ContentType = "text/html";
            using (var writer = Storage.TextWriterForPath(entry.Path)){
                parsed.FullHtmlDocument.Save (writer);
            }
            Manifest.Entries [FullHtmlEntryName] = entry;
        }

        private void StoreTopEntries ()
        {
            string topTextPath = null;
            string topHtmlPath = null;

            if (parsed.TopText != null && parsed.TopHtmlDocument != null) {
                topTextPath = TopTextPath;
                topHtmlPath = TopHtmlPath;
                Storage.StoreStringContentsForPath (parsed.TopText, topTextPath);
                using (var writer = Storage.TextWriterForPath (topHtmlPath)) {
                    parsed.TopHtmlDocument.Save (writer);
                }
            } else {
                // If there's no quoted content, then the top text is identical to the full text
                // Therefore, we can point the top entries to the paths for the full entries and save space
                topTextPath = FullTextPath;
                topHtmlPath = FullHtmlPath;
            }

            var entry = new BundleManifest.Entry ();
            entry.Path = topTextPath;
            entry.ContentType = "text/plain";
            Manifest.Entries [TopTextEntryName] = entry;

            entry = new BundleManifest.Entry ();
            entry.Path = topHtmlPath;
            entry.ContentType = "text/html";
            Manifest.Entries [TopHtmlEntryName] = entry;
        }

        private void StoreLightlyStyledEntries ()
        {
            // TODO: generate lightly styled text from HTML
            // Lightly styled text contains only basic formatting like bold/italic, which is
            // useful to convey emphasis, but can be used in a view with other messages and
            // maintain an overall consistentcy of font/size/color across all messages.

            string fullStyled = parsed.FullText;
            string topStyled = parsed.TopText;

            bool fullHasStyle = false;
            bool topHasStyle = false;

            string fullStyledPath = null;
            string topStyledPath = null;

            if (fullHasStyle) {
                fullStyledPath = FullLightlyStyledPath;
                Storage.StoreStringContentsForPath (fullStyled, fullStyledPath);
            } else {
                // If the lightly styled text is no different than the plain text (meaning there is no styling)
                // we can save space by pointing the styled entry to the plain text entry
                fullStyledPath = Manifest.Entries [FullTextEntryName].Path;
            }
            if (topHasStyle) {
                topStyledPath = TopLightlyStyledPath;
                Storage.StoreStringContentsForPath (topStyled, topStyledPath);
            } else {
                // Same pointer trick as above with the full styled text
                topStyledPath = Manifest.Entries [TopTextEntryName].Path;
            }

            var entry = new BundleManifest.Entry ();
            entry.Path = fullStyledPath;
            entry.ContentType = "text/rtf";
            Manifest.Entries [FullLightlyStyledEntryName] = entry;

            entry = new BundleManifest.Entry ();
            entry.Path = topStyledPath;
            entry.ContentType = "text/rtf";
            Manifest.Entries [TopLightlyStyledEntryName] = entry;
        }

        private void StoreManifest ()
        {
            Manifest.Version = LastestVersion;
            Storage.StoreObjectContentsForPath (Manifest, ManifestPath);
        }

        #endregion

        #region MimeVisitor (for Populate/Update)

        protected override void VisitMultipart (Multipart multipart)
        {
            VisitChildren (multipart);
        }

        protected override void VisitMultipartAlternative (MultipartAlternative alternative)
        {
            // The simple idea of multipart/alternative is that the children are listed in priority order, with
            // the higest priority last.  So you loop backwards until you find the type you want to display.
            // For bundle purposes, we're actually looking for two types simultaneously: html and plain text.
            // In effect, we want both alternatives so we can have each to show in various situations.
            // Further complicating matters, it's possible to have arbitrary nesting of other multipart/* types,
            // which makes it not totally obvious what type a child represents.  So we dive into any multipart
            // and guess if its basically html or text (by first depth-first leaf decendant).
            // Annoyingly, we could run into a nested multipart/alternative, which can't really be considered to be
            // just one type.  So that complicates things even further.
            // Finally, we'll accept an RTF part, but only we have to, as if it's always demoted to the lowest priority.
            // Most of these cases mentioned are rare-to-non-existant in real life, but they can happen and the code
            // needs to behave reasonably; it just adds cases and checks that aren't immediately obvious when considering
            // the simple idea of multipart/alternative.
            var IsOutermost = parsed.AlternateTypeInfo == null;
            bool foundHtml = false;
            bool foundText = false;
            if (IsOutermost) {
                MimeEntity rtfPart = null;
                parsed.AlternateTypeInfo = new AlternativeTypes ();
                for (int i = alternative.Count - 1; i >= 0; --i) {
                    var part = alternative [i];
                    var multipart = part as Multipart;
                    var contentType = part.ContentType;
                    if (multipart != null) {
                        contentType = MultipartContentType (multipart);
                    }
                    bool isHtml = contentType.IsMimeType ("text", "html");
                    bool isText = contentType.IsMimeType ("text", "plain");
                    if ((contentType.IsMimeType ("text", "rtf") || contentType.IsMimeType ("application", "rtf")) && rtfPart != null) {
                        // We don't really want RTF, but we'll hang onto in case we don't find html by the end.
                        // Even if RTF is a higher priority than HTML, we still de-prioritize it becasue we won't
                        // be displaying RTF natively; it will be converted to HTML, and the conversion may not be perfect.
                        rtfPart = part;
                    } else if (contentType.IsMimeType ("multipart", "alternative")) {
                        // This would be a very odd case when one alternative section is nested inside the other.
                        // We'll see if the child alternative has the types we're looking for
                        isHtml = MultipartMatchesContentType (multipart, "text", "html");
                        isText = MultipartMatchesContentType (multipart, "text", "plain");
                        if ((MultipartMatchesContentType (multipart, "text", "rtf") || MultipartMatchesContentType (multipart, "application", "rtf")) && rtfPart != null) {
                            rtfPart = part;
                        }
                    }
                    // Because of the nested alternative case, isHtml and isText are not mutually exclusive.  If they
                    // both exist in a nested alternative, we want them both.
                    if (isHtml && !foundHtml) {
                        parsed.AlternateTypeInfo.PopulatingHtml = true;
                        part.Accept (this);
                        parsed.AlternateTypeInfo.PopulatingHtml = false;
                        foundHtml = true;
                    }
                    if (isText && !foundText) {
                        parsed.AlternateTypeInfo.PopulatingText = true;
                        part.Accept (this);
                        parsed.AlternateTypeInfo.PopulatingText = false;
                        foundText = true;
                    }
                    if (contentType.IsMimeType ("text", "x-nacho-chat")) {
                        part.Accept (this);
                    }
                }
                // If it turns out we had an RTF, but no HTML part, go ahead and get the RTF
                if (rtfPart != null && foundHtml == false){
                    parsed.AlternateTypeInfo.PopulatingHtml = true;
                    parsed.AlternateTypeInfo.ConsiderRtfAsHtml = true;
                    rtfPart.Accept (this);
                    parsed.AlternateTypeInfo.PopulatingHtml = false;
                }
                parsed.AlternateTypeInfo = null;
            } else {
                // We're here if the message has nested multipart/alternative segments, which shouldn't really 
                // ever happen in practice because it doesn't really make sense as a concept.
                // If we're inside a larger alternate, it means we're looking for a particular type (either html
                // or text), which is what makes this code different from the outermost alternative case above.
                for (int i = alternative.Count - 1; i >= 0; --i) {
                    var part = alternative [i];
                    var multipart = part as Multipart;
                    var contentType = part.ContentType;
                    if (multipart != null) {
                        contentType = MultipartContentType (multipart);
                    }
                    bool isHtml = contentType.IsMimeType ("text", "html");
                    bool isText = contentType.IsMimeType ("text", "plain");
                    bool isRtf = contentType.IsMimeType ("text", "rtf") || contentType.IsMimeType ("application", "rtf");
                    if (contentType.IsMimeType ("multipart", "alternative")) {
                        isHtml = MultipartMatchesContentType (multipart, "text", "html");
                        isText = MultipartMatchesContentType (multipart, "text", "plain");
                        isRtf = MultipartMatchesContentType (multipart, "text", "rtf");
                    }
                    if (isHtml && !foundHtml && parsed.AlternateTypeInfo.PopulatingHtml) {
                        part.Accept (this);
                        foundHtml = true;
                    }
                    if (isText && !foundText && parsed.AlternateTypeInfo.PopulatingText) {
                        part.Accept (this);
                        foundText = true;
                    }
                    // We only care about the RTF alternative in a very specific case, if we've already gone through and
                    // haven't found HTML.  Theoretically, it could be our sencond trip through this code.  In order to only
                    // consider RTF in the second case, there's and extra flag to check: ConsiderRtfAsHtml
                    if (isRtf && !foundHtml && parsed.AlternateTypeInfo.PopulatingHtml && parsed.AlternateTypeInfo.ConsiderRtfAsHtml) {
                        part.Accept (this);
                        foundHtml = true;
                    }
                }
            }
        }

        protected override void VisitMultipartRelated (MultipartRelated related)
        {
            if (related == null) {
                Log.Warn (Log.LOG_UTILS, "NcEmailMessageBundle got null related part");
                return;
            }
            if (related.Root == null) {
                Log.Warn (Log.LOG_UTILS, "NcEmailMessageBundle got null related.Root");
                return;
            }
            parsed.RelatedStack.Add (related);
            related.Root.Accept (this);
            parsed.RelatedStack.RemoveAt (parsed.RelatedStack.Count - 1);
        }

        protected override void VisitTextPart (TextPart entity)
        {
            bool isAttachment = entity.ContentDisposition != null && entity.ContentDisposition.IsAttachment;
            if (isAttachment && MimeHelpers.isExchangeATTFilename (entity.FileName)) {
                isAttachment = false;
            }
            if (!isAttachment) {
                HtmlDocument htmlDocument = null;
                if (entity.IsHtml) {
                    htmlDocument = new HtmlDocument ();
                    htmlDocument.LoadHtml (entity.Text);
                }
                if (parsed.PopulateHtml) {
                    if (entity.IsHtml) {
                        IncludeHtmlDocument (htmlDocument);
                    } else if (entity.IsPlain) {
                        IncludeTextAsHtml (entity.Text.Trim ());
                    } else if (entity.IsRichText) {
                        if (parsed.AlternateTypeInfo == null || parsed.AlternateTypeInfo.ConsiderRtfAsHtml) {
                            IncludeRtfAsHtml (entity.Text);
                        }
                    }
                }
                if (parsed.PopulateText) {
                    if (entity.IsPlain) {
                        IncludeText (entity.Text.Trim ());
                    } else if (entity.IsHtml) {
                        IncludeHtmlDocumentAsText (htmlDocument);
                    } else if (entity.IsRichText) {
                        IncludeRtfAsText (entity.Text);
                    }
                }
            }
        }

        protected override void VisitMimePart (MimePart entity)
        {
            if (entity.ContentType.IsMimeType ("image", "*")) {
                // We'll skip anything with an explicit size of 0 because it's likely to be a part we truncated.
                // If there's no size set, assume there might be some data.
                if (entity.ContentDisposition == null || entity.ContentDisposition.Size > 0) {
                    // even though it's not an inline image, go ahead and include in message
                    VisitImagePart (entity);
                }
            } else {
                var tnef = entity as MimeKit.Tnef.TnefPart;
                if (tnef != null) {
                    VisitTnefPart (tnef);
                }
            }
        }

        protected override void VisitMessagePart (MessagePart entity)
        {
            VisitMessage (entity.Message, true);
        }

        protected override void VisitTnefPart (MimeKit.Tnef.TnefPart entity)
        {
            MimeMessage message = null;
            try {
                message = entity.ConvertToMessage ();
                MimeHelpers.RemoveNestedTnefParts (message);
                MimeHelpers.FixTnefMessage (message);
            } catch {
            }
            if (message != null) {
                VisitMessage (message, false);
            }
        }

        protected void VisitImagePart (MimePart entity)
        {
            if (parsed.PopulateHtml) {
                if (parsed.FullHtmlDocument == null) {
                    parsed.FullHtmlDocument = TemplateHtmlDocument ();
                }
                var entry = new BundleManifest.Entry ();
                var ext = FileExtForEntity (entity);
                entry.Path = SafeFilename ("image-attachment" + ext);
                var entryKey = entry.Path;
                Manifest.Entries [entryKey] = entry;
                entry.ContentType = entity.ContentType.MimeType;
                using (var writer = Storage.BinaryWriterForPath (entry.Path)) {
                    entity.ContentObject.DecodeTo (writer.BaseStream);
                }
                var body = parsed.FullHtmlDocument.DocumentNode.Element ("html").Element ("body");
                var img = body.AppendChild (parsed.FullHtmlDocument.CreateElement ("img"));
                var relativeUrl = Storage.RelativeUrlForPath (entry.Path, FullHtmlPath);
                if (relativeUrl != null) {
                    img.SetAttributeValue ("nacho-bundle-entry", entryKey);
                    img.SetAttributeValue ("src", relativeUrl.ToString());
                } else {
                    img.SetAttributeValue("src", Storage.UrlForPath(entry.Path, entry.ContentType).ToString ());
                }
                img.SetAttributeValue ("nacho-image-attachment", "true");
                img.SetAttributeValue ("nacho-resize", "true");
            }
        }

        protected void VisitMessage (MimeMessage message, bool includeHeaders = true)
        {
            ++SubmessageCount;
            string substorageRoot = null;
            string submessagePath = String.Format ("message{0}", SubmessageCount);
            var fileStorage = Storage as NcBundleFileStorage;
            if (fileStorage != null) {
                substorageRoot = Path.Combine (fileStorage.RootPath, submessagePath);
            }
            var bundle = new NcEmailMessageBundle (message, substorageRoot);
            bundle.Update ();
            if (parsed.PopulateText) {
                if (includeHeaders) {
                    IncludeText ("\n\n--------------------------------\n");
                    if (message.From.Count > 0) {
                        IncludeText (String.Format ("From: {0}\n", message.From.ToString ()));
                    }
                    if (message.To.Count > 0) {
                        IncludeText (String.Format ("To: {0}\n", message.To.ToString ()));
                    }
                    if (message.Cc.Count > 0) {
                        IncludeText (String.Format ("Cc: {0}\n", message.Cc.ToString ()));
                    }
                    if (message.Date != default(DateTimeOffset)) {
                        IncludeText (String.Format ("Sent: {0}\n", message.Date.ToString ()));
                    }
                    if (!String.IsNullOrEmpty (message.Subject)) {
                        IncludeText (String.Format ("Subject: {0}\n", message.Subject));
                    }
                    IncludeText ("\n");
                }
                IncludeText (bundle.FullText);
            }
            if (parsed.PopulateHtml) {
                // When including a sub-message, we'll load up the HTML from its bundle,
                // and then change the content around a little by placing all body nodes
                // within a new top-level node, and then insert headers at the start; finally,
                // fix up any relative image srcs to be relative to this parent bundle
                HtmlDocument doc = new HtmlDocument ();
                doc.LoadHtml (bundle.FullHtml);
                var body = doc.DocumentNode.Element ("html").Element ("body");
                var messageElement = doc.CreateElement ("div");
                messageElement.SetAttributeValue ("nacho-message-attachment", "true");
                if (includeHeaders) {
                    messageElement.AppendChild (doc.CreateElement ("br"));
                    messageElement.AppendChild (doc.CreateElement ("hr"));
                    var headersElement = doc.CreateElement ("div");
                    headersElement.SetAttributeValue ("nacho-message-headers", "true");
                    if (message.From.Count > 0) {
                        headersElement.AppendChild (SimpleMessageHeaderNode (doc, "From", message.From.ToString ()));
                    }
                    if (message.To.Count > 0) {
                        headersElement.AppendChild (SimpleMessageHeaderNode (doc, "To", message.To.ToString ()));
                    }
                    if (message.Cc.Count > 0) {
                        headersElement.AppendChild (SimpleMessageHeaderNode (doc, "Cc", message.Cc.ToString ()));
                    }
                    if (message.Date != default(DateTimeOffset)) {
                        headersElement.AppendChild (SimpleMessageHeaderNode (doc, "Sent", message.Date.ToString ()));
                    }
                    if (!String.IsNullOrEmpty (message.Subject)) {
                        headersElement.AppendChild (SimpleMessageHeaderNode (doc, "Subject", message.Subject));
                    }
                    headersElement.AppendChild (doc.CreateElement ("br"));
                    messageElement.AppendChild (headersElement);
                }
                var bodyElement = doc.CreateElement ("div");
                bodyElement.SetAttributeValue ("nacho-message-body", "true");
                messageElement.AppendChild (bodyElement);
                for (var i = body.ChildNodes.Count - 1; i >= 0; --i) {
                    var child = body.ChildNodes [i];
                    child.Remove ();
                    if (bodyElement.FirstChild == null) {
                        bodyElement.AppendChild (child);
                    } else {
                        bodyElement.InsertBefore (child, bodyElement.FirstChild);
                    }
                }
                body.AppendChild (messageElement);
                var stack = new List<HtmlNode> ();
                HtmlNode node = null;
                stack.Add (messageElement);
                while (stack.Count > 0) {
                    node = stack [0];
                    stack.RemoveAt (0);
                    if (node.NodeType == HtmlNodeType.Element) {
                        if (node.Name.Equals ("img")) {
                            if (node.Attributes.Contains ("nacho-bundle-entry")) {
                                foreach (var src in node.Attributes.AttributesWithName("src")) {
                                    src.Value = String.Format ("{0}/{1}", submessagePath, src.Value);
                                }
                            }
                        }
                        foreach (var child in node.ChildNodes) {
                            stack.Add (child);
                        }
                    }
                }
                IncludeHtmlDocument (doc);
            }
        }

        #endregion

        #region Parsing Helpers

        protected MimeKit.ContentType MultipartContentType (Multipart multipart)
        {
            if (multipart.Count > 0) {
                MimeEntity part = multipart [0];
                if (part is MultipartAlternative) {
                    return part.ContentType;
                } else if (part is MultipartRelated) {
                    var related = part as MultipartRelated;
                    if (related.Root is Multipart) {
                        return MultipartContentType (related.Root as Multipart);
                    }
                    return related.Root.ContentType;
                } else if (part is Multipart) {
                    return MultipartContentType (part as Multipart);
                } else {
                    return part.ContentType;
                }
            }
            return multipart.ContentType;
        }

        protected bool MultipartMatchesContentType (Multipart multipart, string mediaType, string mediaSubtype)
        {
            if (multipart.Count > 0){
                MimeEntity part = multipart [0];
                if (part is MultipartRelated) {
                    var related = part as MultipartRelated;
                    if (related.Root is Multipart) {
                        return MultipartMatchesContentType (related.Root as Multipart, mediaType, mediaSubtype);
                    }
                    return related.Root.ContentType.IsMimeType(mediaType, mediaSubtype);
                } else if (part is Multipart) {
                    return MultipartMatchesContentType (part as Multipart, mediaType, mediaSubtype);
                } else {
                    return part.ContentType.IsMimeType(mediaType, mediaSubtype);
                }
            }
            return false;
        }

        private MimePart RelatedImagePart (Uri uri)
        {
            // MimeKit strips off any trailing '.' when it validates Content-IDs.  We'll do the same so we can match
            if (uri.IsAbsoluteUri) {
                if (uri.Scheme.ToLowerInvariant () == "cid") {
                    uri = new Uri (uri.AbsoluteUri.TrimEnd ('.'), UriKind.Absolute);
                }
            }
            for (int i = parsed.RelatedStack.Count - 1; i >= 0; --i){
                var related = parsed.RelatedStack [i];
                var index = related.IndexOf (uri);
                if (index >= 0) {
                    return related [index] as MimePart;
                }
            }
            return null;
        }

        private void IncludeHtml (string html)
        {
            var document = new HtmlDocument ();
            document.LoadHtml (html);
            IncludeHtmlDocument (document);
        }

        private void IncludeHtmlDocument (HtmlDocument document)
        {
            // The basic idea here is to run through the html nodes, strip out anything
            // we don't want, and include the rest in our single html document.
            if (parsed.FullHtmlDocument == null) {
                parsed.FullHtmlDocument = TemplateHtmlDocument ();
            }
            List<HtmlNode> nodes = new List<HtmlNode> ();
            List<HtmlNode> headElements = new List<HtmlNode> ();
            List<HtmlNode> bodyElements = new List<HtmlNode> ();
            nodes.Add (document.DocumentNode);
            HtmlNode node;
            int remainingTableNodes = 0;
            bool inTable = false;
            while (nodes.Count > 0) {
                node = nodes [0];
                nodes.RemoveAt (0);
                inTable = remainingTableNodes > 0;
                if (remainingTableNodes > 0) {
                    // if we're in a table, adjust the remaing node count by the number of children pushed minus ourselves
                    remainingTableNodes += node.ChildNodes.Count - 1;
                } else if (node.NodeType == HtmlNodeType.Element && node.Name.Equals ("table")) {
                    remainingTableNodes = node.ChildNodes.Count;
                }
                if (node.NodeType == HtmlNodeType.Element) {
                    // Remove any and all script tags, wherever they exist in the document.
                    // This allows us to enable javascript in a webview to run our own javascript
                    // without worrying about malicious scripts in the original email source
                    if (node.Name.Equals("script")) {
                        node.Remove ();
                        continue;
                    }
                    // Remove any and all meta tags because there aren't any we need, and some we 
                    // absolutely must remove (charset, viewport, http-equiv="content-type", etc.)
                    // because our base template already sets those to the values we require
                    if (node.Name.Equals ("meta")) {
                        node.Remove ();
                        continue;
                    }
                    // Remove any and all base tags because we will be setting our own
                    if (node.Name.Equals ("base")) {
                        node.Remove ();
                        continue;
                    }
                    // Remove any and all title tags because we have our own (and title isn't used for emails anyway)
                    if (node.Name.Equals ("title")) {
                        node.Remove ();
                        continue;
                    }

                    if (node.Attributes.Contains ("nacho-tag")) {
                        node.Remove ();
                        continue;
                    }

                    // Consider removing:
                    // form - Phishing security risk, shouldn't be used in emails anway (possibly just sanitize action/method attributes or change to div)
                    // iframe - Possible spoofing risk?  Should see what other clients do
                    // embed - No need.  Should see what other clients do
                    // object - No need, plugin security risk, plugins not allowed anyway?  Should see what other clients do
                    // template - No need (html5, not really used yet), I think can include scripting.
                    // canvas - Pointless without JS

                    // remove any attributes related to scripting
                    for (int i = node.Attributes.Count - 1; i >= 0; --i){
                        var attr = node.Attributes [i];

                        // Remove any and all event attributes (onload, onclick, etc)
                        // to ensure that no scripts are run
                        if (attr.Name.StartsWith ("on")) {
                            attr.Remove ();
                        }

                        // If any attribute value starts with javascript:, just remove it.
                        // This will catch any href, src, or other attribute that has a javascript: scheme to start a url.
                        // It will also catch other things that may be harmless, but aren't useful for anything we care about.
                        if (attr.Value.Trim ().ToLower ().StartsWith ("javscript:")) {
                            attr.Remove ();
                        }
                    }

                    // Update any image references that point to other parts of the message to point to
                    // the storage area for this bundle
                    if (node.Name.Equals ("img") && node.Attributes.Contains("src")) {
                        // Removing our special attributes because no one else should be setting them
                        node.Attributes.Remove ("nacho-bundle-entry");
                        node.Attributes.Remove ("nacho-resize");
                        if (!inTable) {
                            node.Attributes.Add ("nacho-resize", "true");
                        }
                        // There should only be one src per img tag, we'll only consider the first one
                        var srcs = node.Attributes.AttributesWithName ("src");
                        string src = null;
                        foreach (var attr in srcs) {
                            src = attr.Value;
                            break;
                        }
                        UriKind kind;
                        if (Uri.IsWellFormedUriString (src, UriKind.Absolute)) {
                            kind = UriKind.Absolute;
                        } else if (Uri.IsWellFormedUriString (src, UriKind.Relative)) {
                            kind = UriKind.Relative;
                        } else {
                            kind = UriKind.RelativeOrAbsolute;
                        }
                        Uri uri = null;
                        try {
                            uri = new Uri (src, kind);
                        } catch {
                        }
                        if (uri != null) {
                            var imagePart = RelatedImagePart (uri);
                            if (imagePart != null) {
                                // Removing existing src in case 
                                node.Attributes.Remove ("src");
                                BundleManifest.Entry entry;
                                string entryKey;
                                if (!parsed.ImageEntriesBySrc.ContainsKey (src)) {
                                    entry = new BundleManifest.Entry ();
                                    var ext = FileExtForEntity (imagePart);
                                    entry.Path = SafeFilename ("image" + ext);
                                    entryKey = entry.Path;
                                    Manifest.Entries [entryKey] = entry;
                                    parsed.ImageEntriesBySrc [src] = entryKey;
                                    entry.ContentType = imagePart.ContentType.MimeType;
                                    using (var writer = Storage.BinaryWriterForPath (entry.Path)) {
                                        imagePart.ContentObject.DecodeTo (writer.BaseStream);
                                    }
                                } else {
                                    entryKey = parsed.ImageEntriesBySrc [src];
                                    entry = Manifest.Entries [entryKey];
                                }
                                var relativeUrl = Storage.RelativeUrlForPath (entry.Path, FullHtmlPath);
                                if (relativeUrl != null) {
                                    node.SetAttributeValue ("nacho-bundle-entry", entryKey);
                                    node.SetAttributeValue ("src", relativeUrl.ToString ());
                                } else {
                                    node.SetAttributeValue ("src", Storage.UrlForPath (entry.Path, entry.ContentType).ToString ());
                                }
                                node.SetAttributeValue ("nacho-original-src", src);
                            }
                        }
                    }
                }
                if (node.ParentNode != null){
                    if (node.ParentNode.NodeType == HtmlNodeType.Document) {
                        if (node.NodeType == HtmlNodeType.Element && !node.Name.Equals ("html") && !node.Name.Equals ("head") && !node.Name.Equals ("body")) {
                            // If we have a root-level element that's not html, head, or body, assume the tag should
                            // really be part of the document body.  Emails aren't always well-formed HTML documents and
                            // sometimes just start with <div>, for example.
                            bodyElements.Add (node);
                        } else if (node.NodeType == HtmlNodeType.Text) {
                            bodyElements.Add (node);
                        }
                    } else if (node.ParentNode.NodeType == HtmlNodeType.Element) {
                        if (node.ParentNode.Name.Equals ("head")) {
                            // We'll try to preserve any head nodes that we haven't already deleted
                            // this is mainly style and link tags, which generally shouldn't be used in emails,
                            // but could be, and shouldn't cause much harm to keep.  It's possible in situations
                            // where we end up combining multiple html parts that we'll put conflicting styles into
                            // the head of our single html document, but that will be pretty rare.
                            headElements.Add (node);
                        } else if (node.ParentNode.Name.Equals ("body")) {
                            // Preserve anything we find in the body that we haven't deleted.
                            bodyElements.Add (node);
                        } else if (node.ParentNode.Name.Equals ("html") && !node.Name.Equals ("head") && !node.Name.Equals ("body")) {
                            // If we come across any child nodes of html that aren't head or body, assume we're deailing
                            // with a poorly formed doc and the child nodes are part of the body.
                            bodyElements.Add (node);
                        }
                    }
                }
                foreach (var child in node.ChildNodes) {
                    nodes.Add (child);
                }
            }
            var head = parsed.FullHtmlDocument.DocumentNode.Element ("html").Element ("head");
            var body = parsed.FullHtmlDocument.DocumentNode.Element ("html").Element ("body");
            // I think there might be issues with OwnerDocument not being updated as we copy nodes
            // from one doc to another...not sure if it matters
            foreach (var element in headElements) {
                head.AppendChild(element);
            }
            foreach (var element in bodyElements) {
                body.AppendChild (element);
            }
        }

        private void IncludeTextAsHtml (string text)
        {
            if (parsed.FullHtmlDocument == null) {
                parsed.FullHtmlDocument = TemplateHtmlDocument ();
            }
            var serializer = new HtmlTextDeserializer ();
            var doc = serializer.Deserialize (text);
            IncludeHtmlDocument (doc);
        }

        private void IncludeRtfAsHtml (string rtf)
        {
            if (RtfConverter != null) {
                if (parsed.FullHtmlDocument == null) {
                    parsed.FullHtmlDocument = TemplateHtmlDocument ();
                }
                var html = RtfConverter.ToHtml (rtf);
                IncludeHtml (html);
            }
        }

        private void IncludeText (string text)
        {
            if (parsed.FullText == null) {
                parsed.FullText = "";
            }
            parsed.FullText += text;
        }

        private void IncludeHtmlDocumentAsText (HtmlDocument doc)
        {
            if (parsed.FullText == null) {
                parsed.FullText = "";
            }
            parsed.FullText += doc.TextContents ();
        }

        private void IncludeRtfAsText (string rtf)
        {
            if (RtfConverter != null) {
                if (parsed.FullText == null) {
                    parsed.FullText = "";
                }
                var txt = RtfConverter.ToTxt (rtf);
                IncludeText (txt);
            }
        }

        private static HtmlNode SimpleMessageHeaderNode (HtmlDocument doc, string name, string value)
        {
            var div = doc.CreateElement ("div");
            div.SetAttributeValue ("nacho-message-header", "true");
            var span = doc.CreateElement ("span");
            span.SetAttributeValue ("nacho-message-header-name", "true");
            span.AppendChild (doc.CreateTextNode (String.Format("{0}: ", name)));
            div.AppendChild (span);
            span = doc.CreateElement ("span");
            span.SetAttributeValue ("nacho-message-header-value", "true");
            span.AppendChild (doc.CreateTextNode (value));
            div.AppendChild (span);
            return div;
        }

        private void ResolveRelativeReferences (HtmlDocument doc, NcEmailMessageBundle sourceBundle)
        {
            var stack = new List<HtmlNode> ();
            stack.Add (doc.DocumentNode);
            HtmlNode node;
            while (stack.Count > 0) {
                node = stack [0];
                stack.RemoveAt (0);
                if (node.NodeType == HtmlNodeType.Element) {
                    if (node.Name.Equals ("img")) {
                        if (node.Attributes.Contains ("nacho-bundle-entry")) {
                            string sourceEntryName = node.Attributes ["nacho-bundle-entry"].Value;
                            var src = node.Attributes ["src"];
                            BundleManifest.Entry entry;
                            string entryKey;
                            if (!parsed.ImageEntriesBySrc.ContainsKey (src.Value)) {
                                var ext = Path.HasExtension(src.Value) ? Path.GetExtension (src.Value) : "";
                                entry = new BundleManifest.Entry ();
                                entry.Path = SafeFilename ("image" + ext);
                                entryKey = entry.Path;
                                Manifest.Entries [entryKey] = entry;
                                parsed.ImageEntriesBySrc [src.Value] = entryKey;
                                var sourceEntry = sourceBundle.Manifest.Entries [sourceEntryName];
                                entry.ContentType = sourceEntry.ContentType;
                                using (var writer = Storage.BinaryWriterForPath (entry.Path)) {
                                    using (var reader = sourceBundle.Storage.BinaryReaderForPath (sourceEntry.Path)) {
                                        int L = 1024;
                                        byte [] buffer = new byte[L];
                                        int bytesRead = 0;
                                        do {
                                            bytesRead = reader.Read (buffer, 0, L);
                                            writer.Write(buffer, 0, bytesRead);
                                        } while (bytesRead > 0);
                                    }
                                }
                            } else {
                                entryKey = parsed.ImageEntriesBySrc [src.Value];
                                entry = Manifest.Entries [entryKey];
                            }
                            var relativeUrl = Storage.RelativeUrlForPath (entry.Path, FullHtmlPath);
                            if (relativeUrl != null) {
                                node.SetAttributeValue ("nacho-bundle-entry", entryKey);
                                src.Value = relativeUrl.ToString ();
                            } else {
                                src.Value = Storage.UrlForPath (entry.Path, entry.ContentType).ToString ();
                            }
                        }
                    }
                }
                foreach (var child in node.ChildNodes) {
                    stack.Add (child);
                }
            }
        }

        string FileExtForEntity (MimePart entity)
        {
            if (!String.IsNullOrEmpty (entity.FileName) && Path.HasExtension(entity.FileName)) {
                return Path.GetExtension (entity.FileName);
            }
            return "";
        }

        #endregion
    }
}

