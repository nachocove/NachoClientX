//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using NachoCore.Brain;

namespace NachoCore.Model
{
    public class McEmailMessageScoreSyncInfo : McAbstrObjectPerAcc
    {
        // Id of the corresponding McEmailMessage
        [Indexed]
        public Int64 EmailMessageId { get; set; }

        // How many times the email is read
        public int TimesRead { get; set; }

        // How long the user read the email
        public int SecondsRead { get; set; }

        // Whether brain marks this email as has been read
        public bool ScoreIsRead { get; set; }

        // Whether brain marks this email as has been replied
        public bool ScoreIsReplied { get; set; }

        public McEmailMessageScoreSyncInfo ()
        {
            EmailMessageId = 0;
            TimesRead = 0;
            SecondsRead = 0;
            ScoreIsRead = false;
            ScoreIsReplied = false;
        }

        public void InsertByBrain ()
        {
            int rc = Insert ();
            if (0 < rc) {
                NcBrain.SharedInstance.McEmailMessageScoreSyncInfoCounters.Insert.Click ();
            }
        }

        public void UpdateByBrain ()
        {
            int rc = Update ();
            if (0 < rc) {
                NcBrain.SharedInstance.McEmailMessageScoreSyncInfoCounters.Update.Click ();
            }
        }

        public void DeleteByBrain ()
        {
            int rc = Delete ();
            if (0 < rc) {
                NcBrain.SharedInstance.McEmailMessageScoreSyncInfoCounters.Delete.Click ();
            }
        }
    }
}

