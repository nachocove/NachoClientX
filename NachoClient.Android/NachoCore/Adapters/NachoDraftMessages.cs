//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Brain;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    public class NachoDraftMessages : NachoEmailMessagesBase, INachoEmailMessages
    {
        McFolder folder;

        List<McEmailMessageThread> threadList;

        public NachoDraftMessages (McFolder folder)
        {
            this.folder = folder;
            List<int> adds;
            List<int> deletes;
            Refresh (out adds, out deletes);
        }

        public override bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryActiveMessageItems (folder.AccountId, folder.Id, false);
            var threads = NcMessageThreads.ThreadByMessage (list);
            if (NcMessageThreads.AreDifferent (threadList, threads, out adds, out deletes)) {
                threadList = threads;
                return true;
            }
            return false;
        }

        public override int Count ()
        {
            return threadList.Count;
        }

        public override McEmailMessageThread GetEmailThread (int i)
        {
            var t = threadList.ElementAt (i);
            t.Source = this;
            return t;
        }

        public override List<McEmailMessageThread> GetEmailThreadMessages (int id)
        {
            var thread = new List<McEmailMessageThread> ();
            var m = new McEmailMessageThread ();
            m.FirstMessageId = id;
            m.MessageCount = 1;
            thread.Add (m);
            return thread;
        }

        public override string DisplayName ()
        {
            if (folder.IsClientOwnedOutboxFolder ()) {
                return "Outbox";
            }
            if (folder.IsClientOwnedDraftsFolder ()) {
                return "Drafts";
            }
            NachoCore.Utils.NcAssert.CaseError (folder.DisplayName);
            return "";
        }

        public override bool HasOutboxSemantics ()
        {
            return folder.IsClientOwnedOutboxFolder ();
        }

        public override bool HasDraftsSemantics ()
        {
            return folder.IsClientOwnedDraftsFolder ();
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return account.Id == folder.AccountId;
        }
    }
}

