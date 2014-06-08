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
        public void ServerSaysChangeEmail (XElement command, McFolder folder)
        {
            ProcessEmailItem (command, folder, false);
        }

        private McEmailMessage ServerSaysAddEmail (XElement command, McFolder folder)
        {
            return ProcessEmailItem (command, folder, true);
        }

        public McEmailMessage ProcessEmailItem (XElement command, McFolder folder, bool isAdd)
        {
            bool justCreated = false;
            XNamespace email2Ns = Xml.Email2.Ns;

            var serverId = command.Element (m_ns + Xml.AirSync.ServerId).Value;
            // MoveItems should make the server send an Add/Delete that we want to ignore.
            // If we see an Add with a pre-existing ServerId in the DB, then we ignore the Add Op.
            // FIXME be more precise about dropping these Add/Deletes.
            var emailMessage = McFolderEntry.QueryByServerId<McEmailMessage> (folder.AccountId, serverId);
            if (null == emailMessage) {
                justCreated = true;
                emailMessage = new McEmailMessage {
                    AccountId = BEContext.Account.Id,
                    ServerId = serverId,
                };
            }

            IEnumerable<XElement> xmlAttachments = null;

            var appData = command.Element (m_ns + Xml.AirSync.ApplicationData);
            foreach (var child in appData.Elements()) {
                switch (child.Name.LocalName) {
                case Xml.AirSyncBase.Attachments:
                    xmlAttachments = child.Elements (m_baseNs + Xml.AirSyncBase.Attachment);
                    break;
                case Xml.AirSyncBase.Body:
                    emailMessage.BodyType = child.Element (m_baseNs + Xml.AirSyncBase.Type).Value.ToInt();
                    var bodyElement = child.Element (m_baseNs + Xml.AirSyncBase.Data);
                    // NOTE: We have seen EstimatedDataSize of 0 and no Truncate here.
                    if (null != bodyElement) {
                        var saveAttr = bodyElement.Attributes ().Where (x => x.Name == "nacho-body-id").SingleOrDefault ();
                        if (null != saveAttr) {
                            emailMessage.BodyId = int.Parse (saveAttr.Value);
                        } else {
                            var body = McBody.Save(bodyElement.Value); 
                            emailMessage.BodyId = body.Id;
                        }
                    } else {
                        emailMessage.BodyId = 0;
                        Console.WriteLine ("Truncated message from server.");
                    }
                    break;

                case Xml.Email.Flag:
                    if (!child.HasElements) {
                        // This is the clearing of the Flag.
                        emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Cleared;
                    } else {
                        foreach (var flagPart in child.Elements()) {
                            switch (flagPart.Name.LocalName) {
                            case Xml.Email.Status:
                                try {
                                    uint statusValue = uint.Parse (flagPart.Value);
                                    if (2 < statusValue) {
                                        // FIXME log.
                                    } else {
                                        emailMessage.FlagStatus = statusValue;
                                    }
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Email.FlagType:
                                emailMessage.FlagType = flagPart.Value;
                                break;

                            case Xml.Tasks.StartDate:
                                try {
                                    emailMessage.FlagDeferUntil = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Tasks.UtcStartDate:
                                try {
                                    emailMessage.FlagUtcDeferUntil = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Tasks.DueDate:
                                try {
                                    emailMessage.FlagDue = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Tasks.UtcDueDate:
                                try {
                                    emailMessage.FlagUtcDue = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Tasks.ReminderSet:
                                try {
                                    int boolInt = int.Parse (flagPart.Value);
                                    if (0 == boolInt) {
                                        emailMessage.FlagReminderSet = false;
                                    } else if (1 == boolInt) {
                                        emailMessage.FlagReminderSet = true;
                                    } else {
                                        // FIXME log.
                                    }
                                } catch {
                                    // FIXME log.
                                }
                                break;
                               
                            case Xml.Tasks.Subject:
                            // Ignore. This SHOULD be the same as the message Subject.
                                break;

                            case Xml.Tasks.ReminderTime:
                                try {
                                    emailMessage.FlagReminderTime = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Email.CompleteTime:
                                try {
                                    emailMessage.FlagCompleteTime = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Tasks.DateCompleted:
                                try {
                                    emailMessage.FlagDateCompleted = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;
                            }
                        }
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
                        emailMessage.Importance = (NcImportance)uint.Parse (child.Value);
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
                    emailMessage.ConversationId = child.Value;
                    break;
                default:
                    Log.Warn (Log.LOG_AS, "ProcessEmailItem UNHANDLED: " + child.Name.LocalName + " value=" + child.Value);
                    break;
                }
            }

            if (justCreated) {
                /// SCORING - Score based on the # of emails in the thread.
                List<McEmailMessage> emailThread = 
                    McEmailMessage.QueryByThreadTopic (emailMessage.AccountId, emailMessage.ThreadTopic);
                if (0 < emailThread.Count) {
                    emailMessage.ContentScore += emailThread.Count;
                    Log.Info (Log.LOG_BRAIN, "SCORE: ThreadTopic={0}, Subject={1}, ContentScore={2}", 
                        emailMessage.ThreadTopic, emailMessage.Subject, emailMessage.ContentScore);
                }
                emailMessage.Insert ();
            } else {
                emailMessage.Update ();
            }

            // We handle the illegal case (GOOG "All" folder) where the ServerId is used twice
            // on the 2nd insert of the same message.
            // FIXME Ignore retval only if already-there-in-dir.
            folder.Link (emailMessage);
            if (!justCreated) {
                // We're done. Attachments are process on Add, not Change.
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
