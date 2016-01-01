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
        private List<Regex> RegexList;

        public ImapEmailMoveCommand (IBEContext beContext, NcImapClient imap, List<McPending> pendingList) : base (beContext, imap)
        {
            PendingList = pendingList;
            NcModel.Instance.RunInTransaction (() => {
                foreach (var pending in pendingList) {
                    pending.MarkDispached ();
                }
            });
            RedactProtocolLogFunc = RedactProtocolLog;

            RegexList = new List<Regex> ();
            RegexList.Add (new Regex (@"^(?<num>\w+)(?<space1>\s)(?<cmd>UID MOVE )(?<uid>\d+ )(?<redact>.*)(?<end>[\r\n]+)$", NcMailKitProtocolLogger.rxOptions));
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            //2015-06-22T17:27:03.854Z: IMAP C: A00000082 UID MOVE 8728 REDACTED
            //2015-06-22T17:27:04.326Z: IMAP S: * 60 EXPUNGE
            //* 59 EXISTS
            //A00000082 OK [COPYUID 5 8728 8648] (Success)
            return NcMailKitProtocolLogger.RedactLogDataRegex (RegexList, logData);
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
            var uids = new List<UniqueId> ();
            var removeList = new List<McPending> ();
            foreach (var pending in PendingList) {
                NcAssert.True (pending.ItemId > 0);
                var emailMessage = McEmailMessage.QueryByServerId<McEmailMessage> (AccountId, pending.ServerId);
                if (null == emailMessage) {
                    Log.Error (Log.LOG_IMAP, "Could not find email message {0}", pending.ServerId);
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageMoveFailed, NcResult.WhyEnum.BadOrMalformed));
                    removeList.Add (pending);
                    continue;
                }
                emails.Add (emailMessage);
                uids.Add (new UniqueId ((uint)(pending.ItemId)));
            }
            foreach (var pending in removeList) {
                PendingList.Remove (pending);
            }

            if (!emails.Any ()) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMOVHARD");

            }
            var result = MoveEmails (emails, uids, src, dst, Cts.Token);
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

        /// <summary>
        /// Moves the emails.
        /// </summary>
        /// <returns>The emails.</returns>
        /// <param name="emails">Emails.</param>
        /// <param name="uids">Uids. Order must match the emails</param>
        /// <param name="src">Source.</param>
        /// <param name="dst">Dst.</param>
        /// <param name="Token">Token.</param>
        public NcResult MoveEmails (List<McEmailMessage> emails, List<UniqueId> uids, McFolder src, McFolder dst, CancellationToken Token)
        {
            NcResult result;
            var srcFolder = Client.GetFolder (src.ServerId, Token);
            NcAssert.NotNull (srcFolder);
            var dstFolder = Client.GetFolder (dst.ServerId, Token);
            NcAssert.NotNull (dstFolder);

            srcFolder.Open (FolderAccess.ReadWrite, Token);
            try {
                var newUids = srcFolder.MoveTo (uids, dstFolder, Token);
                if (newUids.Any ()) {
                    NcModel.Instance.RunInTransaction (() => {
                        for (var i=0; i<newUids.Count; i++) {
                            emails[i].SetImapUid (dst, new UniqueId (newUids[i].Id));
                        }
                    });
                } else {
                    // FIXME How do we determine the new ID? This can happen with servers that don't support UIDPLUS.
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
