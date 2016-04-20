//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace NachoCore.Model
{
    public class McMigration : McAbstrObject
    {
        public int Version { set; get; }

        public DateTime StartTime { set; get; }

        public int NumberOfTimesRan { set; get; }

        public int DurationMsec { set; get; }

        public int NumberOfItems { set; get; }

        public bool Finished { set; get; }

        public McMigration ()
        {
        }

        public static McMigration QueryLatestMigration ()
        {
            return NcModel.Instance.Db.Query<McMigration> (
                "SELECT * FROM McMigration ORDER BY Version DESC LIMIT 1" 
            ).SingleOrDefault ();
        }

        public static McMigration QueryByVersion (int version)
        {
            return NcModel.Instance.Db.Table<McMigration> ()
                .Where (x => x.Version == version)
                .SingleOrDefault ();
        }
    }
}

