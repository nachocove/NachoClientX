//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using MimeKit;
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
            // If the server attempts to overwrite, delete the pre-existing record first.
            var eMsg = McEmailMessage.QueryByServerId<McEmailMessage> (folder.AccountId, xmlServerId.Value);
            if (Xml.AirSync.Add == command.Name.LocalName && null != eMsg) {
                eMsg.Delete ();
                eMsg = null;
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

            emailMessage.FromEmailAddressId = McEmailAddress.Get (folder.AccountId, emailMessage.From);
            emailMessage.SenderEmailAddressId = McEmailAddress.Get (folder.AccountId, emailMessage.Sender);
            emailMessage.ToEmailAddressId = McEmailAddress.GetList (folder.AccountId, emailMessage.To);
            emailMessage.CcEmailAddressId = McEmailAddress.GetList (folder.AccountId, emailMessage.Cc);

            bool justCreated = false;
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
