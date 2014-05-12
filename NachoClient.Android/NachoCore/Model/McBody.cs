using System;
using System.IO;

namespace NachoCore.Model
{
    public class McBody : McObject
    {
        // FIXME - we should carry the encoding type (RTF, Mime, etc) here.
        public bool IsValid { get; set; }

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