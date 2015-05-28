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
    public class ImapCommand : IImapCommand
    {
        protected IBEContext BEContext;
        protected ImapClient Client { get; set; }
        public CancellationTokenSource Cts { get; protected set; }

        public ImapCommand (IBEContext beContext, ImapClient imap)
        {
            Cts = new CancellationTokenSource ();
            BEContext = beContext;
            Client = imap;
        }

        public virtual void Execute (NcStateMachine sm)
        {
        }

        public virtual void Cancel ()
        {
            Cts.Cancel ();
        }
    }

    public class ImapFolderSyncCommand : ImapCommand
    {
        public ImapFolderSyncCommand (IBEContext beContext, ImapClient imap) : base (beContext, imap)
        {
        }

        public override void Execute (NcStateMachine sm)
        {
            // Right now, we rely on MailKit's FolderCache so access is synchronous.
            IEnumerable<IMailFolder> folderList;
            try {
                // On startup, we just asked the server for a list of folder (via Client.Authenticate()).
                // An optimization might be to keep a timestamp since the last authenticate OR last Folder Sync, and
                // skip the GetFolders if it's semi-recent (seconds).
                lock(Client.SyncRoot) {
                    if (Client.PersonalNamespaces.Count == 0) {
                        Log.Error (Log.LOG_IMAP, "No personal namespaces");
                        sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD0");
                        return;
                    }
                    // TODO Should we loop over all namespaces here? Typically there appears to be only one.
                    folderList = Client.GetFolders (Client.PersonalNamespaces[0], false, Cts.Token);
                }
            }
            catch (InvalidOperationException e) {
                Log.Error (Log.LOG_IMAP, "Could not refresh folder list: {0}", e);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD1");
                return;
            }
            catch (Exception e) {
                Log.Error (Log.LOG_IMAP, "GetFolders: Unexpected exception: {0}", e);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD2");
                return;
            }

            if (null == folderList) {
                Log.Error (Log.LOG_IMAP, "Could not refresh folder list");
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD3");
                return;
            }


            // Process all incoming folders. Create or update them
            List<string> foldernames = new List<string> (); // Keep track of folder names, so we can compare later.
            foreach (var mailKitFolder in folderList) {
                foldernames.Add (mailKitFolder.FullName);

                if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Inbox)) {
                    CreateOrUpdateFolder (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2, mailKitFolder.Name, true);
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Sent)) {
                    CreateOrUpdateFolder (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultSent_5, mailKitFolder.Name, true);
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Drafts)) {
                    CreateOrUpdateFolder (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDrafts_3, mailKitFolder.Name, true);
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Trash)) {
                    CreateOrUpdateFolder (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDeleted_4, mailKitFolder.Name, true);
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Junk)) {
                    CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, mailKitFolder.Name, true);
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Archive)) {
                    CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, McFolder.ARCHIVE_DISPLAY_NAME, true);
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.All)) {
                    CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, mailKitFolder.Name, true);
                }
                else {
                    CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, mailKitFolder.Name, false);
                }
            }

            // Compare the incoming folders to the ones we know about. Delete any that disappeared.
            foreach (var folder in McFolder.QueryByAccountId<McFolder> (BEContext.Account.Id)) {
                if (!foldernames.Contains (folder.ServerId)) {
                    folder.Delete ();
                }
            }
            sm.PostEvent ((uint)SmEvt.E.Success, "IMAPFSYNCSUC");
        }

        protected void CreateOrUpdateFolder (IMailFolder mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode folderType, string folderDisplayName, bool isDisinguished)
        {
            McFolder existing;
            if (isDisinguished) {
                existing = McFolder.GetDistinguishedFolder (BEContext.Account.Id, folderType);
            } else {
                existing = McFolder.GetUserFolders (BEContext.Account.Id, folderType, mailKitFolder.ParentFolder.UidValidity.ToString (), mailKitFolder.Name).SingleOrDefault ();
            }
            if (null == existing) {
                // Just add it.
                string parentId = null == mailKitFolder.ParentFolder ? McFolder.AsRootServerId : mailKitFolder.ParentFolder.FullName;
                var created = McFolder.Create (BEContext.Account.Id, false, false, isDisinguished, parentId, mailKitFolder.FullName, mailKitFolder.Name, folderType);
                created.ImapUidValidity = mailKitFolder.UidValidity;
                created.Insert ();
            } else {
                if (existing.IsDistinguished) {
                    Log.Error (Log.LOG_IMAP, "Trying to update distinguished folder.");
                    return;
                }
                // check & update.
                if (existing.ImapUidValidity != mailKitFolder.UidValidity) {
                    // FIXME flush and re-sync folder contents.
                }
                existing = existing.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ServerId = mailKitFolder.FullName;
                    target.DisplayName = folderDisplayName;
                    target.ImapUidValidity = mailKitFolder.UidValidity;
                    return true;
                });
                return;
            }
        }
    }

    public class ImapAuthenticateCommand : ImapCommand
    {
        public ImapAuthenticateCommand (IBEContext beContext, ImapClient imap) : base (beContext, imap)
        {
        }

        public override void Execute (NcStateMachine sm)
        {
            NcTask.Run (() => {
                try {
                    if (Client.IsConnected) {
                        Client.Disconnect (false, Cts.Token);
                    }
                    Client.Connect (BEContext.Server.Host, BEContext.Server.Port, true, Cts.Token);
                    // FIXME - add support for OAUTH2.
                    Client.AuthenticationMechanisms.Remove ("XOAUTH");
                    Client.AuthenticationMechanisms.Remove ("XOAUTH2");
                    Client.Authenticate (BEContext.Cred.Username, BEContext.Cred.GetPassword (), Cts.Token);
                    sm.PostEvent ((uint)SmEvt.E.Success, "IMAPAUTHSUC");
                } catch (InvalidOperationException ex) {
                    Log.Info (Log.LOG_IMAP, "ImapAuthenticateCommand: InvalidOperationException: {0}", ex.ToString ());
                    sm.PostEvent ((uint)SmEvt.E.TempFail, "IMAPAUTHTEMP0");
                } catch (IOException) {
                    sm.PostEvent ((uint)SmEvt.E.TempFail, "IMAPAUTHTEMP1");
                } catch (AuthenticationException) {
                    sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTHFAIL");
                } catch (NotSupportedException ex) {
                    Log.Info (Log.LOG_IMAP, "ImapAuthenticateCommand: NotSupportedException: {0}", ex.ToString ());
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPAUTHHARD0");
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "ImapAuthenticateCommand: Unexpected exception: {0}", ex.ToString ());
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPAUTHHARDX");
                }
            }, "ImapAuthenticateCommand");
        }
    }

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
                    if (!SyncKit.MailKitFolder.IsOpen) {
                        if (FolderAccess.None == SyncKit.MailKitFolder.Open (FolderAccess.ReadOnly, Cts.Token)) {
                            sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPSYNCNOOPEN");
                            return;
                        }
                    }
                    switch (SyncKit.Method) {
                    case SyncKit.MethodEnum.Range:
                        summaries = SyncKit.MailKitFolder.Fetch (
                            new UniqueIdRange (new UniqueId (SyncKit.MailKitFolder.UidValidity, SyncKit.Start),
                                new UniqueId (SyncKit.MailKitFolder.UidValidity, SyncKit.Start + SyncKit.Span)),
                            SyncKit.Flags, Cts.Token);
                        break;
                    case SyncKit.MethodEnum.OpenEnded:
                        summaries = SyncKit.MailKitFolder.Fetch (
                            new UniqueIdRange (new UniqueId (SyncKit.MailKitFolder.UidValidity, SyncKit.Start),
                                UniqueId.MaxValue), SyncKit.Flags, Cts.Token);
                        break;
                    }
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "ImapSyncCommand: Unexpected exception: {0}", ex.ToString ());
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPSYNCHARDX"); 
                }
                if (null != summaries && 0 < summaries.Count) {
                    foreach (var summary in summaries) {
                        // FIXME use NcApplyServerCommand framework.
                        ServerSaysAddOrChangeEmail (summary, SyncKit.Folder);
                        if (summary.UniqueId.Value.Id > SyncKit.Folder.ImapLargestUid) {
                            SyncKit.Folder = SyncKit.Folder.UpdateWithOCApply<McFolder> ((record) => {
                                var target = (McFolder)record;
                                target.ImapLargestUid = summary.UniqueId.Value.Id;
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
