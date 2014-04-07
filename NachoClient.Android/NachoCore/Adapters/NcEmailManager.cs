//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore;
using NachoCore.Model;
using NachoClient;

namespace NachoCore
{
    public class NcEmailManager
    {
        public NcEmailManager ()
        {
        }

        public static INachoEmailMessages Inbox()
        {
            var email = new NachoFolders (NachoFolders.FilterForEmail);
            for (int i = 0; i < email.Count (); i++) {
                McFolder f = email.GetFolder (i);
                if (f.DisplayName.Equals ("Inbox")) {
                    return new NachoEmailMessages (f);
                }
            }
            return new MissingFolder ();
        }

        protected class MissingFolder : INachoEmailMessages
        {
            public int Count()
            {
                return 0;
            }

            public void Refresh()
            {
            }

            public List<McEmailMessage> GetEmailThread (int i)
            {
                NachoAssert.CaseError ();
                return null;
            }
        }
    }
}

