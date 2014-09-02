//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public partial class AsSyncCommand : AsCommand
    {
        public static McEmailMessage ServerSaysAddOrChangeEmail (XElement command, McFolder folder)
        {   
            AsHelpers aHelp = new AsHelpers ();
            var r = aHelp.ParseEmail (Ns, command, folder);
            McEmailMessage emailMessage = r.GetValue<McEmailMessage> ();

            bool justCreated = false;

            var eMsg = McAbstrFolderEntry.QueryByServerId<McEmailMessage> (folder.AccountId, emailMessage.ServerId);
            if (null == eMsg) {
                justCreated = true;
                emailMessage.AccountId = folder.AccountId;
            }

            if (justCreated) {
                emailMessage.Insert ();
            } else {
                emailMessage.AccountId = folder.AccountId;
                emailMessage.Id = eMsg.Id;
                emailMessage.Update ();
            }

            folder.Link (emailMessage);
            if (!justCreated) {
                return null;
            }
            aHelp.InsertAttachments (emailMessage);
            return emailMessage;
        }
    }
}
