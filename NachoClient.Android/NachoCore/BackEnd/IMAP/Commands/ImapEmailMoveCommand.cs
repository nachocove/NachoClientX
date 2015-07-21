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
            NcAssert.NotNull (emailMessage);
            McFolder src = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.ParentId);
            NcAssert.NotNull (src);
            McFolder dst = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.DestParentId);
            NcAssert.NotNull (dst);

            var result = MoveEmail (emailMessage, src, dst, Cts.Token);
            if (result.isOK ()) {
                // FIXME Need to do fixup stuff in pending. Are there API's for that?
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMoveSucceeded));
                });
                Event evt = result.GetValue<Event> ();
                return evt;
            } else {
                ResolveAllFailed (NcResult.WhyEnum.Unsupported);
                return Event.Create((uint)SmEvt.E.HardFail, "IMAPMOVHARD");
            }
        }

        public NcResult MoveEmail(McEmailMessage emailMessage, McFolder src, McFolder dst, CancellationToken Token)
        {
            NcResult result;
            UniqueId? newUid;
            var folderGuid = ImapProtoControl.ImapMessageFolderGuid (emailMessage.ServerId);
            var emailUid = ImapProtoControl.ImapMessageUid (emailMessage.ServerId);
            NcAssert.Equals (folderGuid, src.ImapGuid);
            var srcFolder = Client.GetFolder (src.ServerId, Token);
            NcAssert.NotNull (srcFolder);
            var dstFolder = Client.GetFolder (dst.ServerId, Token);
            NcAssert.NotNull (dstFolder);

            srcFolder.Open (FolderAccess.ReadWrite, Token);
            try {
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
                result = NcResult.OK ();
                result.Value = Event.Create ((uint)SmEvt.E.Success, "IMAPMOVSUC");
            } catch (ImapCommandException ex) {
                result = NcResult.Error (string.Format ("ImapCommandException {0}", ex.Message));
            }
            return result;
        }
    }
}
