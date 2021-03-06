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
            string cmdNameWithAccount = string.Format ("AsSyncCommand({0})", folder.AccountId);
            var xmlServerId = command.Element (Ns + Xml.AirSync.ServerId);
            if (null == xmlServerId || null == xmlServerId.Value || string.Empty == xmlServerId.Value) {
                Log.Error (Log.LOG_AS, "{0}: ServerSaysAddOrChangeEmail: No ServerId present.", cmdNameWithAccount);
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
                Log.Error (Log.LOG_AS, "{0}: ServerSaysAddOrChangeEmail: Exception parsing: {1}", cmdNameWithAccount, ex.ToString ());
                if (null == emailMessage || null == emailMessage.ServerId || string.Empty == emailMessage.ServerId) {
                    emailMessage = new McEmailMessage () {
                        ServerId = xmlServerId.Value,
                    };
                }
                emailMessage.IsIncomplete = true;
            }
            bool justCreated = false;
            if (null == eMsg) {
                justCreated = true;
                emailMessage.AccountId = folder.AccountId;
            }

            NcModel.Instance.RunInTransaction (() => {
                if (justCreated) {
                    emailMessage.ParseIntentFromSubject ();
                    emailMessage.IsJunk = folder.IsJunkFolder ();
                    emailMessage.DetermineIfIsChat ();
                    emailMessage.DetermineIfIsAction (folder);

                    emailMessage.Insert ();
                    folder.Link (emailMessage);
                    aHelp.InsertAttachments (emailMessage);
                    if (emailMessage.IsChat) {
                        var result = BackEnd.Instance.DnldEmailBodyCmd (emailMessage.AccountId, emailMessage.Id, false);
                        if (result.isError ()) {
                            Log.Error (Log.LOG_AS, "{0}: ServerSaysAddOrChangeEmail: could not start download for chat message: {1}", cmdNameWithAccount, result);
                        }
                    }
                    if (emailMessage.IsAction) {
                        McAction.RunCreateActionFromMessageTask (emailMessage.Id);
                    }
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
                emailMessage.ProcessAfterReceipt ();

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
