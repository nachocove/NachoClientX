//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;

namespace NachoCore.ActiveSync
{
    public class AsStrategy : IAsStrategy
    {
        public const int KBaseOverallWindowSize = 150;
        public const int KBasePerFolderWindowSize = 100;
        public const int KBaseFetchSize = 10;

        private enum ItemType
        {
            Email = 0,
            Cal = 1,
            Contact = 2,
            Last = Contact,
        };

        private enum EmailEnum
        {
            None,
            Def1d,
            Def3d,
            Def1w,
            Def2w,
            All1m,
            All3m,
            All6m,
            AllInf,

        };

        private enum CalEnum
        {
            None,
            Def2w,
            All1m,
            All3m,
            All6m,
            AllInf,
        };

        private enum ContactEnum
        {
            None,
            RicInf,
            DefRicInf,
            AllInf,
        };

        private enum ActionEnum
        {
            None,
            RicSynced,
        };

        private ItemType[] ItemTypeSeq = new ItemType[] { ItemType.Email, ItemType.Cal, ItemType.Contact };

        private int[,] Ladder = new int[,] {
            // { Email, Cal, Contact, Action }
            { (int)EmailEnum.None, (int)CalEnum.None, (int)ContactEnum.RicInf, (int)ActionEnum.RicSynced },
            { (int)EmailEnum.Def1d, (int)CalEnum.Def2w, (int)ContactEnum.RicInf, (int)ActionEnum.None },
            { (int)EmailEnum.Def3d, (int)CalEnum.Def2w, (int)ContactEnum.RicInf, (int)ActionEnum.None },
            { (int)EmailEnum.Def1w, (int)CalEnum.Def2w, (int)ContactEnum.RicInf, (int)ActionEnum.None },
            { (int)EmailEnum.Def2w, (int)CalEnum.Def2w, (int)ContactEnum.DefRicInf, (int)ActionEnum.None },
            { (int)EmailEnum.All1m, (int)CalEnum.All1m, (int)ContactEnum.DefRicInf, (int)ActionEnum.None },
            { (int)EmailEnum.All3m, (int)CalEnum.All3m, (int)ContactEnum.AllInf, (int)ActionEnum.None },
            { (int)EmailEnum.All6m, (int)CalEnum.All6m, (int)ContactEnum.AllInf, (int)ActionEnum.None },
            { (int)EmailEnum.AllInf, (int)CalEnum.AllInf, (int)ContactEnum.AllInf, (int)ActionEnum.None },
        };

        private IBEContext BEContext;
        private Random CoinToss;

        public AsStrategy (IBEContext beContext)
        {
            BEContext = beContext;
            CoinToss = new Random ();
        }

        private List<ItemType> RequiredToAdvance (int rung)
        {
            var retval = new List<ItemType> ();
            var limit = Ladder.GetLength (0);
            if (limit == rung) {
                return retval;
            }
            foreach (int track in ItemTypeSeq) {
                if (Ladder [rung, track] != Ladder [rung + 1, track]) {
                    retval.Add ((ItemType)track);
                }
            }
            return retval;
        }

        private bool CanAdvance (int rung)
        {
            var musts = RequiredToAdvance (rung);
            var folders = new List<McFolder> ();
            foreach (var must in musts) {
                switch (must) {
                case ItemType.Email:
                    folders.AddRange (EmailFolderListProvider ((EmailEnum)Ladder [rung, (int)must], false));
                    break;
                case ItemType.Cal:
                    folders.AddRange (CalFolderListProvider ((CalEnum)Ladder [rung, (int)must], false));
                    break;
                case ItemType.Contact:
                    folders.AddRange (ContactFolderListProvider ((ContactEnum)Ladder [rung, (int)must], false));
                    break;
                default:
                    NcAssert.CaseError (must.ToString ());
                    break;
                }
            }
            return !folders.Any (x => x.AsSyncMetaToClientExpected = true);
        }

        private int AdvanceIfPossible (int rung)
        {
            if (CanAdvance (rung)) {
                switch ((ActionEnum)Ladder[rung, (int)ItemType.Last + 1]) {
                case ActionEnum.RicSynced:
                    BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_RicInitialSyncCompleted));
                    break;
                case ActionEnum.None:
                    break;
                default:
                    NcAssert.CaseError (rung.ToString ());
                    break;
                }
                var protocolState = BEContext.ProtocolState;
                protocolState.StrategyRung++;
                protocolState.Update ();
                rung = protocolState.StrategyRung;
            }
            return rung;
        }

        private List<McFolder> EmailFolderListProvider (EmailEnum scope, bool isNarrow)
        {
            switch (scope) {
            case EmailEnum.None:
                if (isNarrow) {
                    return new List<McFolder> () { McFolder.GetDefaultInboxFolder (BEContext.Account.Id) };
                }
                return new List<McFolder> ();

            case EmailEnum.Def1d:
            case EmailEnum.Def1w:
            case EmailEnum.Def2w:
            case EmailEnum.Def3d:
                return new List<McFolder> () { McFolder.GetDefaultInboxFolder (BEContext.Account.Id) };

            case EmailEnum.All1m:
            case EmailEnum.All3m:
            case EmailEnum.All6m:
            case EmailEnum.AllInf:
                if (isNarrow) {
                    return new List<McFolder> () { McFolder.GetDefaultInboxFolder (BEContext.Account.Id) };
                }
                return McFolder.ServerEndQueryAll (BEContext.Account.Id).Where (f => 
                    Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) ==
                McAbstrFolderEntry.ClassCodeEnum.Email).ToList ();

            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        private List<McFolder> CalFolderListProvider (CalEnum scope, bool isNarrow)
        {
            switch (scope) {
            case CalEnum.None:
                if (isNarrow) {
                    return new List<McFolder> () { McFolder.GetDefaultCalendarFolder (BEContext.Account.Id) };
                }
                return new List<McFolder> ();

            case CalEnum.Def2w:
                return new List<McFolder> () { McFolder.GetDefaultCalendarFolder (BEContext.Account.Id) };

            case CalEnum.All1m:
            case CalEnum.All3m:
            case CalEnum.All6m:
            case CalEnum.AllInf:
                if (isNarrow) {
                    return new List<McFolder> () { McFolder.GetDefaultCalendarFolder (BEContext.Account.Id) };
                }
                return McFolder.ServerEndQueryAll (BEContext.Account.Id).Where (f => 
                    Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) ==
                McAbstrFolderEntry.ClassCodeEnum.Calendar).ToList ();

            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        private List<McFolder> ContactFolderListProvider (ContactEnum scope, bool isNarrow)
        {
            if (isNarrow) {
                return new List<McFolder> ();
            }
            switch (scope) {
            case ContactEnum.None:
                return new List<McFolder> ();

            case ContactEnum.RicInf:
                return new List<McFolder> () { McFolder.GetRicContactFolder (BEContext.Account.Id) };

            case ContactEnum.DefRicInf:
                return new List<McFolder> () { McFolder.GetRicContactFolder (BEContext.Account.Id),
                    McFolder.GetDefaultContactFolder (BEContext.Account.Id)
                };

            case ContactEnum.AllInf:
                return McFolder.ServerEndQueryAll (BEContext.Account.Id).Where (f => 
                    Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) ==
                McAbstrFolderEntry.ClassCodeEnum.Contact).ToList ();

            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        // function returning all folders at current level eligible for Sync.
        private List<McFolder> FolderListProvider (int rung, bool isNarrow)
        {
            var result = new List<McFolder> ();
            foreach (int track in ItemTypeSeq) {
                var scope = Ladder [rung, track];
                switch ((ItemType)track) {
                case ItemType.Email:
                    result.AddRange (EmailFolderListProvider ((EmailEnum)scope, isNarrow));
                    break;
                case ItemType.Cal:
                    result.AddRange (CalFolderListProvider ((CalEnum)scope, isNarrow));
                    break;
                case ItemType.Contact:
                    result.AddRange (ContactFolderListProvider ((ContactEnum)scope, isNarrow));
                    break;
                default:
                    NcAssert.CaseError (string.Format ("{0}", track));
                    break;
                }
            }
            return result;
        }

        private Tuple<Xml.Provision.MaxAgeFilterCode, uint> ParametersProvider (McFolder folder, int rung, bool isNarrow)
        {
            uint perFolderWindowSize = KBasePerFolderWindowSize;
            switch (NcCommStatus.Instance.Speed) {
            case NetStatusSpeedEnum.CellFast:
                perFolderWindowSize *= 2;
                break;
            case NetStatusSpeedEnum.WiFi:
                perFolderWindowSize *= 3;
                break;
            }
            switch (Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (folder.Type)) {
            case McFolder.ClassCodeEnum.Email:
                switch ((EmailEnum)Ladder [rung, (int)ItemType.Email]) {
                case EmailEnum.None:
                    return null;
                case EmailEnum.Def1d:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneDay_1, perFolderWindowSize);
                case EmailEnum.Def3d:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.ThreeDays_2, perFolderWindowSize);
                case EmailEnum.Def1w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneWeek_3, perFolderWindowSize);
                case EmailEnum.Def2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.TwoWeeks_4, perFolderWindowSize);
                case EmailEnum.All1m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneMonth_5, perFolderWindowSize);
                case EmailEnum.All3m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.ThreeMonths_6, perFolderWindowSize);
                case EmailEnum.All6m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SixMonths_7, perFolderWindowSize);
                case EmailEnum.AllInf:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, perFolderWindowSize);
                default:
                    NcAssert.CaseError (string.Format ("{0}", Ladder [rung, (int)ItemType.Email]));
                    return null;
                }

            case McFolder.ClassCodeEnum.Calendar:
                switch ((CalEnum)Ladder [rung, (int)ItemType.Cal]) {
                case CalEnum.None:
                    return null;
                case CalEnum.Def2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.TwoWeeks_4, perFolderWindowSize);
                case CalEnum.All1m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneMonth_5, perFolderWindowSize);
                case CalEnum.All3m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.ThreeMonths_6, perFolderWindowSize);
                case CalEnum.All6m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SixMonths_7, perFolderWindowSize);
                case CalEnum.AllInf:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, perFolderWindowSize);
                default:
                    NcAssert.CaseError (string.Format ("{0}", Ladder [rung, (int)ItemType.Cal]));
                    return null;
                }

            case McFolder.ClassCodeEnum.Contact:
                switch ((ContactEnum)Ladder [rung, (int)ItemType.Cal]) {
                case ContactEnum.None:
                    return null;
                case ContactEnum.RicInf:
                case ContactEnum.DefRicInf:
                case ContactEnum.AllInf:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, perFolderWindowSize);
                default:
                    NcAssert.CaseError (string.Format ("{0}", Ladder [rung, (int)ItemType.Contact]));
                    return null;
                }

            default:
                NcAssert.CaseError (string.Format ("{0}", folder.Type));
                return null;
            }
        }

        private List<McFolder> AllSyncedFolders ()
        {
            // A folder must be created on the server before it can be the subject of a Sync/Ping.
            return McFolder.ServerEndQueryAll (BEContext.Account.Id);
        }

        public Tuple<uint, List<Tuple<McFolder, List<McPending>>>> SyncKit (bool cantBeEmpty)
        {
            return SyncKit (false, cantBeEmpty);
        }

        private Tuple<uint, List<Tuple<McFolder, List<McPending>>>> SyncKit (bool isNarrow, bool cantBeEmpty)
        {
            var rung = BEContext.ProtocolState.StrategyRung;
            uint overallWindowSize = KBaseOverallWindowSize;
            switch (NcCommStatus.Instance.Speed) {
            case NetStatusSpeedEnum.CellFast:
                overallWindowSize *= 2;
                break;
            case NetStatusSpeedEnum.WiFi:
                overallWindowSize *= 3;
                break;
            }
            if (!isNarrow) {
                // Only climb the ladder when not in a narrow sync.
                rung = AdvanceIfPossible (rung);
            }
            List<McFolder> eligibleForGetChanges = FolderListProvider (rung, isNarrow);

            List<McPending> issuePendings;
            bool inSerialMode = false;
            bool issuedAtLeast1 = false;
            var includedFolders = new List<McFolder> ();
            var retList = new List<Tuple<McFolder, List<McPending>>> ();
            var limit = BEContext.ProtocolState.AsSyncLimit;

            // Loop through all synced folders.
            foreach (var folder in AllSyncedFolders ()) {
                if (0 >= limit) {
                    break;
                }
                // See if we can and should do GetChanges. O(N**2), small N.
                if (null != eligibleForGetChanges.FirstOrDefault (x => x.Id == folder.Id)) {
                    if (folder.AsSyncMetaToClientExpected) {
                        folder.AsSyncMetaDoGetChanges = (McFolder.AsSyncKey_Initial != folder.AsSyncKey);
                        var parms = ParametersProvider (folder, BEContext.ProtocolState.StrategyRung, isNarrow);
                        folder.AsSyncMetaFilterCode = parms.Item1;
                        folder.AsSyncMetaWindowSize = parms.Item2;
                    } else {
                        folder.AsSyncMetaDoGetChanges = false;
                    }
                    folder.Update ();
                }
                // See if we can complete some McPending.
                issuePendings = new List<McPending> ();
                if (McFolder.AsSyncKey_Initial != folder.AsSyncKey) {
                    // If we are in serial mode, we will issue no more pendings.
                    if (!inSerialMode) {
                        var rawPendings = McPending.QueryEligibleByFolderServerId (BEContext.Account.Id, folder.ServerId);
                        issuePendings = rawPendings.Where (p => AsSyncCommand.IsSyncCommand (p.Operation)).ToList ();
                        if (issuedAtLeast1) {
                            // If we have issuedAtLeast1, then we exclude any serial pendings.
                            issuePendings = issuePendings.Where (p => !p.DeferredSerialIssueOnly).ToList ();
                        } else if (0 < issuePendings.Count) {
                            // If we have not issuedAtLeast1, then grab the 1st and decide based on that.
                            var first = issuePendings.First ();
                            if (first.DeferredSerialIssueOnly) {
                                inSerialMode = true;
                                issuePendings = new List<McPending> () { first };
                            } else {
                                issuePendings = issuePendings.Where (p => !p.DeferredSerialIssueOnly).ToList ();
                            }
                            issuedAtLeast1 = true;
                        }
                    }
                }
                // if initial-key || some pending || GetChanges, include folder in Sync.
                if (McFolder.AsSyncKey_Initial == folder.AsSyncKey ||
                    folder.AsSyncMetaDoGetChanges ||
                    0 < issuePendings.Count) {
                    retList.Add (Tuple.Create (folder, issuePendings));
                    includedFolders.Add (folder);
                    --limit;
                }
            }
            return (0 == retList.Count) ? null : Tuple.Create (overallWindowSize, retList);
        }

        private IEnumerable<McFolder> PingKit (bool isNarrow)
        {
            var folders = FolderListProvider (BEContext.ProtocolState.StrategyRung, isNarrow);
            if (BEContext.ProtocolState.MaxFolders >= folders.Count) {
                return folders;
            }
            List<McFolder> fewer = new List<McFolder> ();
            var defInbox = folders.FirstOrDefault (x => x.Type == Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            if (null != defInbox) {
                fewer.Add (defInbox);
                folders.Remove (defInbox);
            }
            var defCal = folders.FirstOrDefault (x => x.Type == Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            if (null != defCal) {
                fewer.Add (defCal);
                folders.Remove (defCal);
            }
            var stalest = folders.OrderBy (x => x.AsSyncLastPing).Take ((int)BEContext.ProtocolState.MaxFolders - fewer.Count);
            fewer.AddRange (stalest);
            return fewer;
        }

        private Tuple<IEnumerable<McPending>, IEnumerable<Tuple<McAbstrItem, string>>> FetchKit ()
        {
            var fetchSize = KBaseFetchSize;
            switch (NcCommStatus.Instance.Speed) {
            case NetStatusSpeedEnum.CellFast:
                fetchSize *= 2;
                break;
            case NetStatusSpeedEnum.WiFi:
                fetchSize *= 3;
                break;
            }
            // Address user-driven fetching first.
            var pendings = McPending.QueryFirstNEligibleByOperation (BEContext.Account.Id, McPending.Operations.AttachmentDownload, fetchSize).ToList ();
            if (pendings.Count < fetchSize) {
                var emails = McPending.QueryFirstNEligibleByOperation (BEContext.Account.Id, McPending.Operations.EmailBodyDownload, fetchSize);
                pendings.AddRange (emails);
            }
            if (pendings.Count < fetchSize) {
                var contacts = McPending.QueryFirstNEligibleByOperation (BEContext.Account.Id, McPending.Operations.ContactBodyDownload, fetchSize);
                pendings.AddRange (contacts);
            }
            if (pendings.Count < fetchSize) {
                var cals = McPending.QueryFirstNEligibleByOperation (BEContext.Account.Id, McPending.Operations.CalBodyDownload, fetchSize);
                pendings.AddRange (cals);
            }
            if (pendings.Count < fetchSize) {
                var tasks = McPending.QueryFirstNEligibleByOperation (BEContext.Account.Id, McPending.Operations.TaskBodyDownload, fetchSize);
                pendings.AddRange (tasks);
            }
            List<Tuple<McAbstrItem, string>> prefetches = new List<Tuple<McAbstrItem, string>> ();
            var remaining = fetchSize - pendings.Count;

            // Address background fetching if no immediate user need.
            if (0 == pendings.Count) {
                if (0 < remaining) {
                    var folders = FolderListProvider (BEContext.ProtocolState.StrategyRung, false);
                    foreach (var folder in folders) {
                        var emails = McEmailMessage.QueryNeedsFetch (BEContext.Account.Id, folder.Id, fetchSize);
                        foreach (var email in emails) {
                            prefetches.Add (Tuple.Create ((McAbstrItem)email, folder.ServerId));
                            if (remaining <= prefetches.Count) {
                                break;
                            }
                            // TODO - if we choose to prefetch Tasks, Contacts, etc then add code here.
                        }
                        if (remaining <= prefetches.Count) {
                            break;
                        }
                    }
                }
            }
            // Return a tuple: Item1 is the list of McPendings (user-initiated fetches),
            // Item2 is the list of McItems (background fetching).
            return Tuple.Create (pendings.Take (fetchSize), prefetches.Take (remaining));
        }

        bool CanExecuteNarrowPing (int accountId)
        {
            var defInbox = McFolder.GetDefaultInboxFolder (accountId);
            var defCal = McFolder.GetDefaultCalendarFolder (accountId);
            return !(defInbox.AsSyncMetaToClientExpected || defCal.AsSyncMetaToClientExpected);
        }

        public Tuple<PickActionEnum, AsCommand> Pick ()
        {
            var accountId = BEContext.Account.Id;
            var protocolState = BEContext.ProtocolState;
            var exeCtxt = NcApplication.Instance.ExecutionContext;
            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt) {
                // (FG) If the user has initiated a Search command, we do that.
                var search = McPending.QueryEligible (accountId).
                    Where (x => McPending.Operations.ContactSearch == x.Operation).FirstOrDefault ();
                if (null != search) {
                    return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.QOop, 
                        new AsSearchCommand (BEContext.ProtoControl, search));
                }
                // (FG) If the user has initiated a ItemOperations Fetch (body or attachment), we do that.
                var fetch = McPending.QueryEligible (accountId).
                    Where (x => 
                        McPending.Operations.AttachmentDownload == x.Operation ||
                            McPending.Operations.EmailBodyDownload == x.Operation ||
                            McPending.Operations.CalBodyDownload == x.Operation ||
                            McPending.Operations.ContactBodyDownload == x.Operation ||
                            McPending.Operations.TaskBodyDownload == x.Operation
                            ).FirstOrDefault ();
                if (null != fetch) {
                    return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.QOop,
                        new AsItemOperationsCommand (BEContext.ProtoControl, 
                            Tuple.Create<IEnumerable<McPending>, IEnumerable<Tuple<McAbstrItem, string>>> (
                                new List<McPending> { fetch }, new List<Tuple<McAbstrItem, string>> ())));
                }
            }
            // (FG, BG) If there is a SendMail, SmartForward or SmartReply in the pending queue, send it.
            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt ||
                NcApplication.ExecutionContextEnum.Background == exeCtxt) {
                var send = McPending.QueryEligible (accountId).
                    Where (x => 
                        McPending.Operations.EmailSend == x.Operation ||
                           McPending.Operations.EmailForward == x.Operation ||
                           McPending.Operations.EmailReply == x.Operation
                           ).FirstOrDefault ();
                if (null != send) {
                    AsCommand cmd = null;
                    switch (send.Operation) {
                    case McPending.Operations.EmailSend:
                        cmd = new AsSendMailCommand (BEContext.ProtoControl, send);
                        break;
                    case McPending.Operations.EmailForward:
                        cmd = new AsSmartForwardCommand (BEContext.ProtoControl, send);
                        break;
                    case McPending.Operations.EmailReply:
                        cmd = new AsSmartReplyCommand (BEContext.ProtoControl, send);
                        break;
                    default:
                        NcAssert.CaseError (send.Operation.ToString ());
                        break;
                    }
                    return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.QOop, cmd);
                }
            }
            // (FG, BG) Unless one of these conditions are met, perform a narrow Sync Command...
            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt ||
                NcApplication.ExecutionContextEnum.Background == exeCtxt) {
                var past120secs = DateTime.UtcNow.AddSeconds (-120);
                if (protocolState.LastNarrowSync < past120secs &&
                    (protocolState.LastPing < past120secs ||
                        !CanExecuteNarrowPing (accountId))) {
                    var nSyncKit = SyncKit (true, false);
                    if (null != nSyncKit) {
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Sync, 
                            new AsSyncCommand (BEContext.ProtoControl, nSyncKit));
                    }
                }
            }
            // (QS) If a narrow Sync hasn’t successfully completed in the last 60 seconds, 
            // perform a narrow Sync Command.
            if (NcApplication.ExecutionContextEnum.QuickSync == exeCtxt) {
                if (protocolState.LastNarrowSync < DateTime.UtcNow.AddSeconds (-120)) {
                    var nSyncKit = SyncKit (true, false);
                    if (null != nSyncKit) {
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Sync, 
                            new AsSyncCommand (BEContext.ProtoControl, nSyncKit));
                    }
                }
            }

            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt ||
                NcApplication.ExecutionContextEnum.Background == exeCtxt) {
                if (NcCommStatus.Instance.IsRateLimited (BEContext.Server.Id)) {
                    // (FG, BG) If we are rate-limited, and we can execute a narrow Ping command at the 
                    // current filter setting, execute a narrow Ping command.
                    if (CanExecuteNarrowPing (accountId)) {
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Ping, 
                            new AsPingCommand (BEContext.ProtoControl, PingKit (true)));
                    }
                    // (FG, BG) If we are rate-limited, and we can’t execute a narrow Ping command
                    // at the current filter setting, then wait.
                    else {
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Wait,
                            new AsWaitCommand (BEContext.ProtoControl, 120, false));
                    }
                }
                // I(FG, BG) If there are entries in the pending queue, execute the oldest.
                var next = McPending.QueryEligible (BEContext.Account.Id).FirstOrDefault ();
                if (null != next) {
                    AsCommand cmd = null;
                    switch (next.Operation) {
                    case McPending.Operations.FolderCreate:
                        cmd = new AsFolderCreateCommand (BEContext.ProtoControl, next);
                        break;
                    case McPending.Operations.FolderUpdate:
                        cmd = new AsFolderUpdateCommand (BEContext.ProtoControl, next);
                        break;
                    case McPending.Operations.FolderDelete:
                        cmd = new AsFolderDeleteCommand (BEContext.ProtoControl, next);
                        break;
                    // TODO: make move op n-ary.
                    case McPending.Operations.EmailMove:
                        cmd = new AsMoveItemsCommand (BEContext.ProtoControl, next, McAbstrFolderEntry.ClassCodeEnum.Email);
                        break;
                    case McPending.Operations.CalMove:
                        cmd = new AsMoveItemsCommand (BEContext.ProtoControl, next, McAbstrFolderEntry.ClassCodeEnum.Calendar);
                        break;
                    case McPending.Operations.ContactMove:
                        cmd = new AsMoveItemsCommand (BEContext.ProtoControl, next, McAbstrFolderEntry.ClassCodeEnum.Contact);
                        break;
                    case McPending.Operations.TaskMove:
                        cmd = new AsMoveItemsCommand (BEContext.ProtoControl, next, McAbstrFolderEntry.ClassCodeEnum.Tasks);
                        break;
                    case McPending.Operations.CalRespond:
                        cmd = new AsMeetingResponseCommand (BEContext.ProtoControl, next);
                        break;
                    default:
                        NcAssert.CaseError (next.Operation.ToString ());
                        break;
                    }
                    return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.QOop, cmd);
                }
                // (FG, BG) Choose eligible option by priority, split tie randomly...
                if (Power.Instance.PowerState != PowerStateEnum.Unknown &&
                    Power.Instance.BatteryLevel > 0.7) {
                    Tuple<IEnumerable<McPending>, IEnumerable<Tuple<McAbstrItem, string>>> fetchKit = null;
                    Tuple<uint, List<Tuple<McFolder, List<McPending>>>> syncKit = null;
                    if (NetStatusSpeedEnum.WiFi == NcCommStatus.Instance.Speed) {
                        fetchKit = FetchKit ();
                    }
                    syncKit = SyncKit (false, true);
                    if (null != fetchKit && (null == syncKit || 0.5 < CoinToss.NextDouble ())) {
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Fetch, 
                            new AsItemOperationsCommand (BEContext.ProtoControl, fetchKit));
                    }
                    if (null != syncKit) {
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Sync, 
                            new AsSyncCommand (BEContext.ProtoControl, syncKit));
                    }
                }
                var pingKit = PingKit (false);
                if (null == pingKit) {
                    pingKit = PingKit (true);
                }
                if (null != pingKit) {
                    return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Ping,
                        new AsPingCommand (BEContext.ProtoControl, pingKit));
                }
                return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Wait,
                    new AsWaitCommand (BEContext.ProtoControl, 120, false));
            }
            // (QS) Wait.
            if (NcApplication.ExecutionContextEnum.QuickSync == exeCtxt) {
                return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Wait,
                    new AsWaitCommand (BEContext.ProtoControl, 300, true));
            }
            NcAssert.True (false);
            return null;
        }
    }
}
