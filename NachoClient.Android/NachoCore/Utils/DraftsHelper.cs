//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using MimeKit;
using System.Text;
using NachoCore.Model;
using NachoCore.ActiveSync;


namespace NachoCore.Utils
{
    public class DraftsHelper
    {
        //Will need this once we're saving a draft.
        public static McFolder GetOrCreateEmailDraftsFolder (int accountId)
        {
            if (null == McFolder.GetEmailDraftsFolder (accountId)) {
                var deviceDraftsFolder = McFolder.Create (accountId, true, false, true, "0",
                    McFolder.ClientOwned_EmailDrafts, "Drafts",
                    Xml.FolderHierarchy.TypeCode.UserCreatedMail_12);
                deviceDraftsFolder.Insert ();
                return deviceDraftsFolder;
            }
            return McFolder.GetEmailDraftsFolder (accountId);
        }
    }
}

