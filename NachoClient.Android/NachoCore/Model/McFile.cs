﻿using SQLite;
using System;
using System.IO;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class McFile : McObject
    {
        [Indexed]
        public string DisplayName { get; set; }

        public string SourceApplication { get; set; }

        public string LocalFileName { get; set; }

        public override int Delete ()
        {
            File.Delete (Path.Combine (BackEnd.Instance.AttachmentsDir, LocalFileName));
            return base.Delete ();
        }

        public static List<McFile> QueryAllFiles ()
        {
            return BackEnd.Instance.Db.Query<McFile> ("SELECT * FROM McFile ORDER BY DisplayName");
        }
    }
}
