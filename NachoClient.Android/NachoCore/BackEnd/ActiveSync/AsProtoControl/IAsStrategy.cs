//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public interface IAsStrategy
    {
        bool RequestQuickFetch { set; get; }

        void ReportSyncResult (List<McFolder> folders);
        Tuple<uint, List<Tuple<McFolder, List<McPending>>>> SyncKit ();
        bool IsMoreSyncNeeded ();
        IEnumerable<McFolder> PingKit ();
    }
}

