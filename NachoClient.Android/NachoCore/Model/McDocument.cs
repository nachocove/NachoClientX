using SQLite;
using System;
using System.IO;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class McDocument : McAbstrObject, IFilesViewItem
    {
        [Indexed]
        public string DisplayName { get; set; }

        public string SourceApplication { get; set; }

        public string LocalFileName { get; set; }

        public string FilePath ()
        {
            return Path.Combine (NcModel.Instance.FilesDir, Id.ToString(), DisplayName);
        }

        public override int Delete ()
        {
            File.Delete (FilePath ());
            return base.Delete ();
        }

        public static List<McDocument> QueryAllFiles ()
        {
            return NcModel.Instance.Db.Query<McDocument> ("SELECT * FROM McFile ORDER BY DisplayName");
        }
    }
}
