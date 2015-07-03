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

namespace NachoCore.IMAP
{
    public class ImapEmailMoveCommand : ImapCommand
    {
        private List<Regex> RegexList;

        public ImapEmailMoveCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
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
            return NcMailKitProtocolLogger.RedactLogDataRegex(RegexList, logData);
        }

        protected override Event ExecuteCommand ()
        {
            var emailMessage = McEmailMessage.QueryByServerId<McEmailMessage> (BEContext.Account.Id, PendingSingle.ServerId);
            if (null == emailMessage) {
                Log.Error (Log.LOG_IMAP, "No Email matches id {0}.", PendingSingle.ServerId);
                throw new NcImapCommandFailException (Event.Create ((uint)SmEvt.E.HardFail, "IMAPEMMOVHARD0"), NcResult.WhyEnum.NotSpecified);
            }
            McFolder src = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.ParentId);
            if (null == src) {
                Log.Error (Log.LOG_IMAP, "No folder matches id {0}.", PendingSingle.ParentId);
                throw new NcImapCommandFailException (Event.Create ((uint)SmEvt.E.HardFail, "IMAPEMMOVHARD1"), NcResult.WhyEnum.NotSpecified);
            }
            McFolder dst = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.DestParentId);
            if (null == src) {
                Log.Error (Log.LOG_IMAP, "No folder matches id {0}.", PendingSingle.DestParentId);
                throw new NcImapCommandFailException (Event.Create ((uint)SmEvt.E.HardFail, "IMAPEMMOVHARD2"), NcResult.WhyEnum.NotSpecified);
            }

            MoveEmail (emailMessage, src, dst, Cts.Token);

            // FIXME Need to do fixup stuff in pending. Are there API's for that?
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMoveSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPEMMOVSUC");
        }

        public void MoveEmail(McEmailMessage emailMessage, McFolder src, McFolder dst, CancellationToken Token)
        {
            UniqueId? newUid;
	    var folderGuid = ImapProtoControl.ImapMessageFolderGuid (emailMessage.ServerId);
	    var emailUid = ImapProtoControl.ImapMessageUid (emailMessage.ServerId);
	    if (folderGuid != src.ImapGuid) {
		Log.Error (Log.LOG_IMAP, "folder UIDVALIDITY does not match.");
		throw new NcImapCommandRetryException (Event.Create ((uint)ImapProtoControl.ImapEvt.E.ReFSync, "IMAPEMMOVUID"));
	    }
	    var srcFolder = Client.GetFolder (src.ServerId, Token);
	    if (null == srcFolder) {
		Log.Error (Log.LOG_IMAP, "Could not Get imap src folder");
		throw new NcImapCommandFailException (Event.Create ((uint)SmEvt.E.HardFail, "IMAPEMMOVHARD3"), NcResult.WhyEnum.MissingOnServer);
	    }
	    var dstFolder = Client.GetFolder (dst.ServerId, Token);
	    if (null == dstFolder) {
		Log.Error (Log.LOG_IMAP, "Could not Get imap dst folder");
		throw new NcImapCommandFailException (Event.Create ((uint)SmEvt.E.HardFail, "IMAPEMMOVHARD4"), NcResult.WhyEnum.MissingOnServer);
	    }

            srcFolder.Open (FolderAccess.ReadWrite, Token);
            newUid = srcFolder.MoveTo (emailUid, dstFolder, Token);
            if (null != newUid && newUid.HasValue && 0 != newUid.Value.Id) {
                emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                    var target = (McEmailMessage)record;
                    target.ServerId = ImapProtoControl.MessageServerId (dst, (UniqueId)newUid);
                    return true;
                });
            } else {
                // FIXME How do we determine the new ID? This can happen with servers that don't support UIDPLUS.
            }
        }
    }
}
