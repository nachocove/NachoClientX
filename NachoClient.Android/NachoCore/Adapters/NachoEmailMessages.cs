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
        List<McEmailMessageThread> threadList;
        McFolder folder;

        public NachoEmailMessages (McFolder folder)
        {
            this.folder = folder;
            Refresh ();
        }

        public void Refresh ()
        {
            var list = McEmailMessage.QueryActiveMessageItems (folder.AccountId, folder.Id);
            if (null == list) {
                list = new List<NcEmailMessageIndex> ();
            }
            threadList = NcMessageThreads.ThreadByConversation (list);
        }

        public int Count ()
        {
            return threadList.Count;
        }

        public McEmailMessageThread GetEmailThread (int i)
        {
            var t = threadList.ElementAt (i);
            return t;
        }

        public string DisplayName ()
        {
            return folder.DisplayName;
        }

    }
}
