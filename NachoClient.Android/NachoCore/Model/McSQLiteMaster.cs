//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace NachoCore.Model
{
    public class McSQLiteMaster
    {
//        CREATE TABLE sqlite_master (
//            type TEXT,
//            name TEXT,
//            tbl_name TEXT,
//            rootpage INTEGER,
//            sql TEXT
//        );
        public string type { get; set; }

        public string name { get; set; }

        public string tbl_name { get; set; }

        public int rootpage { get; set; }

        public string sql { get; set; }
             
        public static List<McSQLiteMaster> QueryAllTables ()
        {
            var all = NcModel.Instance.Db.Query<McSQLiteMaster> ("SELECT * FROM sqlite_master WHERE type= ?", "table");
            return all.ToList ();
        }

    }
}
