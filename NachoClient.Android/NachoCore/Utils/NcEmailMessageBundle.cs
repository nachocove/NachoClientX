//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.Net;
using MimeKit;
using NachoCore.Model;
using HtmlAgilityPack;

namespace NachoCore.Utils
{

    #region Storage

    public abstract class NcEmailMessageBundleStorage
    {

        public abstract Stream ReadableContentStreamForPath (string path);
        public abstract Stream WriteableContentsStreamForPath (string path);
        public abstract Uri UrlForPath (string path, string contentType);

        public string StringContentsForPath (string path)
        {
            Stream contents = ReadableContentStreamForPath (path);
            if (contents != null) {
                StreamReader reader = new StreamReader (contents, System.Text.Encoding.UTF8);
                return reader.ReadToEnd ();
            }
            return null;
        }

        public void StoreStringContentsForPath (string stringContents, string path)
        {
            var contents = WriteableContentsStreamForPath (path);
            var writer = new StreamWriter (contents, System.Text.Encoding.UTF8);
            writer.Write (stringContents);
            writer.Close ();
            contents.Close ();
        }

        public object ObjectContentsForPath (string path)
        {
            Stream contents = ReadableContentStreamForPath (path);
            if (contents != null) {
                BinaryFormatter serializer = new BinaryFormatter ();
                return serializer.Deserialize (contents);
            }
            return null;
        }

        public void StoreObjectContentsForPath (object o, string path)
        {
            var stream = WriteableContentsStreamForPath (path);
            if (o != null) {
                BinaryFormatter serializer = new BinaryFormatter ();
                serializer.Serialize (stream, 0);
                stream.Close ();
            }
        }
    }


    class NcEmailMessageBundleMemoryStorage
    {

        private Dictionary<string, object> MemoryStorage;
        private HttpListener Server;

        public NcEmailMessageBundleMemoryStorage () : base ()
        {
            MemoryStorage = new Dictionary<string, byte[]> ();
        }

        public Stream ReadableContentStreamForPath (string path)
        {
            object o;
            if (MemoryStorage.TryGetValue (path, o)) {
                var stream = o as Stream;
                if (stream != null) {
                    stream.Seek (0);
                }
                return stream;
            }
            return null;
        }

        public Stream WriteableContentsStreamForPath (string path)
        {
            var stream = new MemoryStream ();
            MemoryStorage [path] = stream;
            return stream;
        }

        public Uri UrlForPath (string path, string contentType)
        {
            var contents = ReadableContentStreamForPath (path) as MemoryStream;
            if (contents != null){
                string base64Encoded = Convert.ToBase64String(contents.ToArray ());
                return String.Format ("data:{0};base64,{1}", contentType, base64Encoded);
            }
            return null;
        }

        public string StringContentsForPath (string path)
        {
            return ObjectContentsForPath (path) as string;
        }

        public void StoreStringContentsForPath (string stringContents, string path)
        {
            StoreObjectContentsForPath(stringContents, path);
        }

        public object ObjectContentsForPath (string path)
        {
            object o;
            if (MemoryStorage.TryGetValue (path, o)) {
                return o;
            }
            return null;
        }

        public void StoreObjectContentsForPath (object o, string path)
        {
            MemoryStorage [path] = o;
        }

    }


    class NcEmailMessageBundleFileStorage
    {

        private string RootPath;

        public NcEmailMessageBundleFileStorage (string rootPath) : base ()
        {
            RootPath = rootPath;
        }

        public Stream ReadableContentStreamForPath (string path)
        {
            var filePath = FullFilePathForLocalPath (path);
            return new FileStream (filePath, FileMode.Open);
        }

        public Stream WriteableContentsStreamForPath (string path)
        {
            var filePath = FullFilePathForLocalPath (path);
            return new FileStream (filePath, FileMode.Create);
        }

        public Uri UrlForPath (string path, string contentType)
        {
            return new Uri (String.Format("file://{0}/{1}", RootPath.Replace(Path.DirectorySeparatorChar, '/'), path.Replace(Path.DirectorySeparatorChar, '/')));
        }

        private string FullFilePathForLocalPath (string path)
        {
            return Path.Combine (RootPath, path);
        }

    }

    #endregion

    #endregion

    public class NcEmailMessageBundle : MimeVisitor
    {

        [Serializable]
        private class BundleManifest
        {

            [Serializable]
            public class Entry
            {
                public string Path { get; set; }
                public string ContentType { get; set; }
            }

            public int Version { get; set; }
            public Dictionary<string, Entry> Entries { get; set; }

            public BundleManifest (int version)
            {
                Version = version;
                Entries = new Dictionary<string, Entry> ();
            }

        }

        private class ParseResult {
            public HtmlDocument FullHtmlDocument = null;
            public HtmlDocument TopHtmlDocument = null;
            public string FullText = null;
            public string TopText = null;
            public List<MultipartRelated> RelatedStack;

            public ParseResult ()
            {
                RelatedStack = new List<MultipartRelated> ();
            }
        }

        #region Properties

        public bool NeedsUpdate = false;
        private bool HasHtmlUrl = true;
        private McEmailMessage Message = null;
        private NcEmailMessageBundleStorage Storage;
        private BundleManifest Manifest;

        private static int LastestVersion = 1;

        private static string FullTextEntryName = "full-text";
        private static string TopTextEntryName = "top-text";
        private static string FullHtmlEntryName = "full-html";
        private static string TopHtmlEntryName = "top-html";
        private static string FullLightlyStyledEntryName = "full-simple";
        private static string TopLightlyStyledEntryName = "top-simple";

        private static string ManifestPath = "manifest.json";
        private static string FullTextPath = "full.txt";
        private static string TopTextPath = "top.txt";
        private static string FullHtmlPath = "full.html";
        private static string TopHtmlPath = "top.html";
        private static string FullLightlyStyledPath = "full.rtf";
        private static string TopLightlyStyledPath = "top.rtf";

        ParseResult parsed;

        #endregion

        #region Constructors

        public NcEmailMessageBundle (McEmailMessage message)
        {
            Storage = new NcEmailMessageBundleMemoryStorage ();
            HasHtmlUrl = false;
            Message = message;
            Manifest = Storage.ObjectContentsForPath (ManifestPath);
            if (Manifest != null) {
                string versionString;
                if (Manifest.Version) {
                    Version = System.Int32.Parse (versionString);
                    NeedsUpdate = Version != LastestVersion;
                } else {
                    NeedsUpdate = true;
                }
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

        public string TopHtmlUrl {
            get {
                if (HasHtmlUrl) {
                    return GetUrlOfManifestEntry (TopHtmlEntryName);
                }
                return null;
            }
        }

        public string FullHtmlUrl {
            get {
                if (HasHtmlUrl) {
                    return GetUrlOfManifestEntry (FullHtmlEntryName);
                }
                return null;
            }
        }

        #endregion

        #region Helpers

        private string GetUrlOfManifestEntry (string name)
        {
            if (Manifest != null && Manifest.Entries != null) {
                BundleManifest.Entry entry;
                if (Manifest.Entries.TryGetValue (name, entry)) {
                    return Storage.UrlForPath (entry.Path, entry.ContentType);
                }
            }
            return null;
        }

        private string GetStringContentsOfManifestEntry (string name)
        {
            if (Manifest != null && Manifest.Entries != null) {
                BundleManifest.Entry entry;
                if (Manifest.Entries.TryGetValue (name, entry)) {
                    string[] typeParts = entry.ContentType.Split ('/');
                    if (typeParts [0] == "text") {
                        return Storage.StringContentsForPath (entry.Path);
                    }
                    return null;
                }
            }
            return null;
        }

        #endregion

        protected string TemplateHtml ()
        {
            // This should come from a resource file within the app, but I'm not sure
            // how to do that in a cross platform way.  It may be that we need a subclass
            // on each platform just to fill in this method 
        }

        #region Populate/Update Bundle

        private void UpdateBundle ()
        {
            ParseMessage ();
            StoreFullEntries ();
            StoreTopEntries ();
            StoreLightlyStyledEntries ();
            StoreManifest ();
            // TODO: store images somewhere (maybe symlink if they're already stored somewhere)
        }

        private void ParseMessage ()
        {
            var parsed = new ParseResult ();

            // FIXME: what to load?
            var mime = MimeMessage.Load ();
            mime.Accept (this);

            if (parsed.FullHtmlDocument == null) {
                if (parsed.FullText == null) {
                    parsed.FullText = "";
                }
                parsed.FullHtmlDocument = new HtmlDocument ();
                parsed.FullHtmlDocument.LoadHtml (TemplateHtml ());
                // TODO: write FullText to the html doc
            } else if (parsed.FullText == null) {
                // TODO: populate FullText with text from html
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
            var stream = Storage.WriteableContentsStreamForPath (entry.Path);
            parsed.FullHtmlDocument.Save (stream);
            stream.Close ();
            Manifest.Entries [FullHtmlEntryName] = entry;
        }

        private void StoreTopEntries ()
        {
            // TODO: figure out if the message has quoted text that should be removed from the top entries

            bool HasQuote = false;
            string topTextPath = null;
            string topHtmlPath = null;

            if (HasQuote) {
                // If there's no quoted content, then the top text is identical to the full text
                // Therefore, we can point the top entries to the paths for the full entries and save space
                topTextPath = TopTextPath;
                topHtmlPath = TopHtmlPath;
                Storage.StoreStringContentsForPath (TopText, topTextPath);
                var stream = Storage.WriteableContentsStreamForPath (topHtmlPath);
                parsed.TopHtmlDocument.Save (stream);
                stream.Close ();
            } else {
                topTextPath = FullTextPath;
                topHtmlPath = FullHtmlPath;
            }

            var entry = new BundleManifest.Entry ();
            entry.Path = TopTextPath;
            entry.ContentType = "text/plain";
            Manifest.Entries [TopTextEntryName] = entry;

            entry = new BundleManifest.Entry ();
            entry.Path = TopHtmlPath;
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
            Manifest.Entries [FullTextEntryName] = entry;

            entry = new BundleManifest.Entry ();
            entry.Path = topStyledPath;
            entry.ContentType = "text/rtf";
            Manifest.Entries [FullTextEntryName] = entry;
        }

        private void StoreManifest ()
        {
            Manifest.Version = LastestVersion;
            Storage.StoreObjectContentsForPath (Manifest, ManifestPath);
        }

        #endregion

        #region MimeVisitor (for Populate/Update)

        protected override void VisitMultipartAlternative (MultipartAlternative alternative)
        {
            // Within an alternative set of parts, loop backwards and only take the lastmost html and plain text
            // parts.  We use either part depending on the situation, which is why we look for both.
            // I've also seen situations where there's a text/plain and text/calendar alternate parts, which might
            // be a bug on our sending/composing end, but perhaps we should watch out for that kind of thing here.
            bool foundHtml = false;
            bool foundText = false;
            for (int i = alternative.Count - 1; i >= 0; --i) {
                var part = alternative [i];
                if (part is TextPart) {
                    if (!foundHtml && ((TextPart)part).IsHtml) {
                        foundHtml = true;
                        part.Accept (this);
                    }
                    if (!foundText && ((TextPart)part).IsPlain) {
                        foundText = true;
                        part.Accept (this);
                    }
                }
            }
        }

        protected override void VisitMultipartRelated (MultipartRelated related)
        {
            parsed.RelatedStack.Add (related);
            related.Root.Accept (this);
            parsed.RelatedStack.RemoveAt (parsed.RelatedStack.Count - 1);
        }

        protected override void VisitTextPart (TextPart entity)
        {
            if (entity.IsHtml) {
                if (parsed.FullHtmlDocument == null) {
                    parsed.FullHtmlDocument = new HtmlDocument ();
                    parsed.FullHtmlDocument.LoadHtml (TemplateHtml ());
                }
                IncludeHtml (entity.Text);
            } else if (entity.IsPlain) {
                if (FullText == null) {
                    FullText = entity.Text;
                } else {
                    FullText += entity.Text;
                }
            }
        }

        protected override void VisitMimePart (MimePart entity)
        {
            if (entity.ContentType.Matches ("image", "*")) {
                // even though it's not an inline image, go ahead and include in message
                var entry = new BundleManifest.Entry ();
                Manifest.Entries [entry.Path] = entry;
                entry.Path = SafeFilename ("image-attachment");
                entry.ContentType = entity.ContentType.MimeType;
                var stream = Storage.WriteableContentsStreamForPath (entry.Path);
                entity.ContentObject.DecodeTo (stream);
                stream.Close ();

                if (parsed.FullText == null) {
                    parsed.FullText = "";
                }
                parsed.FullText += String.Format(" [{0}]", entity.FileName);

                if (parsed.FullHtmlDocument == null) {
                    parsed.FullHtmlDocument = new HtmlDocument ();
                    parsed.FullHtmlDocument.LoadHtml (TemplateHtml ());
                }
                var body = parsed.FullHtmlDocument.DocumentNode.Element ("html").Element ("body");
                var img = body.AppendChild (parsed.FullHtmlDocument.CreateElement ("img"));
                img.SetAttributeValue ("nacho-image-attachment", "true");
                img.SetAttributeValue ("src", Storage.UrlForPath(entry.Path, entry.ContentType));
            } else {
            }
            // TODO: check for calendar type
        }

        #endregion

        private MimePart RelatedImagePart (string src)
        {
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
                uri = new Uri (src);
            } catch {
                return null;
            }
            for (int i = parsed.RelatedStack.Count - 1; i >= 0; --i){
                var related = parsed.RelatedStack [i];
                var index = related.IndexOf (uri);
                if (index >= 0) {
                    return related [i] as MimePart;
                }
            }
            return null;
        }

        private string SafeFilename (string unsafeFilename)
        {
            string[] split = unsafeFilename.Split (Path.GetInvalidFileNameChars, StringSplitOptions.RemoveEmptyEntries);
            var basename = String.Join ("_", split);
            string filename = basename;
            bool exists = false;
            int i = 0;
            do {
                exists = false;
                if (i > 0){
                    filename = String.Format("{0}_{1}", basename, i);
                }
                foreach (var k in Manifest.Entries) {
                    if (Manifest.Entries [k].Path.Equals (filename)) {
                        exists = true;
                        break;
                    }
                }
                ++i;
            } while (exists);
            return filename;
        }

        private void IncludeHtml (string html)
        {
            // The basic idea here is to run through the html source, strip out anything
            // we don't want, and include the rest in our single html document.
            // 
            var document = new HtmlDocument ();
            document.LoadHtml (html);
            List<HtmlNode> nodes = new List<HtmlNode> ();
            List<HtmlNode> headElements = new List<HtmlNode> ();
            List<HtmlNode> bodyElements = new List<HtmlNode> ();
            nodes.Add (document.DocumentNode);
            HtmlNode node;
            while (nodes.Count > 0) {
                node = nodes [0];
                node = nodes.RemoveAt (0);
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

                    // Consider removing:
                    // form - Phishing security risk, shouldn't be used in emails anway (possibly just sanitize action/method attributes or change to div)
                    // iframe - Possible spoofing risk?  Should see what other clients do
                    // embed - No need.  Should see what other clients do
                    // object - No need, plugin security risk, plugins not allowed anyway?  Should see what other clients do
                    // template - No need (html5, not really used yet), I think can include scripting.
                    // canvas - Pointless without JS

                    // Remove any and all event attributes (onload, onclick, etc)
                    // to ensure that no scripts are run
                    for (int i = node.Attributes.Count - 1; i >= 0; --i){
                        var attr = node.Attributes [i];
                        if (attr.Name.StartsWith ("on")) {
                            attr.Remove ();
                        }
                    }

                    // If any attribute value starts with javascript:, just remove it.
                    // This will catch any href, src, or other attribute that has a javascript: scheme to start a url.
                    // It will also catch other things that may be harmless, but aren't useful for anything we care about.
                    foreach (var attr in node.Attributes){
                        if (attr.Value.Trim ().ToLower ().StartsWith ("javscript:")) {
                            attr.Remove ();
                        }
                    }

                    // Update any image references that point to other parts of the message to point to
                    // the storage area for this bundle
                    if (node.Name.Equals ("img") && node.Attributes.Contains("src")) {
                        var srcs = node.Attributes.AttributesWithName ("src");
                        // There should only be one src per img tag, but we'll go ahead and change them all if more than 1 exist
                        foreach (var src in srcs) {
                            var imagePart = RelatedImagePart (src.Value);
                            if (imagePart != null) {
                                var entryKey = src.Value;
                                BundleManifest.Entry entry;
                                if (!Manifest.Entries.ContainsKey (entryKey)) {
                                    entry = new BundleManifest.Entry ();
                                    Manifest.Entries [entryKey] = entry;
                                    entry.Path = SafeFilename (entryKey);
                                    entry.ContentType = imagePart.ContentType.MimeType;
                                    var stream = Storage.WriteableContentsStreamForPath (entry.Path);
                                    imagePart.ContentObject.DecodeTo (stream);
                                    stream.Close ();
                                } else {
                                    entry = Manifest.Entries [entryKey];
                                }
                                node.SetAttributeValue ("nacho-original-src", src.Value);
                                src.Value = Storage.UrlForPath (entry.Path, entry.ContentType);
                            }
                        }
                    }

                    if (node.ParentNode != null){
                        if (node.ParentNode.NodeType == HtmlNodeType.Document) {
                            if (!node.Name.Equals ("html") && !node.Name.Equals ("head") && !node.Name.Equals ("body")) {
                                // If we have a root-level element that's not html, head, or body, assume the tag should
                                // really be part of the document body.  Emails aren't always well-formed HTML documents and
                                // sometimes just start with <div>, for example.
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

    }
}

