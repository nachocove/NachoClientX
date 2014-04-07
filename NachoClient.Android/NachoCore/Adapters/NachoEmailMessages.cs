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
    public class NachoEmailMessages : INachoEmailMessages
    {
        List<List<McEmailMessage>> threadList;
        McFolder folder;

        public NachoEmailMessages (McFolder folder)
        {
            this.folder = folder;
            Refresh ();
        }

        public void Refresh ()
        {
            List<McEmailMessage> list = McEmailMessage.QueryActiveMessages (folder.AccountId, folder.Id).OrderByDescending (c => c.DateReceived).ToList ();
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
