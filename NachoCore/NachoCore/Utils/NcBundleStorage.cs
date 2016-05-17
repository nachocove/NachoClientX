//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;

namespace NachoCore.Utils
{

    #region Base Storage Class

    public interface NcBundleStorageSerializable {

        byte[] SerializeForBundleStorage ();
        void DeserializeFromBundleStorage (byte[] contents);

    }

    public abstract class NcBundleStorage
    {

        public abstract TextReader TextReaderForPath (string path);
        public abstract TextWriter TextWriterForPath (string path);
        public abstract BinaryReader BinaryReaderForPath (string path);
        public abstract BinaryWriter BinaryWriterForPath (string path);
        public abstract Uri UrlForPath (string path, string contentType);
        public abstract Uri RelativeUrlForPath (string path, string relativeToPath);
        public abstract Uri RelativeUrlForDocumentsPath (string path);
        public abstract Uri BaseUrl ();
        public abstract void Delete ();

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
        public abstract void StoreObjectContentsForPath (NcBundleStorageSerializable o, string path);
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

        public override Uri RelativeUrlForPath (string path, string relativeToPath)
        {
            return null;
        }

        public override Uri RelativeUrlForDocumentsPath (string path)
        {
            return new Uri (path, UriKind.Relative);
        }

        public override Uri BaseUrl ()
        {
            var documentsPath = NcApplication.GetDocumentsPath ();
            return new Uri (String.Format ("file://{0}/", documentsPath));
        }

        public override void Delete ()
        {
            MemoryStorage.Clear ();
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
            MemoryStorage [path] = stringContents;
        }

        public override object ObjectContentsForPath (string path, Type t)
        {
            object o;
            if (MemoryStorage.TryGetValue (path, out o)) {
                return o;
            }
            return null;
        }

        public override void StoreObjectContentsForPath (NcBundleStorageSerializable o, string path)
        {
            MemoryStorage [path] = o;
        }

    }

    #endregion

    #region File Storage

    class NcBundleFileStorage : NcBundleStorage
    {

        private string rootPath;

        public string RootPath {
            get {
                return rootPath;
            }
        }

        public NcBundleFileStorage (string rootPath) : base ()
        {
            this.rootPath = rootPath;
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
                return new BinaryReader (new FileStream (filePath, FileMode.Open, FileAccess.Read));
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

        public override Uri RelativeUrlForPath (string path, string relativeToPath)
        {
            var url = UrlForPath (path, null);
            var relativeToUrl = UrlForPath (relativeToPath, null);
            return relativeToUrl.MakeRelativeUri (url);
        }

        public override Uri RelativeUrlForDocumentsPath (string path)
        {
            var documentsPath = NcApplication.GetDocumentsPath ();
            var documentUri = new Uri (String.Format ("file://{0}/{1}", documentsPath, path));
            var indexUri = UrlForPath ("x", "");
            return indexUri.MakeRelativeUri (documentUri);
        }

        public override Uri BaseUrl ()
        {
            return new Uri(String.Format("file://{0}/", RootPath));
        }

        public override void Delete ()
        {
            Directory.Delete (RootPath, true);
            Directory.CreateDirectory (RootPath);
        }

        private string FullFilePathForLocalPath (string path)
        {
            return Path.Combine (RootPath, path);
        }

        public override object ObjectContentsForPath (string path, Type t)
        {
            var filePath = FullFilePathForLocalPath (path);
            if (File.Exists(filePath)){
                var contents = File.ReadAllBytes (filePath);
                var o = Activator.CreateInstance (t) as NcBundleStorageSerializable;
                if (o != null) {
                    o.DeserializeFromBundleStorage (contents);
                    return o;
                }
            }
            return null;
        }

        public override void StoreObjectContentsForPath (NcBundleStorageSerializable o, string path)
        {
            var filePath = FullFilePathForLocalPath (path);
            using (var stream = new FileStream (filePath, FileMode.Create)) {
                if (o != null) {
                    var contents = o.SerializeForBundleStorage ();
                    stream.Write (contents, 0, contents.Length);
                }
            }
        }

    }

    #endregion

}

