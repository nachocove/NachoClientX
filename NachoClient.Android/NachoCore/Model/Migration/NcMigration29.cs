//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Threading;
using NachoCore.Utils;

namespace NachoCore.Model
{
    // This migration copies
    public class NcMigration29 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            int count = Db.Table<McEmailMessage> ().Count () +
                        Db.Table<McEmailAddress> ().Count ();
            return count;
        }

        public override void Run (CancellationToken token)
        {
            var emailMessageSrcCols = new List<string> () {
                "Id",
                "AccountId",
                "TimesRead",
                "SecondsRead",
                "ScoreIsRead",
                "ScoreIsReplied",
            };
            var emailMessageDstCols = new List<string> () {
                "ParentId",
                "AccountId",
                "TimesRead",
                "SecondsRead",
                "IsRead",
                "IsReplied",
            };
            var cmd = String.Format ("INSERT INTO McEmailMessageScore ({0}) SELECT {1} FROM McEmailMessage",
                          String.Join (",", emailMessageDstCols), String.Join (",", emailMessageSrcCols));
            var rows = Db.Execute (cmd);
            UpdateProgress (rows);

            var emailAddressSrcCols = new List<string> () {
                "Id",
                "AccountId",
                "EmailsReceived",
                "EmailsRead",
                "EmailsReplied",
                "EmailsArchived",
                "EmailsDeleted",
                "ToEmailsReceived",
                "ToEmailsRead",
                "ToEmailsReplied",
                "ToEmailsArchived",
                "ToEmailsDeleted",
                "CcEmailsReceived",
                "CcEmailsRead",
                "CcEmailsReplied",
                "CcEmailsArchived",
                "CcEmailsDeleted",
                "EmailsSent",
            };
            var emailAddressDstCols = new List<string> () {
                "ParentId",
                "AccountId",
                "EmailsReceived",
                "EmailsRead",
                "EmailsReplied",
                "EmailsArchived",
                "EmailsDeleted",
                "ToEmailsReceived",
                "ToEmailsRead",
                "ToEmailsReplied",
                "ToEmailsArchived",
                "ToEmailsDeleted",
                "CcEmailsReceived",
                "CcEmailsRead",
                "CcEmailsReplied",
                "CcEmailsArchived",
                "CcEmailsDeleted",
                "EmailsSent",
            };
            cmd = String.Format ("INSERT INTO McEmailAddressScore ({0}) SELECT {1} FROM McEmailAddress",
                String.Join (",", emailAddressDstCols), String.Join (",", emailAddressSrcCols));
            rows = Db.Execute (cmd);
            UpdateProgress (rows);
        }
    }
}

