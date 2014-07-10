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
        public const uint KBaseOverallWindowSize = 10;
        public const uint KBasePerFolderWindowSize = 7;

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

        public enum CTLst : uint
        {
            RicOnly = (St.Last + 1),
            DefNRic,
            All,
        };

        // Anyone can set to request that the next Sync command be a "quick fetch".
        public bool RequestQuickFetch { set; get; }
        private bool IsQuickFetch;
        private const uint CTLstLast = (uint)CTLst.All;
        private NcStateMachine EmailCalendarSm;
        private NcStateMachine ContactsTasksSm;
        private IBEContext BEContext;

        private delegate List<McFolder> FolderList ();

        private delegate Tuple<Xml.Provision.MaxAgeFilterCode, uint> Parameters (McFolder folder);
        // Success event happens when there is a sync indicating that there is no more available.
        public AsStrategy (IBEContext beContext)
        {
            BEContext = beContext;
            EmailCalendarSm = new NcStateMachine ("ASSTRATEC") { 
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
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoAvCon, State = (uint)ECLst.AllInf },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoSpCon, State = (uint)ECLst.DefI1dC2w },
                        }
                    },
                }
            };
            EmailCalendarSm.Validate ();

            ContactsTasksSm = new NcStateMachine ("ASSTRATCT") {
                Name = string.Format ("ASSyncStratC({0})", BEContext.Account.Id),
                LocalStateType = typeof(CTLst),
                StateChangeIndication = UpdateSavedCTState,
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)CTLst.RicOnly },
                        }
                    },
                    new Node {
                        State = (uint)CTLst.RicOnly,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)CTLst.DefNRic },
                        }
                    },
                    new Node {
                        State = (uint)CTLst.DefNRic,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)CTLst.All },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoNop, State = (uint)CTLst.RicOnly },
                        }
                    },
                    new Node {
                        State = (uint)CTLst.All,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                        },
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)SmEvt.E.Success,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoNop, State = (uint)CTLst.RicOnly },
                        }
                    },
                }
            };
            ContactsTasksSm.Validate ();
            RestoreSavedState ();
            ContactsTasksSm.PostEvent ((uint)SmEvt.E.Launch, "SYNCSTRATGOS");
            EmailCalendarSm.PostEvent ((uint)SmEvt.E.Launch, "SYNCSTRATGO");
        }
        // Almost actions for both SMs are DoNop.
        private void DoNop ()
        {
            // Do nothing.
        }

        private void DoAvCon ()
        {
            ContactsTasksSm.PostEvent ((uint)SmEvt.E.Success, "DOSTCON");
        }

        private void DoSpCon ()
        {
            ContactsTasksSm.PostEvent ((uint)SmEvt.E.HardFail, "DOSPCON");
        }

        private List<McFolder> ECFolderListProvider (bool quickFetch)
        {
            if (quickFetch) {
                return DefaultInboxAndDefaultCalendarFolders ();
            }
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

        private List<McFolder> CTFolderListProvider (bool quickFetch)
        {
            var ric = McFolder.GetRicContactFolder (BEContext.Account.Id);
            var retval = new List<McFolder> ();
            if (null != ric) {
                retval.Add (ric);
            }
            if (quickFetch) {
                // Only the RIC in a quickfetch.
                return retval;
            }
            switch ((CTLst)ContactsTasksSm.State) {
            case CTLst.RicOnly:
                return retval;

            case CTLst.DefNRic:
                retval.Add (McFolder.GetDefaultContactFolder (BEContext.Account.Id));
                retval.Add (McFolder.GetDefaultTaskFolder (BEContext.Account.Id));
                return retval; 

            case CTLst.All:
                return AllSyncedContactsTasksFolders ();

            default:
                throw new Exception ();
            }
        }

        private List<McFolder> FolderListProvider (bool quickFetch)
        {
            List<McFolder> ecFolders = ECFolderListProvider (quickFetch);
            List<McFolder> cFolders = CTFolderListProvider (quickFetch);
            ecFolders.AddRange (cFolders);
            return ecFolders;
        }

        private Tuple<Xml.Provision.MaxAgeFilterCode, uint> ParametersProvider (McFolder folder)
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
                switch ((ECLst)EmailCalendarSm.State) {
                case ECLst.DefI1dC2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneDay_1, perFolderWindowSize);

                case ECLst.DefI3dC2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.ThreeDays_2, perFolderWindowSize);

                case ECLst.DefI1wC2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneWeek_3, perFolderWindowSize);

                case ECLst.DefI2wC2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.TwoWeeks_4, perFolderWindowSize);

                case ECLst.All1m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneMonth_5, perFolderWindowSize);

                case ECLst.EInfC3m:
                case ECLst.EInfC6m:
                case ECLst.AllInf:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, perFolderWindowSize);

                default:
                    throw new Exception ();
                }

            case McFolder.ClassCodeEnum.Calendar:
                switch ((ECLst)EmailCalendarSm.State) {
                case ECLst.DefI1dC2w:
                case ECLst.DefI3dC2w:
                case ECLst.DefI1wC2w:
                case ECLst.DefI2wC2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.TwoWeeks_4, perFolderWindowSize);

                case ECLst.All1m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneMonth_5, perFolderWindowSize);

                case ECLst.EInfC3m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.ThreeMonths_6, perFolderWindowSize);

                case ECLst.EInfC6m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SixMonths_7, perFolderWindowSize);

                case ECLst.AllInf:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, perFolderWindowSize);

                default:
                    throw new Exception ();
                }

            case McFolder.ClassCodeEnum.Contact:
                switch ((CTLst)ContactsTasksSm.State) {
                case CTLst.RicOnly:
                case CTLst.DefNRic:
                case CTLst.All:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, perFolderWindowSize);

                default:
                    throw new Exception ();
                }

            case McFolder.ClassCodeEnum.Tasks:
                return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, perFolderWindowSize);

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
            foreach (var folder in ECFolderListProvider (false)) {
                if (null != folder) {
                    // We may see null if the server hasn't yet created these folders.
                    // Note that because we don't (yet) break out Cal into its own SM, it will get needlessly tickled here.
                    folder.AsSyncMetaToClientExpected = true;
                    folder.Update ();
                }
            }
        }
        private void UpdateSavedCTState ()
        {
            var protocolState = BEContext.ProtocolState;
            protocolState.SyncStratContactsState = ContactsTasksSm.State;
            protocolState.Update ();
            foreach (var folder in CTFolderListProvider (false)) {
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
            ContactsTasksSm.State = protocolState.SyncStratContactsState;
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
            // A folder must be created on the server before it can be the subject of a Sync/Ping.
            return McFolder.ServerEndQueryAll (BEContext.Account.Id);
        }

        private List<McFolder> AllSyncedEmailAndCalendarFolders ()
        {
            return AllSyncedFolders ().Where (f => 
                Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) == McFolderEntry.ClassCodeEnum.Email ||
            Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) == McFolderEntry.ClassCodeEnum.Calendar).ToList ();
        }

        private List<McFolder> AllSyncedContactsTasksFolders ()
        {
            return AllSyncedFolders ().Where (f => 
                Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) == McFolderEntry.ClassCodeEnum.Contact ||
                Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) == McFolderEntry.ClassCodeEnum.Tasks).ToList ();
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
            IsQuickFetch = RequestQuickFetch;
            // Quick fetch request is good only for *next* Sync command.
            RequestQuickFetch = false;
            uint overallWindowSize = KBaseOverallWindowSize;
            switch (NcCommStatus.Instance.Speed) {
            case NetStatusSpeedEnum.CellFast:
                overallWindowSize *= 2;
                break;
            case NetStatusSpeedEnum.WiFi:
                overallWindowSize *= 3;
                break;
            }
            List<McFolder> eligibleForGetChanges = FolderListProvider (IsQuickFetch);
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
            return Tuple.Create (overallWindowSize, retList);
        }

        public bool IsMoreSyncNeeded ()
        {
            // Are there any AsSyncMetaToClientExpected folders available?
            bool areExpecting = FolderListProvider (false).Any (f => f.AsSyncMetaToClientExpected);

            // if we're not in the ultimate state(s), then true.
            if (ECLstLast != EmailCalendarSm.State || CTLstLast != ContactsTasksSm.State) {
                if (!areExpecting && !IsQuickFetch) {
                    EmailCalendarSm.PostEvent ((uint)SmEvt.E.Success, "SYNCSTRATIMSN");
                }
                Log.Info (Log.LOG_SYNC, "IsMoreSyncNeeded: EmailCalendarSm.State/ContactsTasksSm.State");
                return true;
            }
            // if a within-scope folder has to-client stuff waiting on the server, then true.
            // We must not go straight to Ping after a quick fetch, or EAS will know the wrong window size.
            if (areExpecting || IsQuickFetch) {
                Log.Info (Log.LOG_SYNC, "IsMoreSyncNeeded: areExpecting == {0}, IsQuickFetch == {1}", areExpecting, IsQuickFetch);
                return true;
            }
            // if there is a sync-based operation pending, then true.
            var waiting = McPending.QueryEligible (BEContext.Account.Id)
                .Where (p => AsSyncCommand.IsSyncCommand (p.Operation)).ToList ();
            if (0 != waiting.Count ()) {
                Log.Info (Log.LOG_SYNC, "IsMoreSyncNeeded: QueryEligible/IsSyncCommand == {0}", waiting.Count ());
                return true;
            }
            return false;
        }

        public IEnumerable<McFolder> PingKit ()
        {
            var folders = FolderListProvider (false);
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
    }
}

