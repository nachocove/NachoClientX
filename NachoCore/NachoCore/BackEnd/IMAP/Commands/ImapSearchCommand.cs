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
        public ImapSearchCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispatched ();
        }

        public override void Execute (NcStateMachine sm)
        {
            Sm = sm;
            ExecuteNoTask ();
        }

        protected override Event ExecuteCommand ()
        {
            var orderBy = new [] { OrderBy.ReverseArrival };
            var timespan = BEContext.Account.DaysSyncEmailSpan ();

            // Since we'll only consider results that we already have in our local db, and since the only
            // piece we might be missing locally is the message body, we only need to query the body field
            SearchQuery query = SearchQuery.BodyContains (PendingSingle.Search_Prefix);
            //if (Client.Capabilities.HasFlag (ImapCapabilities.GMailExt1)) {
            //    query = query.Or (SearchQuery.GMailRawSearch (PendingSingle.Search_Prefix));
            //}
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
                        List<string> serverIdList = new List<string> ();
                        foreach (var uid in uids) {
                            serverIdList.Add (ImapProtoControl.MessageServerId (folder, uid));
                        }
                        var idList = McEmailMessage.QueryByServerIdList (AccountId, serverIdList);
                        if (idList.Any ()) {
                            foreach (var id in idList) {
                                emailList.Add (id);
                                if (emailList.Count > PendingSingle.Search_MaxResults) {
                                    break;
                                }
                            }
                        }
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

