//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    public interface INachoEmailMessages
    {
        int Count ();

        /// <summary>
        /// Refresh the email message list. Return true if there is changes; false otherrwise.
        /// </summary>
        bool Refresh (out List<int> adds, out List<int> deletes);

        // Returns the thread, not the messages
        McEmailMessageThread GetEmailThread (int i);

        // Need to use the same query to fetch the thread
        List<McEmailMessageThread> GetEmailThreadMessages (int i);

        string DisplayName ();

        NcResult StartSync ();

        INachoEmailMessages GetAdapterForThread (McEmailMessageThread thread);

        bool HasOutboxSemantics ();

        bool HasDraftsSemantics ();

        bool IsCompatibleWithAccount (McAccount account);
    }

    public static class NachoSyncResult
    {
        public static NcResult DoesNotSync ()
        {
            return NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
        }

        public static bool DoesNotSync (NcResult nr)
        {
            return nr.isError () && (NcResult.SubKindEnum.Error_ClientOwned == nr.SubKind);
        }
    }
}
