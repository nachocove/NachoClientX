//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using NachoCore.Brain;

namespace NachoCore.Model
{
    public class McEmailAddressScoreSyncInfo : McAbstrObjectPerAcc
    {
        // Id of the corresponding McEmailAddress
        [Indexed]
        public Int64 EmailAddressId { get; set; }

        // Number of emails receivied
        public int EmailsReceived { get; set; }

        // Number of emails read
        public int EmailsRead { get; set; }

        // Number of emails replied
        public int EmailsReplied { get; set; }

        // Number of emails archived
        public int EmailsArchived { get; set; }

        // Number of emails sent to this contact
        public int EmailsSent { get; set; }

        // Number of emails deleted without being read
        public int EmailsDeleted { get; set; }

        public McEmailAddressScoreSyncInfo ()
        {
            EmailAddressId = 0;
            EmailsReceived = 0;
            EmailsRead = 0;
            EmailsReplied = 0;
            EmailsArchived = 0;
            EmailsSent = 0;
            EmailsDeleted = 0;
        }

        public void InsertByBrain ()
        {
            int rc = Insert ();
            if (0 < rc) {
                NcBrain.SharedInstance.McEmailAddressScoreSyncInfo.Insert.Click ();
            }
        }

        public void UpdateByBrain ()
        {
            int rc = Update ();
            if (0 < rc) {
                NcBrain.SharedInstance.McEmailAddressScoreSyncInfo.Update.Click ();
            }
        }

        public void DeleteByBrain ()
        {
            int rc = Delete ();
            if (0 < rc) {
                NcBrain.SharedInstance.McEmailAddressScoreSyncInfo.Delete.Click ();
            }
        }
    }
}

