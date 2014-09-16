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
            var xmlServerId = command.Element (Ns + Xml.AirSync.ServerId);
            if (null == xmlServerId || null == xmlServerId.Value || string.Empty == xmlServerId.Value) {
                Log.Error (Log.LOG_AS, "ServerSaysAddOrChangeEmail: No ServerId present.");
                return null;
            }
            McEmailMessage emailMessage = null;
            AsHelpers aHelp = new AsHelpers ();
            try {
                var r = aHelp.ParseEmail (Ns, command, folder);
                emailMessage = r.GetValue<McEmailMessage> ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_AS, "ServerSaysAddOrChangeEmail: Exception parsing: {0}", ex.ToString ());
                if (null == emailMessage || null == emailMessage.ServerId || string.Empty == emailMessage.ServerId) {
                    emailMessage = new McEmailMessage () {
                        ServerId = xmlServerId.Value,
                    };
                }
                emailMessage.IsIncomplete = true;
            }
            bool justCreated = false;

            var eMsg = McAbstrFolderEntry.QueryByServerId<McEmailMessage> (folder.AccountId, emailMessage.ServerId);
            if (null == eMsg) {
                justCreated = true;
                emailMessage.AccountId = folder.AccountId;
            }
            if (justCreated) {
                emailMessage.Insert ();
                folder.Link (emailMessage);
                aHelp.InsertAttachments (emailMessage);
            } else {
                emailMessage.AccountId = folder.AccountId;
                emailMessage.Id = eMsg.Id;
                emailMessage.Update ();
            }
            return emailMessage;
        }
    }
}
