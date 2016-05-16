//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore;
using System.IO;

namespace NachoClient.AndroidClient
{
    public class NcMigration52 : NcMigration
    {
        public NcMigration52 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            var q1 = "UPDATE McChatMessage SET MimeMessageId = (SELECT MessageId FROM McEmailMessage m WHERE m.Id = McChatMessage.MessageId)";
            NcModel.Instance.Db.Execute (q1);
            var q2 = "UPDATE McChatMessage SET IsLatestDuplicate = NOT EXISTS(SELECT cm2.* FROM McChatMessage cm2 WHERE McChatMessage.ChatId = cm2.ChatId AND McChatMessage.MimeMessageId = cm2.MimeMessageId AND cm2.MessageId > McChatMessage.MessageId)";
            NcModel.Instance.Db.Execute (q2);
        }
    }
}

