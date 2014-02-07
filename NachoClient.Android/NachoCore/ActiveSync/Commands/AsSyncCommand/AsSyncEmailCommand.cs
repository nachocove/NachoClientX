//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public partial class AsSyncCommand : AsCommand
    {
        public void ServerSaysChangeEmailItem (XElement command, McFolder folder)
        {
        }

        private McEmailMessage ServerSaysAddEmail (XElement command, McFolder folder)
        {
            XNamespace email2Ns = Xml.Email2.Ns;

            var serverId = command.Element (m_ns + Xml.AirSync.ServerId).Value;
            // MoveItems should make the server send an Add/Delete that we want to ignore.
            // If we see an Add with a pre-existing ServerId in the DB, then we ignore the Add Op.
            // In 0.2, be more precise about dropping these Add/Deletes.
            if (BackEnd.Instance.Db.Table<McEmailMessage> ().Any (x =>
                x.AccountId == DataSource.Account.Id &&
                x.ServerId == serverId)) {
                return null;
            }

            IEnumerable<XElement> xmlAttachments = null;
            var emailMessage = new McEmailMessage {
                AccountId = DataSource.Account.Id,
                ServerId = serverId,
            };

            var appData = command.Element (m_ns + Xml.AirSync.ApplicationData);
            foreach (var child in appData.Elements()) {
                switch (child.Name.LocalName) {
                case Xml.AirSyncBase.Attachments:
                    xmlAttachments = child.Elements (m_baseNs + Xml.AirSyncBase.Attachment);
                    break;
                case Xml.AirSyncBase.Body:
                    emailMessage.BodyType = child.Element (m_baseNs + Xml.AirSyncBase.Type).Value;
                    var bodyElement = child.Element (m_baseNs + Xml.AirSyncBase.Data);
                    // NOTE: We have seen EstimatedDataSize of 0 and no Truncate here.
                    if (null != bodyElement) {
                        var saveAttr = bodyElement.Attributes ().SingleOrDefault (x => x.Name == "nacho-body-id");
                        if (null != saveAttr) {
                            emailMessage.BodyId = int.Parse (saveAttr.Value);
                        } else {
                            var body = new McBody ();
                            body.Body = bodyElement.Value; 
                            body.Insert ();
                            emailMessage.BodyId = body.Id;
                        }
                    } else {
                        emailMessage.BodyId = 0;
                        Console.WriteLine ("Truncated message from server.");
                    }
                    break;
                case Xml.Email.To:
                    // TODO: Append
                    emailMessage.To = child.Value;
                    break;
                case Xml.Email.Cc:
                    // TODO: Append
                    emailMessage.Cc = child.Value;
                    break;
                case Xml.Email.From:
                    emailMessage.From = child.Value;
                    break;
                case Xml.Email.ReplyTo:
                    emailMessage.ReplyTo = child.Value;
                    break;
                case Xml.Email.Subject:
                    emailMessage.Subject = child.Value;
                    break;
                case Xml.Email.DateReceived:
                    try {
                        emailMessage.DateReceived = DateTime.Parse (child.Value);
                    } catch {
                        // FIXME - just log it.
                    }
                    break;
                case Xml.Email.DisplayTo:
                    emailMessage.DisplayTo = child.Value;
                    break;
                case Xml.Email.Importance:
                    try {
                        emailMessage.Importance = UInt32.Parse (child.Value);
                    } catch {
                        // FIXME - just log it.
                    }
                    break;
                case Xml.Email.Read:
                    if ("1" == child.Value) {
                        emailMessage.IsRead = true;
                    } else {
                        emailMessage.IsRead = false;
                    }
                    break;
                case Xml.Email.MessageClass:
                    emailMessage.MessageClass = child.Value;
                    break;
                case Xml.Email.ThreadTopic:
                    emailMessage.ThreadTopic = child.Value;
                    break;
                case Xml.Email.Sender:
                    emailMessage.Sender = child.Value;
                    break;
                case Xml.Email2.ReceivedAsBcc:
                    if ("1" == child.Value) {
                        emailMessage.ReceivedAsBcc = true;
                    } else {
                        emailMessage.ReceivedAsBcc = false;
                    }
                    break;
                case Xml.Email2.ConversationId:
                    // FIXME: Will be base64 string soon
//                    emailMessage.ConversationId = child.Value;
                    break;
                }
            }

            // We handle the illegal case (GOOG "All" folder) where the ServerId is used twice
            // on the 2nd insert of the same message.
            var existingEmailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ()
                .SingleOrDefault (x => 
                    x.AccountId == emailMessage.AccountId &&
                    x.ServerId == emailMessage.ServerId);
            int emailMessageId;
            if (null == existingEmailMessage) {
                emailMessage.Insert ();
                emailMessageId = emailMessage.Id;
            } else {
                emailMessageId = existingEmailMessage.Id;
            }
            var map = new McMapFolderItem (DataSource.Account.Id) {
                ItemId = emailMessageId,
                FolderId = folder.Id,
                ClassCode = (uint)McItem.ClassCodeEnum.Email,
            };
            map.Insert ();
            if (null != existingEmailMessage) {
                return null;
            }
            if (null != xmlAttachments) {
                foreach (XElement xmlAttachment in xmlAttachments) {
                    // Create & save the attachment record.
                    var attachment = new McAttachment {
                        AccountId = emailMessage.AccountId,
                        EmailMessageId = emailMessage.Id,
                        IsDownloaded = false,
                        PercentDownloaded = 0,
                        IsInline = false,
                        EstimatedDataSize = uint.Parse (xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.EstimatedDataSize).Value),
                        FileReference = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.FileReference).Value,
                        Method = uint.Parse (xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.Method).Value),
                    };
                    var displayName = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.DisplayName);
                    if (null != displayName) {
                        attachment.DisplayName = displayName.Value;
                    }
                    var contentLocation = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.ContentLocation);
                    if (null != contentLocation) {
                        attachment.ContentLocation = contentLocation.Value;
                    }
                    var contentId = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.ContentId);
                    if (null != contentId) {
                        attachment.ContentId = contentId.Value;
                    }
                    var isInline = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.IsInline);
                    if (null != isInline) {
                        attachment.IsInline = ParseXmlBoolean (isInline);
                    }
                    var xmlUmAttDuration = xmlAttachment.Element (email2Ns + Xml.Email2.UmAttDuration);
                    if (null != xmlUmAttDuration) {
                        attachment.VoiceSeconds = uint.Parse (xmlUmAttDuration.Value);
                    }
                    var xmlUmAttOrder = xmlAttachment.Element (email2Ns + Xml.Email2.UmAttOrder);
                    if (null != xmlUmAttOrder) {
                        attachment.VoiceOrder = int.Parse (xmlUmAttOrder.Value);
                    }
                    attachment.Insert ();
                }
            }
            return emailMessage;
        }
    }
}
