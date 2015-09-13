//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace NachoCore.Utils
{

    #region Base Storage Class

    public abstract class NcBundleStorage
    {

        public abstract TextReader TextReaderForPath (string path);
        public abstract TextWriter TextWriterForPath (string path);
        public abstract BinaryReader BinaryReaderForPath (string path);
        public abstract BinaryWriter BinaryWriterForPath (string path);
        public abstract Uri UrlForPath (string path, string contentType);
        public abstract Uri RelativeUrlForPath (string path, string contentType, string relativeToPath);
        public abstract Uri RelativeUrlForDocumentsPath (string path);
        public abstract Uri BaseUrl ();

        public virtual string StringContentsForPath (string path)
        {
            using (TextReader reader = TextReaderForPath(path)) {
                if (reader != null) {
                    return reader.ReadToEnd ();
                }
            }
            return null;
        }

        public virtual void StoreStringContentsForPath (string stringContents, string path)
        {
            using (var writer = TextWriterForPath (path)) {
                writer.Write (stringContents);
            }
        }

        public abstract object ObjectContentsForPath (string path, Type t);
        public abstract void StoreObjectContentsForPath (object o, string path);
    }

    #endregion

    #region Memory Storage

    class NcBundleMemoryStorage : NcBundleStorage
    {

        private Dictionary<string, object> MemoryStorage;

        public NcBundleMemoryStorage () : base ()
        {
            MemoryStorage = new Dictionary<string, object> ();
        }

        public override TextReader TextReaderForPath (string path)
        {
            object o;
            if (MemoryStorage.TryGetValue (path, out o)) {
                var stream = o as MemoryStream;
                if (stream != null) {
                    stream.Position = 0;
                    return new StreamReader (stream);
                }
            }
            return null;
        }

        public override TextWriter TextWriterForPath (string path)
        {
            var stream = new MemoryStream ();
            MemoryStorage [path] = stream;
            return new StreamWriter (stream, System.Text.Encoding.UTF8, 100, true);
        }

        public override BinaryReader BinaryReaderForPath (string path)
        {
            object o;
            if (MemoryStorage.TryGetValue (path, out o)) {
                var stream = o as MemoryStream;
                if (stream != null) {
                    return new BinaryReader (stream);
                }
            }
            return null;
        }

        public override BinaryWriter BinaryWriterForPath (string path)
        {
            var stream = new MemoryStream ();
            MemoryStorage [path] = stream;
            return new BinaryWriter (stream);
        }

        public override Uri UrlForPath (string path, string contentType)
        {
            var contents = MemoryStorage [path] as MemoryStream;
            if (contents != null){
                string base64Encoded = Convert.ToBase64String(contents.ToArray ());
                return new Uri(String.Format ("data:{0};base64,{1}", contentType, base64Encoded), UriKind.Absolute);
            }
            return null;
        }

        public override Uri RelativeUrlForPath (string path, string contentType, string relativeToPath)
        {
            return UrlForPath (path, contentType);
        }

        public override Uri RelativeUrlForDocumentsPath (string path)
        {
            return new Uri (path, UriKind.Relative);
        }

        public override Uri BaseUrl ()
        {
            var documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            return new Uri (String.Format ("file://{0}/", documentsPath));
        }

        public override string StringContentsForPath (string path)
        {
            var o = ObjectContentsForPath (path, null);
            if (o is Stream) {
                return base.StringContentsForPath (path);
            }
            return o as string;
        }

        public override void StoreStringContentsForPath (string stringContents, string path)
        {
            StoreObjectContentsForPath(stringContents, path);
        }

        public override object ObjectContentsForPath (string path, Type t)
        {
            object o;
            if (MemoryStorage.TryGetValue (path, out o)) {
                return o;
            }
            return null;
        }

        public override void StoreObjectContentsForPath (object o, string path)
        {
            MemoryStorage [path] = o;
        }

    }

    #endregion

    #region File Storage

    class NcBundleFileStorage : NcBundleStorage
    {

        private string RootPath;

        public NcBundleFileStorage (string rootPath) : base ()
        {
            RootPath = rootPath;
            if (!Directory.Exists (RootPath)) {
                Directory.CreateDirectory (RootPath);
            }
        }

        public override TextReader TextReaderForPath (string path)
        {
            var filePath = FullFilePathForLocalPath (path);
            if (File.Exists (filePath)) {
                return new StreamReader (filePath);
            }
            return null;
        }

        public override TextWriter TextWriterForPath (string path)
        {
            var filePath = FullFilePathForLocalPath (path);
            return new StreamWriter (filePath);
        }

        public override BinaryReader BinaryReaderForPath (string path)
        {
            var filePath = FullFilePathForLocalPath (path);
            if (File.Exists (filePath)) {
                return new BinaryReader (new FileStream (filePath, FileMode.Open));
            }
            return null;
        }

        public override BinaryWriter BinaryWriterForPath (string path)
        {
            var filePath = FullFilePathForLocalPath (path);
            return new BinaryWriter (new FileStream (filePath, FileMode.Create));
        }

        public override Uri UrlForPath (string path, string contentType)
        {
            return new Uri (String.Format("file://{0}/{1}", RootPath.Replace(Path.DirectorySeparatorChar, '/'), path.Replace(Path.DirectorySeparatorChar, '/')));
        }

        public override Uri RelativeUrlForPath (string path, string contentType, string relativeToPath)
        {
            var a = new List<string>(path.Split (Path.DirectorySeparatorChar));
            var b = new List<string>(relativeToPath.Split (Path.DirectorySeparatorChar));
            int i = 0;
            for (; i < a.Count && i < b.Count; ++i) {
                if (!a[i].Equals(b[i])){
                    break;
                }
            }
            if (i == 0) {
                for (int j = 0; j < b.Count - 1; ++j){
                    a.Insert (0, "..");
                }
            }
            var relative = String.Join("/", a.GetRange (i, a.Count - i).ToArray());
            return new Uri (relative, UriKind.Relative);
        }

        public override Uri RelativeUrlForDocumentsPath (string path)
        {
            var documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            var documentUri = new Uri (String.Format ("file://{0}/{1}", documentsPath, path));
            var indexUri = UrlForPath ("x", "");
            return indexUri.MakeRelativeUri (documentUri);
        }

        public override Uri BaseUrl ()
        {
            return new Uri(String.Format("file://{0}/", RootPath));
        }

        private string FullFilePathForLocalPath (string path)
        {
            return Path.Combine (RootPath, path);
        }

        public override object ObjectContentsForPath (string path, Type t)
        {
            var filePath = FullFilePathForLocalPath (path);
            if (File.Exists(filePath)){
                using (Stream contents = new FileStream(filePath, FileMode.Open)) {
                    DataContractSerializer serializer = new DataContractSerializer (t);
                    var o = serializer.ReadObject (contents);
                    return o;
                }
            }
            return null;
        }

        public override void StoreObjectContentsForPath (object o, string path)
        {
            var filePath = FullFilePathForLocalPath (path);
            using (var stream = new FileStream (filePath, FileMode.Create)) {
                if (o != null) {
                    DataContractSerializer serializer = new DataContractSerializer (o.GetType());
                    serializer.WriteObject (stream, o);
                }
            }
        }

    }

    #endregion

}

