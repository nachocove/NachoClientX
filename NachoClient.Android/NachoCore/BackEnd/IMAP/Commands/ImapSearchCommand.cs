//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit.Search;
using System.Collections.Generic;
using MailKit;
using MailKit.Net.Imap;
using System.Text.RegularExpressions;

namespace NachoCore.IMAP
{
    public class ImapSearchCommand : ImapCommand
    {
        private List<Regex> RegexList;

        public ImapSearchCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();

            RedactProtocolLogFunc = RedactProtocolLog;

            RegexList = new List<Regex> ();
            RegexList.Add (new Regex (@"^" + NcMailKitProtocolLogger.ImapCommandNumRegexStr + @"(?<uidstr>UID SEARCH RETURN \(.*\) )(?<redact>.*)(?<end>[\r\n]+)$", NcMailKitProtocolLogger.rxOptions));
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            // Need to redact search strings
            //2015-06-22T17:28:56.589Z: IMAP C: B00000006 UID SEARCH RETURN () REDACTED
            //2015-06-22T17:28:57.190Z: IMAP S: * ESEARCH (TAG "B00000006") UID
            //B00000006 OK SEARCH completed (Success)
            return NcMailKitProtocolLogger.RedactLogDataRegex(RegexList, logData);
        }

        public override void Execute (NcStateMachine sm)
        {
            ExecuteNoTask(sm);
        }

        protected override Event ExecuteCommand ()
        {
            var query = SearchQuery.SubjectContains (PendingSingle.Search_Prefix).Or (SearchQuery.BodyContains (PendingSingle.Search_Prefix));
            var orderBy = new [] { OrderBy.ReverseArrival };
            var folderList = McFolder.QueryByIsClientOwned (BEContext.Account.Id, false);
            var emailList = new List<NcEmailMessageIndex> ();
            foreach (var folder in folderList) {
                if (folder.ImapNoSelect) {
                    continue;
                }
                var mailKitFolder = Client.GetFolder (folder.ServerId, Cts.Token);
                mailKitFolder.Open (FolderAccess.ReadOnly, Cts.Token);
                UniqueIdSet notFoundLocally = new UniqueIdSet ();
                if (mailKitFolder.Count > 0) {
                    IList<UniqueId> uids;
                    if (Client.Capabilities.HasFlag (MailKit.Net.Imap.ImapCapabilities.Sort)) {
                        uids = mailKitFolder.Search (query, orderBy);
                    } else {
                        uids = mailKitFolder.Search (query);
                    }
                    foreach (var uid in uids) {
                        var serverId = ImapProtoControl.MessageServerId (folder, uid);
                        var email = McEmailMessage.QueryByServerId<McEmailMessage> (BEContext.Account.Id, serverId);
                        if (null != email) {
                            // if no email is found locally, it likely hasn't sync'd yet, so don't bother sending it back up.
                            emailList.Add (new NcEmailMessageIndex (email.Id));
                        } else {
                            notFoundLocally.Add (uid);
                        }
                    }
                    // TODO Should we post an indication to the UI for each searched folder?
                }
                Log.Info (Log.LOG_IMAP, "ImapSearchCommand {0}: Found {1} items", folder.ImapFolderNameRedacted(), emailList.Count);
                Log.Info (Log.LOG_IMAP, "ImapSearchCommand {0}: Found {1} items that do not exist locally: {2}", folder.ImapFolderNameRedacted(), notFoundLocally.Count, notFoundLocally.ToString ());
            }
            var result = NcResult.Info (NcResult.SubKindEnum.Info_EmailSearchCommandSucceeded);
            result.Value = emailList;
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, result);
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPSEASUC");
        }
    }
}

