//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;
using NachoCore.Brain;

namespace NachoCore
{
    public class NachoDeferredEmailMessages : INachoEmailMessages
    {
        List<List<McEmailMessage>> threadList;

        public NachoDeferredEmailMessages ()
        {
            Refresh ();
        }

        public void Refresh ()
        {
            List<McEmailMessage> list = McEmailMessage.QueryDeferredMessagesAllAccounts ().OrderByDescending (c => c.DateReceived).ToList ();
            if (null == list) {
                list = new List<McEmailMessage> ();
            }
            threadList = NcMessageThreads.ThreadByConversation (list);
        }

        public int Count ()
        {
            return threadList.Count;
        }

        public List<McEmailMessage> GetEmailThread (int i)
        {
            var t = threadList.ElementAt (i);
            return t;
        }
    }
}
