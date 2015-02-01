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
        public enum DraftType {
            Email,
            Calendar,
        }

        //Leave this commented out until we are allowing users to edit draft messages 
        public static bool IsDraftsFolder (McFolder folder)
        {
//            if (McFolder.GetDefaultDraftsFolder (folder.AccountId).Id == folder.Id) {
//                return true;
//            }
            return false; 
        }

        public static DraftType FolderToDraftType (McFolder folder)
        {
//            NcAssert.True (IsDraftsFolder (folder));
//            if (Xml.FolderHierarchy.TypeCode.UserCreatedCal_13 == folder.Type) {
//                return DraftsHelper.DraftType.Calendar;
//            }
            return DraftsHelper.DraftType.Email;
        }

        public static List<McEmailMessage> GetEmailDrafts (int accountId)
        {
            List<McEmailMessage> emailDrafts = McEmailMessage.QueryByFolderId<McEmailMessage> (accountId, McFolder.GetDefaultDraftsFolder (accountId).Id);
            return emailDrafts;
        }
    }
}

