//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using NachoCore.Utils;
using System.Threading;
using MimeKit;
using MailKit;
using MailKit.Search;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Brain;
using NachoCore.Model;
using MailKit.Security;
using System.Security.Cryptography.X509Certificates;

namespace NachoCore.IMAP
{
    public class ImapSyncCommand : ImapCommand
    {
        SyncKit SyncKit;

        public ImapSyncCommand (IBEContext beContext, ImapClient imap, SyncKit syncKit) : base (beContext, imap)
        {
            SyncKit = syncKit;
        }

        public override void Execute (NcStateMachine sm)
        {
            NcTask.Run (() => {
                IList<IMessageSummary> summaries = null;
                try {
                    // TODO - put inside a function that returns Event.
                    if (!Client.IsConnected) {
                        sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.ReConn, "IMAPSYNCCONN");
                        return;
                    }
                    if (!SyncKit.MailKitFolder.IsOpen) {
                        FolderAccess access;
                        lock (Client.SyncRoot) {
                            access = SyncKit.MailKitFolder.Open (FolderAccess.ReadOnly, Cts.Token);
                        }
                        if (FolderAccess.None == access) {
                            sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPSYNCNOOPEN");
                            return;
                        }
                    }
                    switch (SyncKit.Method) {
                    case SyncKit.MethodEnum.Range:
                        lock (Client.SyncRoot) {
                            summaries = SyncKit.MailKitFolder.Fetch (
                                new UniqueIdRange (new UniqueId (SyncKit.MailKitFolder.UidValidity, SyncKit.Start),
                                    new UniqueId (SyncKit.MailKitFolder.UidValidity, SyncKit.Start + SyncKit.Span)),
                                SyncKit.Flags, Cts.Token);
                        }
                        break;
                    case SyncKit.MethodEnum.OpenOnly:
                        // Just load UID with SELECT.
                        sm.PostEvent ((uint)SmEvt.E.Success, "IMAPSYNCSUC");
                        return;
                    }
                } catch (OperationCanceledException) {
                    return;
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "ImapSyncCommand: Unexpected exception: {0}", ex.ToString ());
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPSYNCHARDX"); 
                }
                if (null != summaries && 0 < summaries.Count) {
                    foreach (var summary in summaries) {
                        // FIXME use NcApplyServerCommand framework.
                        ServerSaysAddOrChangeEmail (summary, SyncKit.Folder);
                        if (summary.UniqueId.Value.Id > SyncKit.Folder.ImapUidHighestUidSynced ||
                            summary.UniqueId.Value.Id < SyncKit.Folder.ImapUidLowestUidSynced) {
                            SyncKit.Folder = SyncKit.Folder.UpdateWithOCApply<McFolder> ((record) => {
                                var target = (McFolder)record;
                                target.ImapUidHighestUidSynced = Math.Max (summary.UniqueId.Value.Id, target.ImapUidHighestUidSynced);
                                target.ImapUidLowestUidSynced = Math.Min (summary.UniqueId.Value.Id, target.ImapUidLowestUidSynced);
                                return true;
                            });
                        }
                    }
                    BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                }
                if (NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == SyncKit.Folder.Type)
                {
                    var protocolState = BEContext.ProtocolState;
                    if (!protocolState.HasSyncedInbox) {
                        protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                            var target = (McProtocolState)record;
                            target.HasSyncedInbox = true;
                            return true;
                        });
                    }
                }
                sm.PostEvent ((uint)SmEvt.E.Success, "IMAPSYNCSUC");
            }, "ImapSyncCommand");
        }


        public McEmailMessage ServerSaysAddOrChangeEmail (IMessageSummary summary, McFolder folder)
        {
            var ServerId = summary.UniqueId; // FIXME

            if (null == ServerId || string.Empty == ServerId.Value.ToString ()) {
                Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: No ServerId present.");
                return null;
            }
            // If the server attempts to overwrite, delete the pre-existing record first.
            var eMsg = McEmailMessage.QueryByServerId<McEmailMessage> (folder.AccountId, ServerId.Value.ToString ());
            if (null != eMsg) {
                eMsg.Delete ();
                eMsg = null;
            }

            McEmailMessage emailMessage = null;
            try {
                var r = ParseEmail (summary);
                emailMessage = r.GetValue<McEmailMessage> ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: Exception parsing: {0}", ex.ToString ());
                if (null == emailMessage || null == emailMessage.ServerId || string.Empty == emailMessage.ServerId) {
                    emailMessage = new McEmailMessage () {
                        ServerId = summary.UniqueId.Value.ToString (),
                    };
                }
                emailMessage.IsIncomplete = true;
            }

            // TODO move the rest to parent class or into the McEmailAddress class before insert or update?
            NcModel.Instance.RunInTransaction (() => {
                if ((0 != emailMessage.FromEmailAddressId) || !String.IsNullOrEmpty(emailMessage.To)) {
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
                    // FIXME
                    // InsertAttachments (emailMessage);
                } else {
                    emailMessage.AccountId = folder.AccountId;
                    emailMessage.Id = eMsg.Id;
                    folder.UpdateLink (emailMessage);
                    emailMessage.Update ();
                }
            });

            if (!emailMessage.IsIncomplete) {
                // Extra work that needs to be done, but doesn't need to be in the same database transaction.
            }

            return emailMessage;
        }

        public NcResult ParseEmail (IMessageSummary summary)
        {
            var emailMessage = new McEmailMessage () {
                ServerId = summary.UniqueId.Value.Id.ToString (),
                AccountId = BEContext.Account.Id,
                Subject = summary.Envelope.Subject,
                InReplyTo = summary.Envelope.InReplyTo,
                // FIXME - Any error.
                // cachedHasAttachments = summary.Attachments.Any (),
                MessageID = summary.Envelope.MessageId,
                DateReceived = summary.InternalDate.HasValue ? summary.InternalDate.Value.UtcDateTime : DateTime.MinValue,
                FromEmailAddressId = 0,
                cachedFromLetters = "",
                cachedFromColor = 1,
            };

            // TODO: DRY this out. Perhaps via Reflection?
            if (summary.Envelope.To.Count > 0) {
                if (summary.Envelope.To.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} To entries in message.", summary.Envelope.To.Count);
                }
                emailMessage.To = ((MailboxAddress)summary.Envelope.To [0]).Address;
            }
            if (summary.Envelope.Cc.Count > 0) {
                if (summary.Envelope.Cc.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Cc entries in message.", summary.Envelope.Cc.Count);
                }
                emailMessage.Cc = ((MailboxAddress)summary.Envelope.Cc [0]).Address;
            }
            if (summary.Envelope.Bcc.Count > 0) {
                if (summary.Envelope.Bcc.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Bcc entries in message.", summary.Envelope.Bcc.Count);
                }
                emailMessage.Bcc = ((MailboxAddress)summary.Envelope.Bcc [0]).Address;
            }

            McEmailAddress fromEmailAddress;
            if (summary.Envelope.From.Count > 0) {
                if (summary.Envelope.From.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} From entries in message.", summary.Envelope.From.Count);
                }
                emailMessage.From = ((MailboxAddress)summary.Envelope.From [0]).Address;
                if (McEmailAddress.Get (BEContext.Account.Id, summary.Envelope.From [0] as MailboxAddress, out fromEmailAddress)) {
                    emailMessage.FromEmailAddressId = fromEmailAddress.Id;
                    emailMessage.cachedFromLetters = EmailHelper.Initials (emailMessage.From);
                    emailMessage.cachedFromColor = fromEmailAddress.ColorIndex;
                }
            }

            if (summary.Envelope.ReplyTo.Count > 0) {
                if (summary.Envelope.ReplyTo.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} ReplyTo entries in message.", summary.Envelope.ReplyTo.Count);
                }
                emailMessage.ReplyTo = ((MailboxAddress)summary.Envelope.ReplyTo [0]).Address;
            }
            if (summary.Envelope.Sender.Count > 0) {
                if (summary.Envelope.Sender.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Sender entries in message.", summary.Envelope.Sender.Count);
                }
                emailMessage.Sender = ((MailboxAddress)summary.Envelope.Sender [0]).Address;
                if (McEmailAddress.Get (BEContext.Account.Id, summary.Envelope.Sender [0] as MailboxAddress, out fromEmailAddress)) {
                    emailMessage.SenderEmailAddressId = fromEmailAddress.Id;
                }
            }
            if (null != summary.References && summary.References.Count > 0) {
                if (summary.References.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} References entries in message.", summary.References.Count);
                }
                emailMessage.References = summary.References [0];
            }

            if (summary.Flags.HasValue) {
                if (summary.Flags.Value != MessageFlags.None) {
                    if ((summary.Flags.Value & MessageFlags.Seen) == MessageFlags.Seen) {
                        emailMessage.IsRead = true;
                    }
                    // TODO Where do we set these flags?
                    if ((summary.Flags.Value & MessageFlags.Answered) == MessageFlags.Answered) {
                    }
                    if ((summary.Flags.Value & MessageFlags.Flagged) == MessageFlags.Flagged) {
                    }
                    if ((summary.Flags.Value & MessageFlags.Deleted) == MessageFlags.Deleted) {
                    }
                    if ((summary.Flags.Value & MessageFlags.Draft) == MessageFlags.Draft) {
                    }
                    if ((summary.Flags.Value & MessageFlags.Recent) == MessageFlags.Recent) {
                    }
                    if ((summary.Flags.Value & MessageFlags.UserDefined) == MessageFlags.UserDefined) {
                        // TODO See if these are handled by the summary.UserFlags
                    }
                }
            }
            if (null != summary.UserFlags && summary.UserFlags.Count > 0) {
                // TODO Where do we set these flags?
            }

            if (null != summary.Headers) {
                foreach (var header in summary.Headers) {
                    Log.Info (Log.LOG_IMAP, "IMAP header id {0} {1} {2}", header.Id, header.Field, header.Value);
                    switch (header.Id) {
                    case HeaderId.ContentClass:
                        emailMessage.ContentClass = header.Value;
                        break;

                    case HeaderId.Importance:
                        switch (header.Value) {
                        case "low":
                            emailMessage.Importance = NcImportance.Low_0;
                            break;

                        case "normal":
                            emailMessage.Importance = NcImportance.Normal_1;
                            break;

                        case "high":
                            emailMessage.Importance = NcImportance.High_2;
                            break;

                        default:
                            Log.Error (Log.LOG_IMAP, string.Format ("Unknown importance header value '{0}'", header.Value));
                            break;
                        }
                        break;
                    }
                }
            }

            if (summary.GMailThreadId.HasValue) {
                emailMessage.ConversationId = summary.GMailThreadId.Value.ToString ();
            }
            if ("" == emailMessage.MessageID && summary.GMailMessageId.HasValue) {
                emailMessage.MessageID = summary.GMailMessageId.Value.ToString ();
            }
            emailMessage.IsIncomplete = false;

            return NcResult.OK (emailMessage);
        }
    }
}
