//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;
using NachoCore.Brain;
using NachoCore.Utils;

namespace NachoCore
{
    public class UserInteractionEmailMessages : NachoEmailMessages
    {
        McContact contact;
        List<McEmailMessageThread> threadList;
        List<McEmailMessageThread> updatedThreadList;

        public UserInteractionEmailMessages (McContact contact)
        {
            this.contact = contact;
            threadList = new List<McEmailMessageThread> ();
        }

        public override bool BeginRefresh (out List<int> adds, out List<int> deletes)
        {
            adds = null;
            deletes = null;
            if (null == contact) {
                ClearCache ();
                threadList = new List<McEmailMessageThread> ();
                return true;
            }
            var list = McEmailMessage.QueryInteractions (contact);
            updatedThreadList = NcMessageThreads.ThreadByMessage (list);
            RemoveIgnoredMessages (updatedThreadList);
            return NcMessageThreads.AreDifferent (threadList, updatedThreadList, out adds, out deletes);
        }

        public override void CommitRefresh ()
        {
            ClearCache ();
            threadList = updatedThreadList;
            updatedThreadList = null;
        }

        public override void RemoveIgnoredMessages ()
        {
            RemoveIgnoredMessages (threadList);
        }

        public override bool HasBackgroundRefresh ()
        {
            return true;
        }

        public override void BackgroundRefresh (NachoMessagesRefreshCompletionDelegate completionAction)
        {
            if (null == contact) {
                threadList = new List<McEmailMessageThread> ();
                if (null != completionAction) {
                    completionAction (this, true, null, null);
                }
                return;
            }
            NcTask.Run (() => {
                var rawList = McEmailMessage.QueryInteractions (contact);
                var newThreadList = NcMessageThreads.ThreadByMessage (rawList);
                RemoveIgnoredMessages (newThreadList);
                NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                    List<int> adds = null;
                    List<int> deletes = null;
                    bool changed = NcMessageThreads.AreDifferent (threadList, newThreadList, out adds, out deletes);
                    if (changed) {
                        threadList = newThreadList;
                    }
                    if (null != completionAction) {
                        completionAction (this, changed, adds, deletes);
                    }
                });
            }, "UserInteractionEmailMessages.BackgroundRefresh");
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

        // Add messages make up the thread, just the user ones

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
            return "Interactions";
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return true;
        }
    }
}
