//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using MimeKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;

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

            McEmailAddress fromEmailAddress;
            if (McEmailAddress.Get (folder.AccountId, emailMessage.From, out fromEmailAddress)) {
                emailMessage.FromEmailAddressId = fromEmailAddress.Id;
                emailMessage.cachedFromLetters = EmailHelper.Initials (emailMessage.From);
                emailMessage.cachedFromColor = fromEmailAddress.ColorIndex;
            } else {
                emailMessage.FromEmailAddressId = 0;
                emailMessage.cachedFromLetters = "";
                emailMessage.cachedFromColor = 1;
            }

            emailMessage.SenderEmailAddressId = McEmailAddress.Get (folder.AccountId, emailMessage.Sender);
            emailMessage.ToEmailAddressId = McEmailAddress.GetList (folder.AccountId, emailMessage.To);
            emailMessage.CcEmailAddressId = McEmailAddress.GetList (folder.AccountId, emailMessage.Cc);

            NcModel.Instance.RunInTransaction (() => {
                if ((0 != emailMessage.FromEmailAddressId) || (0 < emailMessage.ToEmailAddressId.Count)) {
                    if (!folder.IsJunkFolder ()) {
                        NcContactGleaner.GleanContactsHeaderPart1 (emailMessage);
                    }
                }

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
                    folder.UpdateLink (emailMessage);
                    emailMessage.Update ();
                }
            });
            return emailMessage;
        }
    }
}
