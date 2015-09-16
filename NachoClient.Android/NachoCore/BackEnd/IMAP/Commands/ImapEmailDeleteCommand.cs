﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;
using System.Collections.Generic;
using MailKit.Net.Imap;
using System.Linq;

namespace NachoCore.IMAP
{
    public class ImapEmailDeleteCommand : ImapCommand
    {
        public ImapEmailDeleteCommand (IBEContext beContext, NcImapClient imap, List<McPending> pendingList) : base (beContext, imap)
        {
            PendingList = pendingList;
            foreach (var pending in PendingList) {
                pending.MarkDispached ();
            }
            RedactProtocolLogFunc = RedactProtocolLog;
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            // Nothing additional to redact. The command just has UID's.
            //L00000059 OK [READ-WRITE] REDACTED selected. (Success)
            //2015-06-22T17:18:23.971Z: IMAP C: L00000060 UID STORE 8642 FLAGS.SILENT (\Deleted)
            //2015-06-22T17:18:25.468Z: IMAP S: * 12 FETCH (UID 8642 MODSEQ (953602) FLAGS (\Deleted))
            //L00000060 OK Success
            //2015-06-22T17:18:25.470Z: IMAP C: L00000061 UID EXPUNGE 8642
            //2015-06-22T17:18:27.717Z: IMAP S: * 12 EXPUNGE
            //* 16 EXISTS
            //L00000061 OK Success
            return logData;
        }

        protected override Event ExecuteCommand ()
        {
            McFolder folder = null;
            // FIXME This will not work once we turn on email-in-multiple-folders feature
            List<UniqueId> uids = new List<UniqueId> ();
            var removeList = new List<McPending> ();
            foreach (var pending in PendingList) {
                if (null == folder) {
                    folder = McFolder.QueryByServerId (AccountId, pending.ParentId);
                    if (null == folder) {
                        Log.Error (Log.LOG_IMAP, "No folder for {0}", pending.ParentId);
                        return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMSGDELFOLDERFAIL");
                    }
                }
                UInt32 uid;
                if (!UInt32.TryParse (pending.ServerId.Split (':') [1], out uid)) {
                    Log.Error (Log.LOG_IMAP, "Could not extract UID from ServerId {0}", pending.ServerId);
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageDeleteFailed, NcResult.WhyEnum.BadOrMalformed));
                    removeList.Add (pending);
                    continue;
                }
                uids.Add (new UniqueId(uid));
            }
            foreach (var pending in removeList) {
                PendingList.Remove (pending);
            }
            if (!uids.Any ()) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMSGDELPARSEFAIL");
            }

            IMailFolder mailKitFolder = GetOpenMailkitFolder (folder, FolderAccess.ReadWrite);
            if (null == mailKitFolder) {
                Log.Error (Log.LOG_IMAP, "No mailKitFolder for {0}", folder.ImapFolderNameRedacted ());
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMSGDELOPEN");
            }

            mailKitFolder.SetFlags (uids, MessageFlags.Deleted, true, Cts.Token);
            if (Client.Capabilities.HasFlag (ImapCapabilities.UidPlus)) {
                mailKitFolder.Expunge (uids, Cts.Token);
            }
            // TODO The set flags reply contains information we can use (S: * 5 FETCH (UID 8631 MODSEQ (948373) FLAGS (\Deleted))).
            // save it. That being said, if we increment the MODSEQ, then that will basically
            // force a resync (or at least a sync), which we don't want. Might want to check the modseq before our delete.
            UpdateImapSetting (mailKitFolder, ref folder);
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, 
                    NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageDeleteSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPMSGDELSUC");
        }
    }
}

