using System;
using System.IO;

namespace NachoCore.Model
{
    // TODO - copy paste from McBody here. Need to unify descriptor + file logic.
    public class McPortrait : McAbstrObject
    {
        public bool IsValid { get; set; }

        public byte[] Body {
            get { return Get (Id); }
        }

        public string PortraitPath {
            get { return GetPortraitPath (Id); }
        }

        public static string GetPortraitPath (int id)
        {
            if (0 == id) {
                return null;
            }
            return Path.Combine (NcModel.Instance.PortraitsDir, id.ToString ());
        }

        public static McPortrait SaveStart ()
        {
            var portrait = new McPortrait ();
            portrait.IsValid = false;
            portrait.Insert ();
            return portrait;
        }

        public void SaveDone ()
        {
            IsValid = true;
            Update ();
        }

        public static McPortrait Save (byte[] content)
        {
            var portrait = SaveStart ();
            File.WriteAllBytes (GetPortraitPath (portrait.Id), content);
            portrait.SaveDone ();
            return portrait;
        }

        public static byte[] Get (int id)
        {
            if (0 == id) {
                return null;
            }
            var portrait = NcModel.Instance.Db.Get<McPortrait> (id);
            if (!portrait.IsValid) {
                return null;
            }
            return File.ReadAllBytes (GetPortraitPath (id));
        }

        public static void Delete (int id)
        {
            if (0 == id) {
                return;
            }
            var portrait = McPortrait.QueryById<McPortrait> (id);
            portrait.IsValid = false;
            portrait.Update ();
            File.Delete (GetPortraitPath (id));
            portrait.Delete ();
        }
    }
}
