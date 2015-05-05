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
        public const int KBaseOverallWindowSize = 25;
        public const int KBasePerFolderWindowSize = 20;
        public const int KBaseFetchSize = 5;

        public enum LadderChoiceEnum
        {
            Production,
            Test,
        };

        // Public for testing only.
        public class Scope
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
                None = 0,
                Def1d,
                Def3d,
                Def1w,
                Def2w,
                Def1m,
                All1m,
                AllInf,

            };

            public enum CalEnum
            {
                None = 0,
                Def2w,
                Def1m,
                All1m,
                All3m,
                All6m,
                AllInf,
            };

            public enum ContactEnum
            {
                None = 0,
                RicInf,
                DefRicInf,
                AllInf,
            };

            public enum FlagEnum
            {
                None = 0,
                RicSynced = (1 << 0),
                NarrowSyncOk = (1 << 1),
                IgnorePower = (1 << 2),
            };

            private static ItemType[] ItemTypeSeq = new ItemType[] { ItemType.Email, ItemType.Cal, ItemType.Contact };

            public static int[,] Ladder;

            /* We achived stability while using this setup. Going back to try the old graduated sync filter scheme.
            public static int[,] ProductionLadder = new int[,] {
                // { Email, Cal, Contact, Action }
                { (int)EmailEnum.None, (int)CalEnum.None, (int)ContactEnum.RicInf, (int)FlagEnum.IgnorePower }, {
                    (int)EmailEnum.Def1m,
                    (int)CalEnum.Def1m,
                    (int)ContactEnum.DefRicInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk | (int)FlagEnum.IgnorePower
                }, {
                    (int)EmailEnum.All1m,
                    (int)CalEnum.All1m,
                    (int)ContactEnum.AllInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk
                },
            };
            */

            public static int[,] ProductionLadder = new int[,] {
                // { Email, Cal, Contact, Action }
                { (int)EmailEnum.None, (int)CalEnum.None, (int)ContactEnum.RicInf, (int)FlagEnum.IgnorePower }, {
                    (int)EmailEnum.Def1d,
                    (int)CalEnum.Def2w,
                    (int)ContactEnum.RicInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.IgnorePower
                }, {
                    (int)EmailEnum.Def3d,
                    (int)CalEnum.Def2w,
                    (int)ContactEnum.RicInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.IgnorePower
                }, {
                    (int)EmailEnum.Def1w,
                    (int)CalEnum.Def2w,
                    (int)ContactEnum.RicInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk | (int)FlagEnum.IgnorePower
                }, {
                    (int)EmailEnum.Def2w,
                    (int)CalEnum.Def2w,
                    (int)ContactEnum.DefRicInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk | (int)FlagEnum.IgnorePower
                }, {
                    (int)EmailEnum.All1m,
                    (int)CalEnum.All1m,
                    (int)ContactEnum.AllInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk
                }, {
                    (int)EmailEnum.All1m,
                    (int)CalEnum.All3m,
                    (int)ContactEnum.AllInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk
                }, {
                    (int)EmailEnum.AllInf,
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

            public static int[,] TestLadder = new int[,] {
                // { Email, Cal, Contact, Action }
                { (int)EmailEnum.None, (int)CalEnum.None, (int)ContactEnum.RicInf, (int)FlagEnum.IgnorePower }, {
                    (int)EmailEnum.Def1d,
                    (int)CalEnum.Def2w,
                    (int)ContactEnum.RicInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.IgnorePower
                }, {
                    (int)EmailEnum.Def3d,
                    (int)CalEnum.Def2w,
                    (int)ContactEnum.RicInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.IgnorePower
                }, {
                    (int)EmailEnum.Def1w,
                    (int)CalEnum.Def2w,
                    (int)ContactEnum.RicInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk | (int)FlagEnum.IgnorePower
                }, {
                    (int)EmailEnum.Def2w,
                    (int)CalEnum.Def2w,
                    (int)ContactEnum.DefRicInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk | (int)FlagEnum.IgnorePower
                }, {
                    (int)EmailEnum.All1m,
                    (int)CalEnum.All1m,
                    (int)ContactEnum.DefRicInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk
                }, {
                    (int)EmailEnum.All1m,
                    (int)CalEnum.All3m,
                    (int)ContactEnum.AllInf,
                    (int)FlagEnum.RicSynced | (int)FlagEnum.NarrowSyncOk
                }, {
                    (int)EmailEnum.AllInf,
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

            public static int MaxRung ()
            {
                return Ladder.GetLength (0) - 1;
            }

            public static int StrategyRung (McProtocolState protocolState)
            {
                // This is meant to fix an out-of-bounds value on-the-fly in an upgrade scenario.
                // TODO - delete this once there is a general approach to migration.
                if (protocolState.StrategyRung > MaxRung ()) {
                    protocolState.StrategyRung = MaxRung ();
                    protocolState.Update ();
                }
                return protocolState.StrategyRung;
            }

            public static bool FlagIsSet (int rung, FlagEnum flag)
            {
                return ((int)flag == (((int)Ladder [rung, (int)ItemType.Last + 1]) & (int)flag));
            }

            public static List<ItemType> RequiredToAdvance (int rung)
            {
                var retval = new List<ItemType> ();
                // Those that step up in the next run must complete to advance.
                foreach (int track in ItemTypeSeq) {
                    if (0 != (int)Ladder [rung, track] && // Exclude None(s).
                        Ladder [rung, track] != Ladder [rung + 1, track]) {
                        retval.Add ((ItemType)track);
                    }
                }
                // If there are no such, then the non-None(s) are required to advance.
                if (0 == retval.Count) {
                    foreach (int track in ItemTypeSeq) {
                        if (0 != (int)Ladder [rung, track]) {
                            retval.Add ((ItemType)track);
                        }
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

        public AsStrategy (IBEContext beContext, LadderChoiceEnum ladder)
        {
            BEContext = beContext;
            CoinToss = new Random ();
            switch (ladder) {
            case LadderChoiceEnum.Test:
                Scope.Ladder = Scope.TestLadder;
                break;
            case LadderChoiceEnum.Production:
                Scope.Ladder = Scope.ProductionLadder;
                break;
            default:
                NcAssert.CaseError (ladder.ToString ());
                break;
            }
        }

        public AsStrategy (IBEContext beContext) : this (beContext, LadderChoiceEnum.Production)
        {
        }

        public bool CanAdvance (int accountId, int rung)
        {
            if (rung >= Scope.MaxRung ()) {
                return false;
            }
            var musts = Scope.RequiredToAdvance (rung);
            // compute the set of folders that need !AsSyncMetaToClientExpected before we advance to next rung.
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
            var stillExp = folders.Count (x => true == x.AsSyncMetaToClientExpected);
            Log.Info (Log.LOG_AS, "Strategy: CanAdvance: {0} folders with ToClientExpected.", stillExp);
            // If there are any AsSyncMetaToClientExpected, we know now that we can't advance.
            if (0 != stillExp) {
                return false;
            }
            // for each of those folders, now see if there are any associated McPending. If yes, we need
            // to process those before we advance.
            foreach (var folder in folders) {
                var commands = EligibleForSync (accountId, folder);
                if (0 != commands.Count) {
                    Log.Info (Log.LOG_AS, "Strategy: CanAdvance: {0} still has {0} commands.", folder.ServerId, commands.Count);
                    return false;
                }
            }
            // Would the next run take us beyond the configured email sync scope?
            var account = McAccount.QueryById<McAccount> (accountId);
            var nextEmail = Scope.EmailScope (rung + 1);
            switch (account.DaysToSyncEmail) {
            case Xml.Provision.MaxAgeFilterCode.OneMonth_5:
                if (nextEmail == Scope.EmailEnum.AllInf) {
                    Log.Info (Log.LOG_AS, "Strategy: CanAdvance: inhibiting advance past rung {0}", rung);
                    return false;
                }
                break;
            case Xml.Provision.MaxAgeFilterCode.SyncAll_0:
                // No stopping needed!
                break;
            default:
                Log.Error (Log.LOG_AS, "Strategy: CanAdvance: ignoring invalid account.DaysToSyncEmail {0}", account.DaysToSyncEmail);
                break;
            }
            Log.Info (Log.LOG_AS, "Strategy: CanAdvance: true.");
            return true;
        }

        public int AdvanceIfPossible (int accountId, int rung)
        {
            if (CanAdvance (accountId, rung)) {
                Log.Info (Log.LOG_AS, "Strategy:AdvanceIfPossible: {0} => {1}", rung, rung + 1);
                var protocolState = BEContext.ProtocolState;
                protocolState.StrategyRung++;
                protocolState.Update ();
                rung = protocolState.StrategyRung;
                var folders = FolderListProvider (accountId, rung, false);
                foreach (var iterFolder in folders) {
                    iterFolder.UpdateSet_AsSyncMetaToClientExpected (true);
                }
                if (Scope.FlagIsSet (rung, Scope.FlagEnum.RicSynced)) {
                    BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_RicInitialSyncCompleted));
                }
            }
            return rung;
        }

        public List<McFolder> EmailFolderListProvider (int accountId, Scope.EmailEnum scope, bool isNarrow)
        {
            McFolder inbox;
            switch (scope) {
            case Scope.EmailEnum.None:
                if (isNarrow) {
                    inbox = McFolder.GetDefaultInboxFolder (accountId);
                    if (null != inbox) {
                        return new List<McFolder> () { inbox };
                    }
                }
                return new List<McFolder> ();

            case Scope.EmailEnum.Def1d:
            case Scope.EmailEnum.Def1w:
            case Scope.EmailEnum.Def2w:
            case Scope.EmailEnum.Def3d:
            case Scope.EmailEnum.Def1m:
                inbox = McFolder.GetDefaultInboxFolder (accountId);
                if (null != inbox) {
                    return new List<McFolder> () { inbox };
                }
                return new List<McFolder> ();

            case Scope.EmailEnum.All1m:
            case Scope.EmailEnum.AllInf:
                if (isNarrow) {
                    inbox = McFolder.GetDefaultInboxFolder (accountId);
                    if (null != inbox) {
                        return new List<McFolder> () { inbox };
                    }
                    return new List<McFolder> ();
                }
                return McFolder.ServerEndQueryAll (accountId).Where (f => 
                    Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) ==
                McAbstrFolderEntry.ClassCodeEnum.Email).ToList ();

            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        public List<McFolder> CalFolderListProvider (int accountId, Scope.CalEnum scope, bool isNarrow)
        {
            McFolder cal;
            switch (scope) {
            case Scope.CalEnum.None:
                if (isNarrow) {
                    cal = McFolder.GetDefaultCalendarFolder (accountId);
                    if (null != cal) {
                        return new List<McFolder> () { cal };
                    }
                }
                return new List<McFolder> ();

            case Scope.CalEnum.Def2w:
            case Scope.CalEnum.Def1m:
                cal = McFolder.GetDefaultCalendarFolder (accountId);
                if (null != cal) {
                    return new List<McFolder> () { cal };
                }
                return new List<McFolder> ();

            case Scope.CalEnum.All1m:
            case Scope.CalEnum.All3m:
            case Scope.CalEnum.All6m:
            case Scope.CalEnum.AllInf:
                if (isNarrow) {
                    cal = McFolder.GetDefaultCalendarFolder (accountId);
                    if (null != cal) {
                        return new List<McFolder> () { cal };
                    }
                    return new List<McFolder> ();
                }
                return McFolder.ServerEndQueryAll (accountId).Where (f => 
                    Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) ==
                McAbstrFolderEntry.ClassCodeEnum.Calendar).ToList ();

            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        public List<McFolder> ContactFolderListProvider (int accountId, Scope.ContactEnum scope, bool isNarrow)
        {
            if (isNarrow) {
                return new List<McFolder> ();
            }
            switch (scope) {
            case Scope.ContactEnum.None:
                return new List<McFolder> ();

            case Scope.ContactEnum.RicInf:
                var ric = McFolder.GetRicContactFolder (accountId);
                if (null != ric) {
                    return new List<McFolder> () { ric };
                } else {
                    return new List<McFolder> ();
                }

            case Scope.ContactEnum.DefRicInf:
                ric = McFolder.GetRicContactFolder (accountId);
                McFolder contacts = McFolder.GetDefaultContactFolder (accountId);
                var list = new List<McFolder> ();
                if (null != ric) {
                    list.Add (ric);
                }
                if (null != contacts) {
                    list.Add (contacts);
                }
                return list;

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
        public List<McFolder> FolderListProvider (int accountId, int rung, bool isNarrow)
        {
            var result = new List<McFolder> ();
            result.AddRange (EmailFolderListProvider (accountId, Scope.EmailScope (rung), isNarrow));
            result.AddRange (CalFolderListProvider (accountId, Scope.CalScope (rung), isNarrow));
            result.AddRange (ContactFolderListProvider (accountId, Scope.ContactScope (rung), isNarrow));
            NcAssert.True (Scope.ItemType.Last == Scope.ItemType.Contact);
            return result;
        }

        public Tuple<Xml.Provision.MaxAgeFilterCode, int> EmailParametersProvider (McFolder folder, Scope.EmailEnum scope, bool isNarrow, int perFolderWindowSize)
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
            case Scope.EmailEnum.Def1m:
            case Scope.EmailEnum.All1m:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneMonth_5, perFolderWindowSize);
            case Scope.EmailEnum.AllInf:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, perFolderWindowSize);
            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        public Tuple<Xml.Provision.MaxAgeFilterCode, int> CalParametersProvider (McFolder folder, Scope.CalEnum scope, bool isNarrow, int perFolderWindowSize)
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
            case Scope.CalEnum.Def1m:
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

        public Tuple<Xml.Provision.MaxAgeFilterCode, int> ContactParametersProvider (McFolder folder, Scope.ContactEnum scope, bool isNarrow, int perFolderWindowSize)
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

        public Tuple<Xml.Provision.MaxAgeFilterCode, int> ParametersProvider (McFolder folder, int rung, bool isNarrow)
        {
            int perFolderWindowSize = KBasePerFolderWindowSize;
            switch (NcCommStatus.Instance.Speed) {
            case NetStatusSpeedEnum.CellFast_1:
                perFolderWindowSize *= 2;
                break;
            case NetStatusSpeedEnum.WiFi_0:
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

        public List<McFolder> AllSyncedFolders (int accountId)
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

        public SyncKit GenSyncKit (int accountId, McProtocolState protocolState)
        {
            return GenSyncKit (accountId, protocolState, SyncMode.Wide);
        }

        public SyncKit GenUserSyncKit (McFolder folder, int rung, int overallWindowSize, McPending pending)
        {
            if (null == folder) {
                Log.Error (Log.LOG_AS, "GenUserSyncKit called with null folder.");
                return null;
            }
            var syncKit = GenNarrowSyncKit (new List<McFolder> () { folder }, rung, overallWindowSize);
            if (null == syncKit) {
                return null;
            }
            syncKit.PerFolders.First ().Commands.Add (pending);
            return syncKit;
        }

        public SyncKit GenNarrowSyncKit (List<McFolder> folders, int rung, int overallWindowSize)
        {
            if (0 == folders.Count) {
                return null;
            }
            var perFolders = new List<SyncKit.PerFolder> ();
            foreach (var iterFolder in folders) {
                var folder = iterFolder;
                if (!folder.AsSyncMetaToClientExpected) {
                    folder = folder.UpdateSet_AsSyncMetaToClientExpected (true);
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
                IsNarrow = true,
            };
        }

        private static List<McPending> EligibleForSync (int accountId, McFolder folder)
        {
            var rawPendings = McPending.QueryEligibleByFolderServerId (accountId, folder.ServerId);
            return rawPendings.Where (p => AsSyncCommand.IsSyncCommand (p.Operation)).ToList ();
        }

        public enum SyncMode { Wide, Narrow, Directed };

        // Returns null if nothing to do.
        public SyncKit GenSyncKit (int accountId, McProtocolState protocolState, SyncMode syncMode, McPending pending = null)
        {
            var rung = Scope.StrategyRung (protocolState);
            int overallWindowSize = KBaseOverallWindowSize;
            switch (NcCommStatus.Instance.Speed) {
            case NetStatusSpeedEnum.CellFast_1:
                overallWindowSize *= 2;
                break;
            case NetStatusSpeedEnum.WiFi_0:
                overallWindowSize *= 3;
                break;
            }

            if (SyncMode.Directed == syncMode) {
                NcAssert.NotNull (pending);
                NcAssert.True (McPending.Operations.Sync == pending.Operation);
                var directed = McFolder.QueryByServerId<McFolder> (accountId, pending.ServerId);
                if (null != directed) {
                    return GenUserSyncKit (directed, rung, overallWindowSize, pending);
                }
                Log.Error (Log.LOG_AS, "GenSyncKit:Directed: Can't find folder.");
                syncMode = SyncMode.Narrow;
                return null;
            }

            var rawFolders = FolderListProvider (accountId, rung, SyncMode.Narrow == syncMode);
            // Don't bother with commands when doing a narrow Sync. Get new messages, get out.
            if (SyncMode.Narrow == syncMode) {
                return GenNarrowSyncKit (rawFolders, rung, overallWindowSize);
            }
            // Wide Sync below.
            List<McPending> commands;
            bool inSerialMode = false;
            bool issuedAtLeast1 = false;
            var retList = new List<SyncKit.PerFolder> ();
            var limit = protocolState.AsSyncLimit;

            // Loop through all synced folders. Choose those that exepect to-client items and
            // those that have waiting pending items. We don't just use rawFolders because we may need
            // to perform an operation for a folder that isn't in the set of synced folders on this rung.
            foreach (var folder in AllSyncedFolders (accountId)) {
                if (0 >= limit) {
                    // TODO: prefer default folders in this scenario.
                    // TODO: prefer least-recently-synced folders, too.
                    break;
                }
                // See if we can and should do GetChanges. Only rawFolders are eligible. O(N**2) alert.
                bool getChanges = false;
                if (null != rawFolders.FirstOrDefault (x => x.Id == folder.Id)) {
                    if (folder.AsSyncMetaToClientExpected) {
                        getChanges = (McFolder.AsSyncKey_Initial != folder.AsSyncKey);
                    } else {
                        getChanges = false;
                    }
                }
                /* See if we can complete some McPending.
                 * From MS-ASCMD:
                 * If the client device has not yet synchronized a folder, there SHOULD be no client-side changes. 
                 * The device MUST synchronize the full contents of a given folder, and then have its changes, 
                 * additions, and deletions applied.
                 * 
                 * If we are in serial mode, we will issue no more commands (McPendings).
                 */
                commands = new List<McPending> ();
                if (!folder.AsSyncMetaToClientExpected && 
                    McFolder.AsSyncKey_Initial != folder.AsSyncKey && 
                    !inSerialMode) {
                    commands = EligibleForSync (accountId, folder);
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
                    var perFolder = new SyncKit.PerFolder () {
                        Folder = folder,
                        Commands = commands,
                        GetChanges = getChanges,
                    };
                    // Parameters are only valid/expressed when not AsSyncKey_Initial.
                    // They are typically only expressed when doing GetChanges (exception: GFE).
                    if (McFolder.AsSyncKey_Initial != folder.AsSyncKey) {
                        var parms = ParametersProvider (folder, rung, false);
                        perFolder.FilterCode = parms.Item1;
                        perFolder.WindowSize = parms.Item2;
                    }
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
            return null;
        }

        public PingKit GenPingKit (int accountId, McProtocolState protocolState, bool isNarrow, bool stillHaveUnsyncedFolders, bool ignoreToClientExpected)
        {
            var maxHeartbeatInterval = (stillHaveUnsyncedFolders) ? 60 : BEContext.ProtocolState.HeartbeatInterval;
            var folders = FolderListProvider (accountId, Scope.StrategyRung (protocolState), isNarrow);
            if (0 == folders.Count) {
                Log.Error (Log.LOG_AS, "GenPingKit: no folders");
                return null;
            }
            if (!ignoreToClientExpected && folders.Any (x => true == x.AsSyncMetaToClientExpected)) {
                return null;
            }
            if (protocolState.MaxFolders >= folders.Count) {
                return new PingKit () { Folders = folders, MaxHeartbeatInterval = maxHeartbeatInterval };
            }
            // If we have too many folders, then whittle down the list, but keep default inbox & cal.
            List<McFolder> fewer = new List<McFolder> ();
            var defInbox = folders.FirstOrDefault (x => x.Type == ProtoControl.FolderHierarchy.TypeCode.DefaultInbox_2);
            if (null != defInbox) {
                fewer.Add (defInbox);
                folders.Remove (defInbox);
            }
            var defCal = folders.FirstOrDefault (x => x.Type == ProtoControl.FolderHierarchy.TypeCode.DefaultCal_8);
            if (null != defCal) {
                fewer.Add (defCal);
                folders.Remove (defCal);
            }
            // Prefer the least-recently-ping'd.
            var stalest = folders.OrderBy (x => x.AsSyncLastPing).Take ((int)protocolState.MaxFolders - fewer.Count);
            fewer.AddRange (stalest);
            return new PingKit () { Folders = fewer, MaxHeartbeatInterval = maxHeartbeatInterval };
        }

        // Returns null if nothing to do.
        public FetchKit GenFetchKit (int accountId)
        {
            // TODO take into account network status and total data in the fetch request.
            var remaining = KBaseFetchSize;
            var fetchBodies = new List<FetchKit.FetchBody> ();
            var emails = McEmailMessage.QueryNeedsFetch (accountId, remaining, McEmailMessage.minHotScore).ToList ();
            foreach (var email in emails) {
                // TODO: all this can be one SQL JOIN.
                var folders = McFolder.QueryByFolderEntryId<McEmailMessage> (accountId, email.Id);
                if (0 == folders.Count) {
                    // This can happen - we score a message, and then it gets moved to a client-owned folder.
                    continue;
                }
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
                    Pendings = new List<McPending> (),
                };
            }
            Log.Info (Log.LOG_AS, "GenFetchKit: nothing to do.");
            return null;
        }

        public bool ANarrowFolderHasToClientExpected (int accountId)
        {
            var defInbox = McFolder.GetDefaultInboxFolder (accountId);
            var defCal = McFolder.GetDefaultCalendarFolder (accountId);
            var retval = ((null != defInbox && defInbox.AsSyncMetaToClientExpected) ||
                         (null != defCal && defCal.AsSyncMetaToClientExpected));
            Log.Info (Log.LOG_AS, "ANarrowFolderHasToClientExpected is {0}", retval);
            return retval;
        }

        // <DEBUG>
        private void DumpAsState ()
        {
            var protocolState = BEContext.ProtocolState;
            NcAssert.NotNull (protocolState);
            Log.Error (Log.LOG_AS, "DumpAsState: ProtoControlState: {0}", protocolState.ProtoControlState);
            var servers = NcModel.Instance.Db.Table<McServer> ().ToList ();
            Log.Error (Log.LOG_AS, "DumpAsState: {0} McServers in table", servers.Count);
            foreach (var server in servers) {
                Log.Error (Log.LOG_AS, "DumpAsState: server.Id/server.Host: {0}/{1}", server.Id, server.Host);
            }
        }
        // </DEBUG>

        public static bool ScrubSyncedFolders (int accountId)
        {
            var folders = McFolder.QueryByIsClientOwned (accountId, false);
            var now = DateTime.UtcNow;
            var stillHaveUnsyncedFolders = false;
            foreach (var folder in folders) {
                if (!folder.AsSyncMetaToClientExpected &&
                    folder.AsSyncKey != McFolder.AsSyncKey_Initial) {
                    if (!folder.HasSeenServerCommand &&
                        15 > folder.SyncAttemptCount &&
                        folder.LastSyncAttempt < (now - new TimeSpan (0, 0, 10))) {
                        // HotMail's MoreAvailable can't be trusted - doing so can get a folder in a never-synced state.
                        // Here we re-enable sync with high freqency for folders that have never seen an Add - to a limit.
                        folder.UpdateSet_AsSyncMetaToClientExpected (true);
                        stillHaveUnsyncedFolders = true;
                        Log.Warn (Log.LOG_AS, "ScrubSyncedFolders: re-enable of never-synced folder {0}", folder.ServerId);
                    } else if (folder.LastSyncAttempt < (now - new TimeSpan (0, 5, 0))) {
                        // Re-enable any folder that hasn't synced in a long time. This is because the AS spec only
                        // requires Ping to report Adds, and not Changes or Deletes.
                        folder.UpdateSet_AsSyncMetaToClientExpected (true);
                        Log.Info (Log.LOG_AS, "ScrubSyncedFolders: re-enable of folder {0}", folder.ServerId);
                    }
                }
            }
            return stillHaveUnsyncedFolders;
        }

        public bool PowerPermitsSpeculation ()
        {
            return (Power.Instance.PowerState != PowerStateEnum.Unknown && Power.Instance.BatteryLevel > 0.7) ||
            (Power.Instance.PowerStateIsPlugged () && Power.Instance.BatteryLevel > 0.2);
        }

        public Tuple<PickActionEnum, AsCommand> PickUserDemand ()
        {
            var accountId = BEContext.Account.Id;
            var exeCtxt = NcApplication.Instance.ExecutionContext;
            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt) {
                // (FG) If the user has initiated a Search command, we do that.
                var search = McPending.QueryEligible (accountId).
                    Where (x => McPending.Operations.ContactSearch == x.Operation ||
                        McPending.Operations.EmailSearch == x.Operation).FirstOrDefault ();
                if (null != search) {
                    Log.Info (Log.LOG_AS, "Strategy:FG:Search");
                    return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.HotQOp, 
                        new AsSearchCommand (BEContext.ProtoControl, search));
                }
                // (FG) If the user has initiated a ItemOperations Fetch (body or attachment), we do that.
                var fetch = McPending.QueryEligibleOrderByPriorityStamp (accountId).
                    Where (x => 
                        McPending.Operations.AttachmentDownload == x.Operation ||
                        McPending.Operations.EmailBodyDownload == x.Operation ||
                        McPending.Operations.CalBodyDownload == x.Operation ||
                        McPending.Operations.ContactBodyDownload == x.Operation ||
                        McPending.Operations.TaskBodyDownload == x.Operation
                    ).FirstOrDefault ();
                if (null != fetch) {
                    Log.Info (Log.LOG_AS, "Strategy:FG:Fetch");
                    // TODO: aggregate more than one hot fetch into this command, keeping in mind the
                    // total expected size.
                    return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.HotQOp,
                        new AsItemOperationsCommand (BEContext.ProtoControl,
                            new FetchKit () {
                                FetchBodies = new List<FetchKit.FetchBody> (),
                                FetchAttachments = new List<McAttachment> (),
                                Pendings = new List<McPending> { fetch },
                            }));
                }
            }
            return null;
        }

        public Tuple<PickActionEnum, AsCommand> Pick ()
        {
            var accountId = BEContext.Account.Id;
            var protocolState = BEContext.ProtocolState;
            var exeCtxt = NcApplication.Instance.ExecutionContext;
            if (NcApplication.ExecutionContextEnum.Initializing == exeCtxt) {
                // ExecutionContext is not set until after BE is started.
                exeCtxt = NcApplication.Instance.PlatformIndication;
            }
            AdvanceIfPossible (accountId, Scope.StrategyRung (protocolState));
            var stillHaveUnsyncedFolders = ScrubSyncedFolders (accountId);
            var userDemand = PickUserDemand ();
            if (null != userDemand) {
                return userDemand;
            }
            // (FG, BG) If there is a SendMail, SmartForward or SmartReply in the pending queue, send it.
            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt ||
                NcApplication.ExecutionContextEnum.Background == exeCtxt) {
                var send = McPending.QueryEligible (accountId).
                    Where (x => 
                        McPending.Operations.EmailSend == x.Operation ||
                           McPending.Operations.EmailForward == x.Operation ||
                           McPending.Operations.EmailReply == x.Operation ||
                           McPending.Operations.CalRespond == x.Operation ||
                           McPending.Operations.CalForward == x.Operation
                           ).FirstOrDefault ();
                if (null != send) {
                    Log.Info (Log.LOG_AS, "Strategy:FG/BG:Send");
                    AsCommand cmd = null;
                    switch (send.Operation) {
                    case McPending.Operations.EmailSend:
                        cmd = new AsSendMailCommand (BEContext.ProtoControl, send);
                        break;
                    case McPending.Operations.EmailForward:
                    case McPending.Operations.CalForward:
                        cmd = new AsSmartForwardCommand (BEContext.ProtoControl, send);
                        break;
                    case McPending.Operations.EmailReply:
                        cmd = new AsSmartReplyCommand (BEContext.ProtoControl, send);
                        break;
                    case McPending.Operations.CalRespond:
                        cmd = new AsMeetingResponseCommand (BEContext.ProtoControl, send);
                        break;
                    default:
                        NcAssert.CaseError (send.Operation.ToString ());
                        break;
                    }
                    return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.HotQOp, cmd);
                }
            }

            // (QS) If a narrow Sync hasn’t successfully completed in the last N seconds, 
            // perform a narrow Sync Command.
            // TODO we'd like to prioritize Inbox, but still get other folders too if we can squeeze it in.
            // TODO we'd like a quick hotness assesment from the Brain, and then download hot bodies/attachments if
            // we can squeeze it in.
            if (NcApplication.ExecutionContextEnum.QuickSync == exeCtxt) {
                if (protocolState.LastNarrowSync < DateTime.UtcNow.AddSeconds (-60)) {
                    var nSyncKit = GenSyncKit (accountId, protocolState, SyncMode.Narrow);
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
                // (FG, BG) If there are entries in the pending queue, execute the oldest.
                var next = McPending.QueryEligible (accountId).FirstOrDefault ();
                if (null != next) {
                    NcAssert.True (McPending.Operations.Last == McPending.Operations.EmailSearch);
                    Log.Info (Log.LOG_AS, "Strategy:FG/BG:QOp:{0}", next.Operation.ToString ());
                    AsCommand cmd = null;
                    var action = PickActionEnum.QOop;
                    switch (next.Operation) {
                    // It is likely that next is one of these at the top of the switch () ...
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
                    // ... however one of these below, which would have been handled above, could have been
                    // inserted into the Q while Pick() is in the middle of running.
                    case McPending.Operations.EmailForward:
                    case McPending.Operations.CalForward:
                        cmd = new AsSmartForwardCommand (BEContext.ProtoControl, next);
                        break;
                    case McPending.Operations.EmailReply:
                        cmd = new AsSmartReplyCommand (BEContext.ProtoControl, next);
                        break;
                    case McPending.Operations.EmailSend:
                        cmd = new AsSendMailCommand (BEContext.ProtoControl, next);
                        break;
                    case McPending.Operations.ContactSearch:
                    case McPending.Operations.EmailSearch:
                        cmd = new AsSearchCommand (BEContext.ProtoControl, next);
                        break;
                    case McPending.Operations.CalRespond:
                        cmd = new AsMeetingResponseCommand (BEContext.ProtoControl, next);
                        break;
                    case McPending.Operations.EmailBodyDownload:
                    case McPending.Operations.ContactBodyDownload:
                    case McPending.Operations.CalBodyDownload:
                    case McPending.Operations.TaskBodyDownload:
                    case McPending.Operations.AttachmentDownload:
                        cmd = new AsItemOperationsCommand (BEContext.ProtoControl,
                            new FetchKit () {
                                FetchBodies = new List<FetchKit.FetchBody> (),
                                FetchAttachments = new List<McAttachment> (),
                                Pendings = new List<McPending> { next },
                            });
                        break;
                    case McPending.Operations.Sync:
                        var uSyncKit = GenSyncKit (accountId, protocolState, SyncMode.Directed, next);
                        if (null != uSyncKit) {
                            cmd = new AsSyncCommand (BEContext.ProtoControl, uSyncKit);
                            action = PickActionEnum.Sync;
                        } else {
                            Log.Error (Log.LOG_AS, "Strategy:FG/BG:QOp: null SyncKit");
                            cmd = null;
                            action = PickActionEnum.FSync;
                        }
                        break;

                    default:
                        if (AsSyncCommand.IsSyncCommand (next.Operation)) {
                            Log.Info (Log.LOG_AS, "Strategy:FG/BG:QOp-IsSyncCommand:{0}", next.Operation.ToString ());
                            var syncKit = GenSyncKit (accountId, protocolState, SyncMode.Wide);
                            if (null != syncKit) {
                                return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Sync, 
                                    new AsSyncCommand (BEContext.ProtoControl, syncKit));
                            } else {
                                Log.Error (Log.LOG_AS, "Strategy:FG/BG:QOp-IsSyncCommand: null SyncKit.");
                                return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.FSync, null);
                            }
                        } else {
                            NcAssert.CaseError (next.Operation.ToString ());
                        }
                        break;
                    }
                    return Tuple.Create<PickActionEnum, AsCommand> (action, cmd);
                }
                // (FG, BG) Unless one of these conditions are met, perform a narrow Sync Command...
                // The goal here is to ensure a narrow Sync periodically so that new Inbox/default cal aren't crowded out.
                var needNarrowSyncMarker = DateTime.UtcNow.AddSeconds (-300);
                if (Scope.FlagIsSet (Scope.StrategyRung (protocolState), Scope.FlagEnum.NarrowSyncOk) &&
                    protocolState.LastNarrowSync < needNarrowSyncMarker &&
                    (protocolState.LastPing < needNarrowSyncMarker || ANarrowFolderHasToClientExpected (accountId))) {
                    Log.Info (Log.LOG_AS, "Strategy:FG/BG:Narrow Sync...");
                    var nSyncKit = GenSyncKit (accountId, protocolState, SyncMode.Narrow);
                    if (null != nSyncKit) {
                        Log.Info (Log.LOG_AS, "Strategy:FG/BG:...SyncKit");
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Sync, 
                            new AsSyncCommand (BEContext.ProtoControl, nSyncKit));
                    }
                }
                // (FG, BG) If it has been more than 5 min since last FolderSync, do a FolderSync.
                // It seems we can't rely on the server to tell us to do one in all situations.
                if (protocolState.AsLastFolderSync < DateTime.UtcNow.AddMinutes (-5)) {
                    return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.FSync, null);
                }
                // (FG, BG) If we are rate-limited, and we can execute a narrow Ping command at the 
                // current filter setting, execute a narrow Ping command.
                // Do don't obey rate-limiter if HotMail.
                if (NcCommStatus.Instance.IsRateLimited (BEContext.Server.Id) &&
                    BEContext.ProtocolState.HasBeenRateLimited &&
                    !BEContext.Server.HostIsHotMail () &&
                    !BEContext.Server.HostIsGMail ()) {
                    var rlPingKit = GenPingKit (accountId, protocolState, true, stillHaveUnsyncedFolders, false);
                    if (null != rlPingKit) {
                        Log.Info (Log.LOG_AS, "Strategy:FG/BG,RL:Narrow Ping");
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Ping, 
                            new AsPingCommand (BEContext.ProtoControl, rlPingKit));
                    }
                    // (FG, BG) If we are rate-limited, and we can’t execute a narrow Ping command
                    // at the current filter setting, then wait.
                    else {
                        Log.Info (Log.LOG_AS, "Strategy:FG/BG,RL:Wait");
                        return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Wait,
                            new AsWaitCommand (BEContext.ProtoControl, 120, false));
                    }
                }
                // (FG, BG) Choose eligible option by priority, split tie randomly...
                if (Scope.FlagIsSet (Scope.StrategyRung (protocolState), Scope.FlagEnum.IgnorePower) ||
                    PowerPermitsSpeculation () ||
                    NcApplication.ExecutionContextEnum.Foreground == exeCtxt) {
                    FetchKit fetchKit = null;
                    SyncKit syncKit = null;
                    if (NetStatusSpeedEnum.WiFi_0 == NcCommStatus.Instance.Speed && PowerPermitsSpeculation ()) {
                        fetchKit = GenFetchKit (accountId);
                    }
                    syncKit = GenSyncKit (accountId, protocolState, SyncMode.Wide);
                    if (null != fetchKit && (null == syncKit || 0.7 < CoinToss.NextDouble ())) {
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
                // DEBUG. It seems like the server is slow to respond when there is new email. 
                // At least it is slower to tell us than other clients. Sniffing touchdown shows 
                // that they do a narrow Ping and that each add'l folder needs to be manuall added
                // as "synced". So we will do narrow Ping 80% of the time and see if that helps.
                PingKit pingKit = null;
                if (0.8 < CoinToss.NextDouble ()) {
                    Log.Info (Log.LOG_AS, "Strategy:FG/BG:PingKit try generating wide PingKit.");
                    pingKit = GenPingKit (accountId, protocolState, false, stillHaveUnsyncedFolders, false);
                }
                if (null == pingKit) {
                    Log.Info (Log.LOG_AS, "Strategy:FG/BG:PingKit will be narrow.");
                    pingKit = GenPingKit (accountId, protocolState, true, stillHaveUnsyncedFolders, false);
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
                Log.Info (Log.LOG_AS, "Strategy:QS:Wait");
                return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Wait,
                    new AsWaitCommand (BEContext.ProtoControl, 120, true));
            }
            NcAssert.True (false);
            return null;
        }

        // Set 1st-try timeout value assuming we are running 25% of average speed, with a base of 30s.
        // http://www.phonearena.com/news/Which-carrier-offers-the-fastest-mobile-data-and-coverage-4G--3G-speed-comparison_id53828
        // http://blog.rottenwifi.com/average-public-wifi-client-satisfaction-rank-410/
        // WiFi_0, CellFast_1, CellSlow_2.
        public readonly double[] KUploadBiSec = { 2.7e6 / 8, 3.0e6 / 8, 1.3e6 / 8 };
        public readonly double[] KDownloadBiSec = { 3.3e6 / 8, 6.0e6 / 8, 2.0e6 / 8 };
        public const double KRateDiscount = 0.75;
        public const int KMinTimeout = 30;

        public int UploadTimeoutSecs (long length)
        {
            length = (0 >= length) ? 100 * 1000 : length;
            return KMinTimeout + (int)(length / (KUploadBiSec [(int)NcCommStatus.Instance.Speed] * (1 - KRateDiscount)));
        }

        public int DownloadTimeoutSecs (long length)
        {
            length = (0 >= length) ? 1000 * 1000 : length;
            return KMinTimeout + (int)(length / (KDownloadBiSec [(int)NcCommStatus.Instance.Speed] * (1 - KRateDiscount)));
        }

        public int DefaultTimeoutSecs { get { return KMinTimeout; } }
    }
}
