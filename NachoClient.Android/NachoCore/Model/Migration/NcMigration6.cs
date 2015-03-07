//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration6 : NcMigration
    {
        public NcMigration6 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return Db.Table<McEmailMessage> ().Where (x => x.ConversationId == null).Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var emailMessage in Db.Table<McEmailMessage>().Where (x => x.ConversationId == null)) {
                token.ThrowIfCancellationRequested ();
                NcModel.Instance.RunInTransaction (() => {
                    emailMessage.ConversationId = System.Guid.NewGuid ().ToString ();
                    emailMessage.Update();
                    UpdateProgress (1);
                });
            }
        }
    }
}

