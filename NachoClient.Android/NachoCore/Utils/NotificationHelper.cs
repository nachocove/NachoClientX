//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class NotificationHelper
    {
        public static bool ShouldNotifyEmailMessage (McEmailMessage emailMessage, McAccount account)
        {
            NcAssert.True (emailMessage.AccountId == account.Id);
            var config = account.NotificationConfiguration;
            if (config.HasFlag (McAccount.NotificationConfigurationEnum.ALLOW_INBOX_64)) {
                return true;
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

