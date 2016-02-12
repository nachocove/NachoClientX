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
using System.Linq;

namespace NachoCore.IMAP
{
    public class ImapSearchCommand : ImapCommand
    {
        private List<Regex> RegexList;

        public ImapSearchCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispatched ();

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
            var orderBy = new [] { OrderBy.ReverseArrival };
            var timespan = BEContext.Account.DaysSyncEmailSpan ();

            var query = SearchQuery.SubjectContains (PendingSingle.Search_Prefix)
                .Or (SearchQuery.BodyContains (PendingSingle.Search_Prefix))
                .Or (SearchQuery.FromContains (PendingSingle.Search_Prefix))
                .Or (SearchQuery.BccContains (PendingSingle.Search_Prefix))
                .Or (SearchQuery.MessageContains (PendingSingle.Search_Prefix))
                .Or (SearchQuery.CcContains (PendingSingle.Search_Prefix));
            if (Client.Capabilities.HasFlag (ImapCapabilities.GMailExt1)) {
                query = query.Or (SearchQuery.GMailRawSearch (PendingSingle.Search_Prefix));
            }
            if (TimeSpan.Zero != timespan) {
                query = query.And (SearchQuery.DeliveredAfter (DateTime.UtcNow.Subtract (timespan)));
            }

            var folderList = McFolder.QueryByIsClientOwned (AccountId, false);
            var emailList = new List<NcEmailMessageIndex> ();
            foreach (var folder in folderList) {
                if (folder.ImapNoSelect) {
                    continue;
                }
                var mailKitFolder = GetOpenMailkitFolder (folder);
                var tmpFolder = folder;
                // this code will soon be rewritten, so we won't worry about the possibly changed tmpFolder
                UpdateImapSetting (mailKitFolder, ref tmpFolder);

                if (mailKitFolder.Count > 0) {
                    IList<UniqueId> uids;
                    if (Client.Capabilities.HasFlag (ImapCapabilities.Sort)) {
                        uids = mailKitFolder.Search (query, orderBy);
                    } else {
                        uids = mailKitFolder.Search (query);
                    }
                    if (uids.Any ()) {
                        emailList.AddRange (McEmailMessage.QueryByImapUidList (AccountId, folder.Id, uids,
                            (uint)(PendingSingle.Search_MaxResults - emailList.Count)));
                    }
                }
                if (emailList.Count > PendingSingle.Search_MaxResults) {
                    break;
                }
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

