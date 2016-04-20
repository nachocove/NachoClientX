//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;
using System.Threading;
using MailKit.Net.Imap;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace NachoCore.IMAP
{
    public class ImapEmailMoveCommand : ImapCommand
    {
        public ImapEmailMoveCommand (IBEContext beContext, List<McPending> pendingList) : base (beContext)
        {
            PendingList = pendingList;
            NcModel.Instance.RunInTransaction (() => {
                foreach (var pending in pendingList) {
                    pending.MarkDispatched ();
                }
            });
        }

        protected override Event ExecuteCommand ()
        {
            // All pendings are assumed to be for the same folders.
            var first = PendingList.FirstOrDefault ();
            if (null == first) {
                Log.Error (Log.LOG_IMAP, "No pendings");
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMSGMOVNONE");
            }
            McFolder src = McFolder.QueryByServerId (AccountId, first.ParentId);
            if (null == src) {
                Log.Error (Log.LOG_IMAP, "No src folder for {0}", first.ParentId);
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMSGDELFOLDERFAIL1");
            }
            McFolder dst = McFolder.QueryByServerId (AccountId, first.DestParentId);
            if (null == dst) {
                Log.Error (Log.LOG_IMAP, "No dst folder for {0}", first.DestParentId);
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMSGDELFOLDERFAIL2");
            }

            var emails = new List<McEmailMessage> ();
            var removeList = new List<McPending> ();
            foreach (var pending in PendingList) {
                var emailMessage = McEmailMessage.QueryByServerId<McEmailMessage> (AccountId, pending.ServerId);
                if (null == emailMessage) {
                    Log.Error (Log.LOG_IMAP, "Could not find email message {0}", pending.ServerId);
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageMoveFailed, NcResult.WhyEnum.BadOrMalformed));
                    removeList.Add (pending);
                    continue;
                }
                emails.Add (emailMessage);
            }
            foreach (var pending in removeList) {
                PendingList.Remove (pending);
            }

            if (!emails.Any ()) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMOVHARD");

            }
            var result = MoveEmails (emails, src, dst, Cts.Token);
            if (result.isOK ()) {
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMoveSucceeded));
                });
                Event evt = result.GetValue<Event> ();
                return evt;
            } else {
                ResolveAllFailed (NcResult.WhyEnum.Unsupported);
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMOVHARD");
            }
        }

        public NcResult MoveEmails (List<McEmailMessage> emails, McFolder src, McFolder dst, CancellationToken Token)
        {
            NcResult result;
            var srcFolder = Client.GetFolder (src.ServerId, Token);
            NcAssert.NotNull (srcFolder);
            var dstFolder = Client.GetFolder (dst.ServerId, Token);
            NcAssert.NotNull (dstFolder);

            var emailUidMapping = new Dictionary<uint, McEmailMessage> ();
            var uids = new List<UniqueId> ();
            foreach (var email in emails) {
                uids.Add (new UniqueId (email.ImapUid));
                emailUidMapping [email.ImapUid] = email;
            }
            srcFolder.Open (FolderAccess.ReadWrite, Token);
            try {
                // in order to protect against messages having been deleted, let's get a list of messages
                // that exist in the folder, based on the list of uid's we want to move.
                var summaries = srcFolder.Fetch (uids, MessageSummaryItems.UniqueId, Token);
                var existingUids = new List<UniqueId> ();
                foreach (var sum in summaries) {
                    existingUids.Add (sum.UniqueId);
                }
                // then move the ones we know exist
                // Note: There's still a tiny window where something might have deleted
                // one of these messages, too. We can't prevent it. MailKit doesn't pass back 
                // the necessary information we need from the COPYUID response to accomplish this.
                var uidMapping = srcFolder.MoveTo (existingUids, dstFolder, Token);
                if (existingUids.Count != uidMapping.Count) {
                    Log.Warn (Log.LOG_IMAP, "Messages seem to have disappeared during move! Wanted to move: {0}, found existing UIDS {1}, and new UIDS {2}", uids, existingUids, uidMapping);
                }

                NcModel.Instance.RunInTransaction (() => {
                    foreach (var pair in uidMapping) {
                        McEmailMessage email;
                        if (emailUidMapping.TryGetValue (pair.Key.Id, out email)) {
                            email.UpdateWithOCApply<McEmailMessage> ((record) => {
                                var target = (McEmailMessage)record;
                                target.ServerId = ImapProtoControl.MessageServerId (dst, pair.Value);
                                target.ImapUid = pair.Value.Id;
                                return true;
                            });
                        } else {
                            Log.Error (Log.LOG_IMAP, "Could not match UID {0} to email", pair.Key);
                        }
                    }
                });

                // deal with the deleted emails
                var toDelete = uids.Except (existingUids).ToList ();
                if (toDelete.Any ()) {
                    foreach (var uid in toDelete) {
                        McEmailMessage email;
                        if (emailUidMapping.TryGetValue (uid.Id, out email)) {
                            email.Delete ();
                        }
                    }
                    BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                }
                result = NcResult.OK ();
                result.Value = Event.Create ((uint)SmEvt.E.Success, "IMAPMOVSUC");
            } catch (ImapCommandException ex) {
                result = NcResult.Error (string.Format ("ImapCommandException {0}", ex.Message));
            }
            return result;
        }
    }
}
