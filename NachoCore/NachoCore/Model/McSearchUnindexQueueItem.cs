//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore
{
    public class McSearchUnindexQueueItem : McAbstrObjectPerAcc
    {
        public string DocumentId { get; set; }

        public static List<McSearchUnindexQueueItem> Query (int maxItems)
        {
            var sql = "SELECT * FROM McSearchUnindexQueueItem ORDER BY Id ASC LIMIT ?";
            return NcModel.Instance.Db.Query<McSearchUnindexQueueItem> (sql, maxItems);
        }
    }
}
