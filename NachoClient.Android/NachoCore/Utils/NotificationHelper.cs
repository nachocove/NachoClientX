//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.ActiveSync;

namespace NachoCore.Utils
{
    public class NotificationHelper
    {
        public static bool ShouldNotifyEmailMessage (McEmailMessage emailMessage)
        {
            var account = McAccount.QueryById<McAccount> (emailMessage.AccountId);

            var folders = McFolder.QueryByFolderEntryId<McEmailMessage> (account.Id, emailMessage.Id);
            foreach (var folder in folders) {
                if (Xml.FolderHierarchy.TypeCode.DefaultDeleted_4 == folder.Type) {
                    // Don't notify the user about messages that have been deleted.
                    return false;
                }
                if (folder.IsJunkFolder ()) {
                    // Don't notify the user about junk mail or spam
                    return false;
                }
            }

            var config = account.NotificationConfiguration;

            if (config.HasFlag (McAccount.NotificationConfigurationEnum.ALLOW_INBOX_64)) {
                foreach (var folder in folders) {
                    if (Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == folder.Type) {
                        return true;
                    }
                }
            }

            if (config.HasFlag (McAccount.NotificationConfigurationEnum.ALLOW_HOT_2) && emailMessage.isHot ()) {
                return true;
            }

            if (config.HasFlag (McAccount.NotificationConfigurationEnum.ALLOW_VIP_4)) {
                var emailAddress = NcEmailAddress.ParseMailboxAddressString (emailMessage.From);
                if (null != emailAddress) {
                    var contactList = McContact.QueryByEmailAddress (emailMessage.AccountId, emailAddress.Address);
                    foreach (var contact in contactList) {
                        if (contact.IsVip) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}

