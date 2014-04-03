//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class AsSyncStrategy
    {
        public enum ECLst : uint
        {
            DefI1dC2w = (St.Last + 1),
            DefI3dC2w,
            DefI1wC2w,
            DefI2wC2w,
            All1m,
            EInfC3m,
            EInfC6m,
            AllInf,
        };

        private const uint ECLstLast = (uint)ECLst.AllInf;

        public enum CLst : uint
        {
            None = (St.Last + 1),
            DefOnly,
            All,
        };

        private const uint CLstLast = (uint)CLst.All;
        private NcStateMachine EmailCalendarSm;
        private NcStateMachine ContactsSm;
        private IBEContext BEContext;

        private delegate List<McFolder> FolderList ();

        private delegate Tuple<Xml.Provision.MaxAgeFilterCode, uint> Parameters (McFolder folder);
        // Success event happens when there is a sync indicating that there is no more available.
        public AsSyncStrategy (IBEContext beContext)
        {
            BEContext = beContext;
            EmailCalendarSm = new NcStateMachine () { 
                Name = string.Format ("ASSyncStratEC({0})", BEContext.Account.Id),
                LocalStateType = typeof(ECLst),
                StateChangeIndication = UpdateSavedECState,
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)ECLst.DefI1dC2w },
                        }
                    },
                    new Node {
                        State = (uint)ECLst.DefI1dC2w,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)ECLst.DefI3dC2w },
                        }
                    },
                    new Node {
                        State = (uint)ECLst.DefI3dC2w,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)ECLst.DefI1wC2w },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoNop, State = (uint)ECLst.DefI1dC2w },
                        }
                    },
                    new Node {
                        State = (uint)ECLst.DefI1wC2w,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)ECLst.DefI2wC2w },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoNop, State = (uint)ECLst.DefI1dC2w },
                        }
                    },
                    new Node {
                        State = (uint)ECLst.DefI2wC2w,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoAvCon, State = (uint)ECLst.All1m },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoNop, State = (uint)ECLst.DefI1dC2w },
                        }
                    },
                    new Node {
                        State = (uint)ECLst.All1m,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoAvCon, State = (uint)ECLst.EInfC3m },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoSpCon, State = (uint)ECLst.DefI1dC2w },
                        }
                    },
                    new Node {
                        State = (uint)ECLst.EInfC3m,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoAvCon, State = (uint)ECLst.EInfC6m },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoSpCon, State = (uint)ECLst.DefI1dC2w },
                        }
                    },
                    new Node {
                        State = (uint)ECLst.EInfC6m,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoAvCon, State = (uint)ECLst.AllInf },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoSpCon, State = (uint)ECLst.DefI1dC2w },
                        }
                    },
                    new Node {
                        State = (uint)ECLst.AllInf,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)SmEvt.E.Success,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoAvCon, State = (uint)ECLst.DefI1dC2w },
                        }
                    },
                }
            };
            EmailCalendarSm.Validate ();

            ContactsSm = new NcStateMachine () {
                Name = string.Format ("ASSyncStratC({0})", BEContext.Account.Id),
                LocalStateType = typeof(CLst),
                StateChangeIndication = UpdateSavedCState,
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)CLst.None },
                        }
                    },
                    new Node {
                        State = (uint)CLst.None,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)CLst.DefOnly },
                        }
                    },
                    new Node {
                        State = (uint)CLst.DefOnly,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)CLst.All },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoNop, State = (uint)CLst.None },
                        }
                    },
                    new Node {
                        State = (uint)CLst.All,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)SmEvt.E.Success,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoNop, State = (uint)CLst.None },
                        }
                    },
                }
            };
            ContactsSm.Validate ();
            RestoreSavedState ();
            ContactsSm.PostEvent ((uint)SmEvt.E.Launch, "SYNCSTRATGOS");
            EmailCalendarSm.PostEvent ((uint)SmEvt.E.Launch, "SYNCSTRATGO");
        }
        // Almost actions for both SMs are DoNop.
        private void DoNop ()
        {
            // Do nothing.
        }

        private void DoAvCon ()
        {
            ContactsSm.PostEvent ((uint)SmEvt.E.Success, "DOSTCON");
        }

        private void DoSpCon ()
        {
            ContactsSm.PostEvent ((uint)SmEvt.E.HardFail, "DOSPCON");
        }

        private List<McFolder> ECFolderListProvider ()
        {
            switch ((ECLst)EmailCalendarSm.State) {
            case ECLst.DefI1dC2w:
            case ECLst.DefI3dC2w:
            case ECLst.DefI1wC2w:
            case ECLst.DefI2wC2w:
                return DefaultInboxAndDefaultCalendarFolders ();

            case ECLst.All1m:
            case ECLst.EInfC3m:
            case ECLst.EInfC6m:
            case ECLst.AllInf:
                return AllSyncedEmailAndCalendarFolders ();

            default:
                throw new Exception ();
            }
        }

        private List<McFolder> CFolderListProvider ()
        {
            switch ((CLst)ContactsSm.State) {
            case CLst.None:
                return new List<McFolder> ();

            case CLst.DefOnly:
                return new List<McFolder> () { 
                    McFolder.GetDefaultContactFolder (BEContext.Account.Id)
                };

            case CLst.All:
                return AllSyncedContactsFolders ();

            default:
                throw new Exception ();
            }
        }

        private List<McFolder> FolderListProvider ()
        {
            List<McFolder> ecFolders = ECFolderListProvider ();
            List<McFolder> cFolders = CFolderListProvider ();
            ecFolders.AddRange (cFolders);
            return ecFolders;
        }

        private Tuple<Xml.Provision.MaxAgeFilterCode, uint> ParametersProvider (McFolder folder)
        {
            uint windowSize = 25;
            switch (Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (folder.Type)) {
            case McFolder.ClassCodeEnum.Email:
                switch ((ECLst)EmailCalendarSm.State) {
                case ECLst.DefI1dC2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneDay_1, windowSize);

                case ECLst.DefI3dC2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.ThreeDays_2, windowSize);

                case ECLst.DefI1wC2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneWeek_3, windowSize);

                case ECLst.DefI2wC2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.TwoWeeks_4, windowSize);

                case ECLst.All1m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneMonth_5, windowSize);

                case ECLst.EInfC3m:
                case ECLst.EInfC6m:
                case ECLst.AllInf:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, windowSize);

                default:
                    throw new Exception ();
                }

            case McFolder.ClassCodeEnum.Calendar:
                switch ((ECLst)EmailCalendarSm.State) {
                case ECLst.DefI1dC2w:
                case ECLst.DefI3dC2w:
                case ECLst.DefI1wC2w:
                case ECLst.DefI2wC2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.TwoWeeks_4, windowSize);

                case ECLst.All1m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneMonth_5, windowSize);

                case ECLst.EInfC3m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.ThreeMonths_6, windowSize);

                case ECLst.EInfC6m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SixMonths_7, windowSize);

                case ECLst.AllInf:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, windowSize);

                default:
                    throw new Exception ();
                }

            case McFolder.ClassCodeEnum.Contact:
                switch ((CLst)ContactsSm.State) {
                case CLst.None:
                case CLst.DefOnly:
                case CLst.All:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, windowSize);

                default:
                    throw new Exception ();
                }

            default:
                throw new Exception ();
            }
        }
        // SM's state persistance callback.
        private void UpdateSavedECState ()
        {
            var protocolState = BEContext.ProtocolState;
            protocolState.SyncStratEmailCalendarState = EmailCalendarSm.State;
            protocolState.Update ();
            // Filter value changed, so go tickle all the changes-expected flags.
            foreach (var folder in ECFolderListProvider ()) {
                if (null != folder) {
                    // We may see null if the server hasn't yet created these folders.
                    // Note that because we don't (yet) break out Cal into its own SM, it will get needlessly tickled here.
                    folder.AsSyncMetaToClientExpected = true;
                    folder.Update ();
                }
            }
        }
        private void UpdateSavedCState ()
        {
            var protocolState = BEContext.ProtocolState;
            protocolState.SyncStratContactsState = ContactsSm.State;
            protocolState.Update ();
            foreach (var folder in CFolderListProvider ()) {
                if (null != folder) {
                    folder.AsSyncMetaToClientExpected = true;
                    folder.Update ();
                }
            }
        }
        // SM's state restore API.
        private void RestoreSavedState ()
        {
            var protocolState = BEContext.ProtocolState;
            EmailCalendarSm.State = protocolState.SyncStratEmailCalendarState;
            ContactsSm.State = protocolState.SyncStratContactsState;
        }

        private List<McFolder> DefaultInboxAndDefaultCalendarFolders ()
        {
            return new List<McFolder> { 
                McFolder.GetDefaultInboxFolder (BEContext.Account.Id),
                McFolder.GetDefaultCalendarFolder (BEContext.Account.Id)
            };
        }

        private List<McFolder> AllSyncedFolders ()
        {
            return McFolder.QueryClientOwned (BEContext.Account.Id, false);
        }

        private List<McFolder> AllSyncedEmailAndCalendarFolders ()
        {
            return AllSyncedFolders ().Where (f => 
                Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) == McFolderEntry.ClassCodeEnum.Email ||
            Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) == McFolderEntry.ClassCodeEnum.Calendar).ToList ();
        }

        private List<McFolder> AllSyncedContactsFolders ()
        {
            return AllSyncedFolders ().Where (f => 
                Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) == McFolderEntry.ClassCodeEnum.Contact).ToList ();
        }

        public void ReportSyncResult (List<McFolder> folders)
        {
            var stillExpected = folders.Where (f => true == f.AsSyncMetaToClientExpected).ToList ();
            if (0 == stillExpected.Count ()) {
                // There were no MoreAvailables.
                EmailCalendarSm.PostEvent ((uint)SmEvt.E.Success, "SYNCSTRAT0");
            }
        }
        // External API.
        public Tuple<uint, List<Tuple<McFolder, List<McPending>>>> SyncKit ()
        {
            List<McFolder> eligibleForGetChanges = FolderListProvider ();
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
                // See if we can and should do GetChanges. O(N**2), small N. FIXME.
                if (null != eligibleForGetChanges.FirstOrDefault (x => x.Id == folder.Id)) {
                    if (folder.AsSyncMetaToClientExpected) {
                        folder.AsSyncMetaDoGetChanges = (McFolder.AsSyncKey_Initial != folder.AsSyncKey);
                        var parms = ParametersProvider (folder);
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
            return Tuple.Create ((uint)25, retList);
        }

        public bool IsMoreSyncNeeded ()
        {
            // if we're not in the ultimate state(s), then true.
            if (ECLstLast != EmailCalendarSm.State || CLstLast != ContactsSm.State) {
                return true;
            }
            // if a within-scope folder has to-client stuff waiting on the server, then true.
            foreach (var folder in FolderListProvider ()) {
                if (folder.AsSyncMetaToClientExpected) {
                    return true;
                }
            }
            // if there is a sync-based operation pending, then true.
            var waiting = McPending.QueryEligible (BEContext.Account.Id)
                .Where (p => AsSyncCommand.IsSyncCommand (p.Operation)).ToList ();
            if (0 != waiting.Count ()) {
                return true;
            }
            return false;
        }

        public List<McFolder> PingKit ()
        {
            return FolderListProvider ();
        }
    }
}

