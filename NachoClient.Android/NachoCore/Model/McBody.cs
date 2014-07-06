using System;
using System.IO;

namespace NachoCore.Model
{
    public class McBody : McObject
    {
        public bool IsValid { get; set; }

        /// Body type is stored in McItem, along with the item's index to McBody.
        /// This circumvents reading db just to get body type. Most references to
        /// bodies are to get its path, which is computed with the body id, without
        /// reading from the database.
        /// 
        /// AirSync.TypeCode PlainText_1, Html_2, Rtf_3, Mime_4
        public const int PlainText = 1;
        public const int HTML = 2;
        public const int RTF = 3;
        public const int MIME = 4;

        public string Body {
            get { return Get (Id); }
        }

        public string BodyPath {
            get { return GetBodyPath (Id); }
        }

        public static string GetBodyPath (int id)
        {
            if (0 == id) {
                return null;
            }
            return Path.Combine (NcModel.Instance.BodiesDir, id.ToString ());
        }

        public static McBody SaveStart ()
        {
            var body = new McBody ();
            body.IsValid = false;
            body.Insert ();
            return body;
        }

        public void SaveDone ()
        {
            IsValid = true;
            Update ();
        }

        public static McBody Save (string content)
        {
            var body = SaveStart ();
            File.WriteAllText (GetBodyPath (body.Id), content);
            body.SaveDone ();
            return body;
        }

        public FileStream SaveFileStream ()
        {
            return File.OpenWrite (GetBodyPath (Id));
        }

        public static int Duplicate (int id)
        {
            if (0 == id) {
                return 0;
            }
            var body = SaveStart ();
            File.Copy (GetBodyPath (id), GetBodyPath (body.Id));
            body.SaveDone ();
            return body.Id;
        }

        public static McBody GetDescr (int id)
        {
            if (0 == id) {
                return null;
            }
            var body = NcModel.Instance.Db.Get<McBody> (id);
            return (null == body || !body.IsValid) ? null : body;
        }

        public static string Get (int id)
        {
            if (0 == id) {
                return null;
            }
            var body = NcModel.Instance.Db.Get<McBody> (id);
            if (!body.IsValid) {
                return null;
            }
            return File.ReadAllText (GetBodyPath (id));
        }

        public static void Delete (int id)
        {
            if (0 == id) {
                return;
            }
            var body = NcModel.Instance.Db.Get<McBody> (id);
            body.IsValid = false;
            body.Update ();
            File.Delete (GetBodyPath (id));
            body.Delete ();
        }
    }
}