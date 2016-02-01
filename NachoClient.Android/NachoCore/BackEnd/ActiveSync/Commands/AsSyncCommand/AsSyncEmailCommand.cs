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

            // TODO move the rest to parent class or into the McEmailAddress class before insert or update?
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

            NcModel.Instance.RunInTransaction (() => {
                bool justCreated = false;
                if (null == eMsg) {
                    justCreated = true;
                    emailMessage.AccountId = folder.AccountId;
                }
                if (justCreated) {
                    emailMessage.IsJunk = folder.IsJunkFolder ();
                    emailMessage.Insert ();
                    folder.Link (emailMessage);
                    aHelp.InsertAttachments (emailMessage);
                } else {
                    emailMessage = emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                        var target = (McEmailMessage)record;
                        target.AccountId = folder.AccountId;
                        target.Id = eMsg.Id;
                        return true;
                    });
                    folder.UpdateLink (emailMessage);

                }
            });

            if (!emailMessage.IsIncomplete) {

                // Extra work that needs to be done, but doesn't need to be in the same database transaction.

                if (emailMessage.ScoreStates.IsRead != emailMessage.IsRead) {
                    // Another client has remotely read / unread this email.
                    // TODO - Should be the average of now and last sync time. But last sync time does not exist yet
                    NcBrain.MessageReadStatusUpdated (emailMessage, DateTime.UtcNow, 60.0);
                }
                NcBrain.SharedInstance.ProcessOneNewEmail (emailMessage);

                // If this message is a cancellation notice, mark the event as cancelled.  (The server may
                // have already done this, but some servers don't.)
                if (emailMessage.IsMeetingCancelation && null != emailMessage.MeetingRequest) {
                    CalendarHelper.MarkEventAsCancelled (emailMessage.MeetingRequest);
                }
            }

            return emailMessage;
        }
    }
}
