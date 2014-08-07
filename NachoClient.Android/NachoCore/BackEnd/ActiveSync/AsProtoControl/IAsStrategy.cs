//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public enum PickActionEnum { Sync, Ping, QOop, Fetch, Wait };
    public interface IAsStrategy
    {
        // FIXME - get rid of ReportSyncResult.
        void ReportSyncResult (List<McFolder> folders);
        Tuple<uint, List<Tuple<McFolder, List<McPending>>>> SyncKit ();

        // revised API below this line.
        Tuple<PickActionEnum, object> Pick ();
    }
}

