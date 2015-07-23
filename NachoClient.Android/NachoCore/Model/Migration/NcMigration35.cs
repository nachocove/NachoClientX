//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using System.Linq;

namespace NachoClient.AndroidClient
{
    public class NcMigration35 : NcMigration
    {
        public override void Run (System.Threading.CancellationToken token)
        {
            var emails = NcModel.Instance.Db.Table<McEmailMessage> ();
            foreach (McEmailMessage email in emails) {
                Db.Execute ("UPDATE McEmailMessage SET ImapUid = ? WHERE Id = ?",
                    ImapMessageUid(email.ServerId), email.Id);
            }
        }

        private static uint ImapMessageUid (string MessageServerId)
        {
            return UInt32.Parse (MessageServerId.Split (':') [1]);
        }
    }
}
