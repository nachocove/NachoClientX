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
    // Partial for test purposes.
    public partial class AsStrategy : IAsStrategy
    {
        public const int KBaseOverallWindowSize = 150;
        public const int KBasePerFolderWindowSize = 100;
        public const int KBaseFetchSize = 10;

        private class Scope
        {
            public enum ItemType
            {
                Email = 0,
                Cal = 1,
                Contact = 2,
                Last = Contact,
            };

            public enum EmailEnum
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

            public enum CalEnum
            {
                None,
                Def2w,
                All1m,
                All3m,
                All6m,
                AllInf,
            };

            public enum ContactEnum
            {
                None,
                RicInf,
                DefRicInf,
                AllInf,
            };

            public enum FlagEnum
            {
                None = 0,
                RicSynced = (1 << 0),
                NarrowSyncOk = (1 << 1),
            };

            private static ItemType[] ItemTypeSeq = new ItemType[] { ItemType.Email, ItemType.Cal, ItemType.Contact };

            private static int[,] Ladder = new int[,] {
                // { Email, Cal, Contact, Action }
                { (int)EmailEnum.None, (int)CalEnum.None, (int)ContactEnum.RicInf, (int)FlagEnum.None },
                { (int)EmailEnum.Def1d, (int)CalEnum.Def2w, (int)ContactEnum.RicInf, (int)FlagEnum.RicSynced },
                { (int)EmailEnum.Def3d, (int)CalEnum.Def2w, (int)ContactEnum.RicInf, (int)FlagEnum.RicSynced }, {
                    (int)EmailEnum.Def1w,
                    (int)CalEnum.Def2w,
                    (int)ContactEnum.RicInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk
                }, {
                    (int)EmailEnum.Def2w,
                    (int)CalEnum.Def2w,
                    (int)ContactEnum.DefRicInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk
                }, {
                    (int)EmailEnum.All1m,
                    (int)CalEnum.All1m,
                    (int)ContactEnum.DefRicInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk
                }, {
                    (int)EmailEnum.All3m,
                    (int)CalEnum.All3m,
                    (int)ContactEnum.AllInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk
                }, {
                    (int)EmailEnum.All6m,
                    (int)CalEnum.All6m,
                    (int)ContactEnum.AllInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk
                }, {
                    (int)EmailEnum.AllInf,
                    (int)CalEnum.AllInf,
                    (int)ContactEnum.AllInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk
                },
            };

            public static bool FlagIsSet (int rung, FlagEnum flag)
            {
                return ((int)flag == (((int)Ladder [rung, (int)ItemType.Last + 1]) & (int)flag));
            }

            public static List<ItemType> RequiredToAdvance (int rung)
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

            public static EmailEnum EmailScope (int rung)
            {
                return (EmailEnum)Ladder [rung, (int)ItemType.Email];
            }

            public static CalEnum CalScope (int rung)
            {
                return (CalEnum)Ladder [rung, (int)ItemType.Cal];
            }

            public static ContactEnum ContactScope (int rung)
            {
                return (ContactEnum)Ladder [rung, (int)ItemType.Contact];
            }
        }

        private IBEContext BEContext;
        private Random CoinToss;

        public AsStrategy (IBEContext beContext)
        {
            BEContext = beContext;
            CoinToss = new Random ();
        }

        private bool CanAdvance (int accountId, int rung)
        {
            var musts = Scope.RequiredToAdvance (rung);
            var folders = new List<McFolder> ();
            foreach (var must in musts) {
                switch (must) {
                case Scope.ItemType.Email:
                    folders.AddRange (EmailFolderListProvider (accountId, Scope.EmailScope (rung), false));
                    break;
                case Scope.ItemType.Cal:
                    folders.AddRange (CalFolderListProvider (accountId, Scope.CalScope (rung), false));
                    break;
                case Scope.ItemType.Contact:
                    folders.AddRange (ContactFolderListProvider (accountId, Scope.ContactScope (rung), false));
                    break;
                default:
                    NcAssert.CaseError (must.ToString ());
                    break;
                }
            }
            var stillExp = folders.Count (x => x.AsSyncMetaToClientExpected = true);
            Log.Info (Log.LOG_AS, "Strategy: CanAdvance: {0} folders with ToClientExpected.", stillExp);
            return (0 == stillExp);
        }

        private int AdvanceIfPossible (int accountId, int rung)
        {
            if (CanAdvance (accountId, rung)) {
                Log.Info (Log.LOG_AS, "Strategy:AdvanceIfPossible: {0} => {1}", rung, rung + 1);
                var protocolState = BEContext.ProtocolState;
                protocolState.StrategyRung++;
                protocolState.Update ();
                rung = protocolState.StrategyRung;
                if (Scope.FlagIsSet (rung, Scope.FlagEnum.RicSynced)) {
                    BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_RicInitialSyncCompleted));
                }
            }
            return rung;
        }

        private List<McFolder> EmailFolderListProvider (int accountId, Scope.EmailEnum scope, bool isNarrow)
        {
            switch (scope) {
            case Scope.EmailEnum.None:
                if (isNarrow) {
                    return new List<McFolder> () { McFolder.GetDefaultInboxFolder (accountId) };
                }
                return new List<McFolder> ();

            case Scope.EmailEnum.Def1d:
            case Scope.EmailEnum.Def1w:
            case Scope.EmailEnum.Def2w:
            case Scope.EmailEnum.Def3d:
                return new List<McFolder> () { McFolder.GetDefaultInboxFolder (accountId) };

            case Scope.EmailEnum.All1m:
            case Scope.EmailEnum.All3m:
            case Scope.EmailEnum.All6m:
            case Scope.EmailEnum.AllInf:
                if (isNarrow) {
                    return new List<McFolder> () { McFolder.GetDefaultInboxFolder (accountId) };
                }
                return McFolder.ServerEndQueryAll (accountId).Where (f => 
                    Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) ==
                McAbstrFolderEntry.ClassCodeEnum.Email).ToList ();

            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        private List<McFolder> CalFolderListProvider (int accountId, Scope.CalEnum scope, bool isNarrow)
        {
            switch (scope) {
            case Scope.CalEnum.None:
                if (isNarrow) {
                    return new List<McFolder> () { McFolder.GetDefaultCalendarFolder (accountId) };
                }
                return new List<McFolder> ();

            case Scope.CalEnum.Def2w:
                return new List<McFolder> () { McFolder.GetDefaultCalendarFolder (accountId) };

            case Scope.CalEnum.All1m:
            case Scope.CalEnum.All3m:
            case Scope.CalEnum.All6m:
            case Scope.CalEnum.AllInf:
                if (isNarrow) {
                    return new List<McFolder> () { McFolder.GetDefaultCalendarFolder (accountId) };
                }
                return McFolder.ServerEndQueryAll (accountId).Where (f => 
                    Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) ==
                McAbstrFolderEntry.ClassCodeEnum.Calendar).ToList ();

            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        private List<McFolder> ContactFolderListProvider (int accountId, Scope.ContactEnum scope, bool isNarrow)
        {
            if (isNarrow) {
                return new List<McFolder> ();
            }
            switch (scope) {
            case Scope.ContactEnum.None:
                return new List<McFolder> ();

            case Scope.ContactEnum.RicInf:
                return new List<McFolder> () { McFolder.GetRicContactFolder (accountId) };

            case Scope.ContactEnum.DefRicInf:
                return new List<McFolder> () { McFolder.GetRicContactFolder (accountId),
                    McFolder.GetDefaultContactFolder (accountId)
                };

            case Scope.ContactEnum.AllInf:
                return McFolder.ServerEndQueryAll (accountId).Where (f => 
                    Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) ==
                McAbstrFolderEntry.ClassCodeEnum.Contact).ToList ();

            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        // function returning all folders at current level. Does NOT evaluate ToClientExpected.
        private List<McFolder> FolderListProvider (int accountId, int rung, bool isNarrow)
        {
            var result = new List<McFolder> ();
            result.AddRange (EmailFolderListProvider (accountId, Scope.EmailScope (rung), isNarrow));
            result.AddRange (CalFolderListProvider (accountId, Scope.CalScope (rung), isNarrow));
            result.AddRange (ContactFolderListProvider (accountId, Scope.ContactScope (rung), isNarrow));
            NcAssert.True (Scope.ItemType.Last == Scope.ItemType.Contact);
            return result;
        }

        private Tuple<Xml.Provision.MaxAgeFilterCode, int> EmailParametersProvider (McFolder folder, Scope.EmailEnum scope, bool isNarrow, int perFolderWindowSize)
        {
            switch (scope) {
            case Scope.EmailEnum.None:
                if (isNarrow) {
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneDay_1, perFolderWindowSize);
                }
                NcAssert.True (false);
                return null;
            case Scope.EmailEnum.Def1d:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneDay_1, perFolderWindowSize);
            case Scope.EmailEnum.Def3d:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.ThreeDays_2, perFolderWindowSize);
            case Scope.EmailEnum.Def1w:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneWeek_3, perFolderWindowSize);
            case Scope.EmailEnum.Def2w:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.TwoWeeks_4, perFolderWindowSize);
            case Scope.EmailEnum.All1m:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneMonth_5, perFolderWindowSize);
            case Scope.EmailEnum.All3m:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.ThreeMonths_6, perFolderWindowSize);
            case Scope.EmailEnum.All6m:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SixMonths_7, perFolderWindowSize);
            case Scope.EmailEnum.AllInf:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, perFolderWindowSize);
            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        private Tuple<Xml.Provision.MaxAgeFilterCode, int> CalParametersProvider (McFolder folder, Scope.CalEnum scope, bool isNarrow, int perFolderWindowSize)
        {
            switch (scope) {
            case Scope.CalEnum.None:
                if (isNarrow) {
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.TwoWeeks_4, perFolderWindowSize);
                }
                NcAssert.True (false);
                return null;
            case Scope.CalEnum.Def2w:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.TwoWeeks_4, perFolderWindowSize);
            case Scope.CalEnum.All1m:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneMonth_5, perFolderWindowSize);
            case Scope.CalEnum.All3m:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.ThreeMonths_6, perFolderWindowSize);
            case Scope.CalEnum.All6m:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SixMonths_7, perFolderWindowSize);
            case Scope.CalEnum.AllInf:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, perFolderWindowSize);
            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        private Tuple<Xml.Provision.MaxAgeFilterCode, int> ContactParametersProvider (McFolder folder, Scope.ContactEnum scope, bool isNarrow, int perFolderWindowSize)
        {
            switch (scope) {
            case Scope.ContactEnum.None:
                if (isNarrow) {
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, perFolderWindowSize);
                }
                NcAssert.True (false);
                return null;
            case Scope.ContactEnum.RicInf:
            case Scope.ContactEnum.DefRicInf:
            case Scope.ContactEnum.AllInf:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, perFolderWindowSize);
            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        private Tuple<Xml.Provision.MaxAgeFilterCode, int> ParametersProvider (McFolder folder, int rung, bool isNarrow)
        {
            int perFolderWindowSize = KBasePerFolderWindowSize;
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
                return EmailParametersProvider (folder, Scope.EmailScope (rung), isNarrow, perFolderWindowSize);

            case McFolder.ClassCodeEnum.Calendar:
                return CalParametersProvider (folder, Scope.CalScope (rung), isNarrow, perFolderWindowSize);

            case McFolder.ClassCodeEnum.Contact:
                return ContactParametersProvider (folder, Scope.ContactScope (rung), isNarrow, perFolderWindowSize);

            default:
                NcAssert.CaseError (string.Format ("{0}", folder.Type));
                return null;
            }
        }

        private List<McFolder> AllSyncedFolders (int accountId)
        {
            // A folder must be created on the server before it can be the subject of a Sync/Ping.
            // Exclude the types of folders we don't yet Sync.
            return McFolder.ServerEndQueryAll (accountId).Where (x => 
                Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (x.Type) ==
            McAbstrFolderEntry.ClassCodeEnum.Email ||
            Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (x.Type) ==
            McAbstrFolderEntry.ClassCodeEnum.Calendar ||
            Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (x.Type) ==
            McAbstrFolderEntry.ClassCodeEnum.Contact).ToList ();
        }

        public SyncKit GenSyncKit (int accountId, McProtocolState protocolState, bool cantBeEmpty)
        {
            return GenSyncKit (accountId, protocolState, false, cantBeEmpty);
        }

        private SyncKit GenNarrowSyncKit (List<McFolder> folders, int rung, int overallWindowSize)
        {
            var perFolders = new List<SyncKit.PerFolder> ();
            foreach (var folder in folders) {
                if (!folder.AsSyncMetaToClientExpected) {
                    folder.AsSyncMetaToClientExpected = true;
                    folder.Update ();
                }
                var parms = ParametersProvider (folder, rung, true);
                perFolders.Add (new SyncKit.PerFolder () {
                    Folder = folder,
                    Commands = new List<McPending> (),
                    FilterCode = parms.Item1,
                    WindowSize = parms.Item2,
                    GetChanges = true,
                });
            }
            return new SyncKit () {
                OverallWindowSize = overallWindowSize,
                PerFolders = perFolders,
            };
        }

        // Returns null if nothing to do.
        private SyncKit GenSyncKit (int accountId, McProtocolState protocolState, bool isNarrow, bool cantBeEmpty)
        {
            var rung = protocolState.StrategyRung;
            int overallWindowSize = KBaseOverallWindowSize;
            switch (NcCommStatus.Instance.Speed) {
            case NetStatusSpeedEnum.CellFast:
                overallWindowSize *= 2;
                break;
            case NetStatusSpeedEnum.WiFi:
                overallWindowSize *= 3;
                break;
            }
            var rawFolders = FolderListProvider (accountId, rung, isNarrow);
            var eligibleForGetChanges = rawFolders.Where (x => true == x.AsSyncMetaToClientExpected).ToList ();
            // Don't bother with commands when doing a narrow Sync. Get new messages, get out.
            if (isNarrow) {
                return GenNarrowSyncKit (rawFolders, rung, overallWindowSize);
            }
            // Wide Sync below.
            rung = AdvanceIfPossible (accountId, rung);
            List<McPending> commands;
            bool inSerialMode = false;
            bool issuedAtLeast1 = false;
            var retList = new List<SyncKit.PerFolder> ();
            var limit = protocolState.AsSyncLimit;

            // Loop through all synced folders. Choose those that exepect to-client items and
            // those that have waiting pending items.
            foreach (var folder in AllSyncedFolders (accountId)) {
                if (0 >= limit) {
                    // TODO: prefer default folders in this scenario.
                    break;
                }
                // See if we can and should do GetChanges. O(N**2), small N.
                bool getChanges = false;
                if (null != eligibleForGetChanges.FirstOrDefault (x => x.Id == folder.Id)) {
                    if (folder.AsSyncMetaToClientExpected) {
                        getChanges = (McFolder.AsSyncKey_Initial != folder.AsSyncKey);
                    } else {
                        getChanges = false;
                    }
                }
                // See if we can complete some McPending.
                commands = new List<McPending> ();
                // If we are in serial mode, we will issue no more pendings.
                if (McFolder.AsSyncKey_Initial != folder.AsSyncKey && !inSerialMode) {
                    var rawPendings = McPending.QueryEligibleByFolderServerId (accountId, folder.ServerId);
                    commands = rawPendings.Where (p => AsSyncCommand.IsSyncCommand (p.Operation)).ToList ();
                    if (issuedAtLeast1) {
                        // If we have issuedAtLeast1, then we exclude any serial pendings.
                        commands = commands.Where (p => !p.DeferredSerialIssueOnly).ToList ();
                    } else if (0 < commands.Count) {
                        // If we have not issuedAtLeast1, then grab the 1st and decide based on that.
                        var first = commands.First ();
                        if (first.DeferredSerialIssueOnly) {
                            inSerialMode = true;
                            commands = new List<McPending> () { first };
                        } else {
                            commands = commands.Where (p => !p.DeferredSerialIssueOnly).ToList ();
                        }
                        issuedAtLeast1 = true;
                    }
                }
                // if initial-key || some pending || GetChanges, include folder in Sync.
                if (McFolder.AsSyncKey_Initial == folder.AsSyncKey || getChanges || 0 < commands.Count) {
                    var parms = ParametersProvider (folder, rung, false);
                    var perFolder = new SyncKit.PerFolder () {
                        Folder = folder,
                        Commands = commands,
                        FilterCode = parms.Item1,
                        WindowSize = parms.Item2,
                        GetChanges = getChanges,
                    };
                    retList.Add (perFolder);
                    --limit;
                }
            }
            if (0 != retList.Count) {
                return new SyncKit () {
                    OverallWindowSize = overallWindowSize,
                    PerFolders = retList,
                };
            }
            if (cantBeEmpty) {
                return GenNarrowSyncKit (eligibleForGetChanges, rung, overallWindowSize);
            }
            return null;
        }

        private PingKit GenPingKit (int accountId, McProtocolState protocolState, bool isNarrow)
        {
            var folders = FolderListProvider (accountId, protocolState.StrategyRung, isNarrow);
            if (folders.Any (x => true == x.AsSyncMetaToClientExpected)) {
                return null;
            }
            if (protocolState.MaxFolders >= folders.Count) {
                return new PingKit () { Folders = folders };
            }
            // If we have too many folders, then whittle down the list, but keep default inbox & cal.
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
            // Prefer the least-recently-ping'd.
            var stalest = folders.OrderBy (x => x.AsSyncLastPing).Take ((int)protocolState.MaxFolders - fewer.Count);
            fewer.AddRange (stalest);
            return new PingKit () { Folders = fewer };
        }

        // Returns null if nothing to do.
        private FetchKit GenFetchKit (int accountId)
        {
            var remaining = KBaseFetchSize;
            var fetchBodies = new List<FetchKit.FetchBody> ();
            var emails = McEmailMessage.QueryNeedsFetch (accountId, remaining, 0.7).ToList ();
            foreach (var email in emails) {
                // TODO: all this can be one SQL JOIN.
                var folders = McFolder.QueryByFolderEntryId<McEmailMessage> (accountId, email.Id);
                fetchBodies.Add (new FetchKit.FetchBody () {
                    ServerId = email.ServerId,
                    ParentId = folders [0].ServerId,
                });
            }
            remaining -= fetchBodies.Count;
            List<McAttachment> fetchAtts = new List<McAttachment> ();
            if (0 < remaining) {
                fetchAtts = McAttachment.QueryNeedsFetch (accountId, remaining, 0.9, 1024 * 1024).ToList ();
            }
            if (0 < fetchBodies.Count || 0 < fetchAtts.Count) {
                Log.Info (Log.LOG_AS, "GenFetchKit: {0} emails, {1} attachments.", fetchBodies.Count, fetchAtts.Count);
                return new FetchKit () {
                    FetchBodies = fetchBodies,
                    FetchAttachments = fetchAtts,
                };
            }
            Log.Info (Log.LOG_AS, "GenFetchKit: nothing to do.");
            return null;
        }

        bool NarrowFoldersNoToClientExpected (int accountId)
        {
            var defInbox = McFolder.GetDefaultInboxFolder (accountId);
            var defCal = McFolder.GetDefaultCalendarFolder (accountId);
            var retval = !(defInbox.AsSyncMetaToClientExpected || defCal.AsSyncMetaToClientExpected);
            Log.Info (Log.LOG_AS, "NarrowFoldersNoToClientExpected is {0}", retval);
            return retval;
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
                    Log.Info (Log.LOG_AS, "Strategy:FG:Search");
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
                    Log.Info (Log.LOG_AS, "Strategy:FG:Fetch");
                    return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.QOop,
                        new AsItemOperationsCommand (BEContext.ProtoControl,
                            new FetchKit () {
                                FetchBodies = new List<FetchKit.FetchBody> (),
                                FetchAttachments = new List<McAttachment> (),
                                Pendings = new List<McPending> { fetch },
                            }));
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
                    Log.Info (Log.LOG_AS, "Strategy:FG/BG:Send");
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
                if (Scope.FlagIsSet (protocolState.StrategyRung, Scope.FlagEnum.NarrowSyncOk) &&
                    protocolState.LastNarrowSync < past120secs &&
                    (protocolState.LastPing < past120secs ||
                    !NarrowFoldersNoToClientExpected (accountId))) {
                    Log.Info (Log.LOG_AS, "Strategy:FG/BG:Narrow Sync...");
                    var nSyncKit = GenSyncKit (accountId, protocolState, true, false);
                    if (null != nSyncKit) {
                        Log.Info (Log.LOG_AS, "Strategy:FG/BG:...SyncKit");
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Sync, 
                            new AsSyncCommand (BEContext.ProtoControl, nSyncKit));
                    }
                }
            }
            // (QS) If a narrow Sync hasn’t successfully completed in the last 60 seconds, 
            // perform a narrow Sync Command.
            if (NcApplication.ExecutionContextEnum.QuickSync == exeCtxt) {
                if (protocolState.LastNarrowSync < DateTime.UtcNow.AddSeconds (-120)) {
                    var nSyncKit = GenSyncKit (accountId, protocolState, true, false);
                    Log.Info (Log.LOG_AS, "Strategy:QS:Narrow Sync...");
                    if (null != nSyncKit) {
                        Log.Info (Log.LOG_AS, "Strategy:QS:...SyncKit");
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
                    if (NarrowFoldersNoToClientExpected (accountId)) {
                        Log.Info (Log.LOG_AS, "Strategy:FG/BG,RL:Narrow Ping");
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Ping, 
                            new AsPingCommand (BEContext.ProtoControl, GenPingKit (accountId, protocolState, true)));
                    }
                    // (FG, BG) If we are rate-limited, and we can’t execute a narrow Ping command
                    // at the current filter setting, then wait.
                    else {
                        Log.Info (Log.LOG_AS, "Strategy:FG/BG,RL:Wait");
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Wait,
                            new AsWaitCommand (BEContext.ProtoControl, 120, false));
                    }
                }
                // I(FG, BG) If there are entries in the pending queue, execute the oldest.
                var next = McPending.QueryEligible (accountId).FirstOrDefault ();
                if (null != next) {
                    Log.Info (Log.LOG_AS, "Strategy:FG/BG:QOp:{0}", next.Operation.ToString ());
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
                    FetchKit fetchKit = null;
                    SyncKit syncKit = null;
                    if (NetStatusSpeedEnum.WiFi == NcCommStatus.Instance.Speed) {
                        fetchKit = GenFetchKit (accountId);
                    }
                    syncKit = GenSyncKit (accountId, protocolState, false, false);
                    if (null != fetchKit && (null == syncKit || 0.5 < CoinToss.NextDouble ())) {
                        Log.Info (Log.LOG_AS, "Strategy:FG/BG:Fetch");
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Fetch, 
                            new AsItemOperationsCommand (BEContext.ProtoControl, fetchKit));
                    }
                    if (null != syncKit) {
                        Log.Info (Log.LOG_AS, "Strategy:FG/BG:Sync");
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Sync, 
                            new AsSyncCommand (BEContext.ProtoControl, syncKit));
                    }
                }
                var pingKit = GenPingKit (accountId, protocolState, false);
                if (null == pingKit) {
                    Log.Info (Log.LOG_AS, "Strategy:FG/BG:PingKit must be narrow.");
                    pingKit = GenPingKit (accountId, protocolState, true);
                }
                if (null != pingKit) {
                    Log.Info (Log.LOG_AS, "Strategy:FG/BG:Ping");
                    return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Ping,
                        new AsPingCommand (BEContext.ProtoControl, pingKit));
                }
                Log.Info (Log.LOG_AS, "Strategy:FG/BG:Wait");
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
