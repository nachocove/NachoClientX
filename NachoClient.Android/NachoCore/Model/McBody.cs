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
            return Path.Combine (BackEnd.Instance.BodiesDir, id.ToString ());
        }

        public static McBody Save (string content)
        {
            var body = new McBody ();
            body.IsValid = false;
            body.Insert ();
            File.WriteAllText (GetBodyPath (body.Id), content);
            body.IsValid = true;
            body.Update ();
            return body;
        }

        public static string Get (int id)
        {
            if (0 == id) {
                return null;
            }
            var body = BackEnd.Instance.Db.Get<McBody> (id);
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
            var body = BackEnd.Instance.Db.Get<McBody> (id);
            body.IsValid = false;
            body.Update ();
            File.Delete (GetBodyPath (id));
            body.Delete ();
        }
    }
}