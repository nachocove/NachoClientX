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

        /*
         * N (3) tracks: email, cal, contact. enum per track.
         * 2D array [rung][track].
         * can advance: if 1 or more done and all not done are the same at next rung OR all are done.
         */
        private enum Tracks
        {
            Email = 0,
            Cal = 1,
            Contact = 2,
        };

        private enum EmailLevels
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

        private enum CalLevels
        {
            None,
            Def2w,
            All1m,
            All3m,
            All6m,
            AllInf,
        };

        private enum ContactLevels
        {
            None,
            RicInf,
            DefRicInf,
            AllInf,
        };

        // FIXME - save state in DB.
        private int CurrentRung = 0;
        private int[][] Ladder = new int[][] {
            { EmailLevels.None, CalLevels.None, ContactLevels.RicInf },
            { EmailLevels.Def1d, CalLevels.Def2w, ContactLevels.RicInf },
            { EmailLevels.Def3d, CalLevels.Def2w, ContactLevels.RicInf },
            { EmailLevels.Def1w, CalLevels.Def2w, ContactLevels.RicInf },
            { EmailLevels.Def2w, CalLevels.Def2w, ContactLevels.DefRicInf },
            { EmailLevels.All1m, CalLevels.All1m, ContactLevels.DefRicInf },
            { EmailLevels.All3m, CalLevels.All3m, ContactLevels.AllInf },
            { EmailLevels.All6m, CalLevels.All6m, ContactLevels.AllInf },
            { EmailLevels.AllInf, CalLevels.AllInf, ContactLevels.AllInf },
        };

        private IBEContext BEContext;

        private delegate List<McFolder> FolderList ();

        private delegate Tuple<Xml.Provision.MaxAgeFilterCode, uint> Parameters (McFolder folder);

        public AsStrategy (IBEContext beContext)
        {
            BEContext = beContext;
        }

        private List<Tracks> RequiredToAdvance ()
        {
            var retval = new List<Tracks> ();
            var limit = Ladder.GetLength (0);
            if (limit == CurrentRung) {
                return retval;
            }
            foreach (int track in Tracks) {
                if (Ladder [CurrentRung] [track] != Ladder [CurrentRung + 1] [track]) {
                    retval.Add (track);
                }
            }
            return retval;
        }

        private void DoSayRicDone ()
        {
            // Once we get to "ping" on 1st series of Syncs, then RIC must be downloaded.
            BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_RicInitialSyncCompleted));
        }

        private List<McFolder> EmailFolderListProvider (EmailLevels scope, bool isNarrow)
        {
            switch (scope) {
            case EmailLevels.None:
                if (isNarrow) {
                    return new List<McFolder> () { McFolder.GetDefaultInboxFolder (BEContext.Account.Id) };
                }
                return new List<McFolder> ();

            case EmailLevels.Def1d:
            case EmailLevels.Def1w:
            case EmailLevels.Def2w:
            case EmailLevels.Def3d:
                return new List<McFolder> () { McFolder.GetDefaultInboxFolder (BEContext.Account.Id) };

            case EmailLevels.All1m:
            case EmailLevels.All3m:
            case EmailLevels.All6m:
            case EmailLevels.AllInf:
                if (isNarrow) {
                    return new List<McFolder> () { McFolder.GetDefaultInboxFolder (BEContext.Account.Id) };
                }
                return McFolder.ServerEndQueryAll (BEContext.Account.Id) ().Where (f => 
                    Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) ==
                McAbstrFolderEntry.ClassCodeEnum.Email);

            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        private List<McFolder> CalFolderListProvider (CalLevels scope, bool isNarrow)
        {
            switch (scope) {
            case CalLevels.None:
                if (isNarrow) {
                    return new List<McFolder> () { McFolder.GetDefaultCalendarFolder (BEContext.Account.Id) };
                }
                return new List<McFolder> ();

            case CalLevels.Def2w:
                return new List<McFolder> () { McFolder.GetDefaultCalendarFolder (BEContext.Account.Id) };

            case CalLevels.All1m:
            case CalLevels.All3m:
            case CalLevels.All6m:
            case CalLevels.AllInf:
                if (isNarrow) {
                    return new List<McFolder> () { McFolder.GetDefaultCalendarFolder (BEContext.Account.Id) };
                }
                return McFolder.ServerEndQueryAll (BEContext.Account.Id) ().Where (f => 
                    Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) ==
                McAbstrFolderEntry.ClassCodeEnum.Calendar);

            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        private List<McFolder> ContactFolderListProvider (ContactLevels scope, bool isNarrow)
        {
            if (isNarrow) {
                return new List<McFolder> ();
            }
            switch (scope) {
            case ContactLevels.None:
                return new List<McFolder> ();

            case ContactLevels.RicInf:
                return new List<McFolder> () { McFolder.GetRicContactFolder (BEContext.Account.Id) };

            case ContactLevels.DefRicInf:
                return new List<McFolder> () { McFolder.GetRicContactFolder (BEContext.Account.Id),
                    McFolder.GetDefaultContactFolder (BEContext.Account.Id)
                };

            case ContactLevels.AllInf:
                return McFolder.ServerEndQueryAll (BEContext.Account.Id) ().Where (f => 
                    Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) ==
                McAbstrFolderEntry.ClassCodeEnum.Contact);

            default:
                NcAssert.CaseError (string.Format ("{0}", scope));
                return null;
            }
        }

        // function returning all folders at current level eligible for Sync.
        private List<McFolder> FolderListProvider (bool isNarrow)
        {
            var result = new List<McFolder> ();
            foreach (int track in Tracks) {
                var scope = Ladder [CurrentRung] [track];
                switch (track) {
                case Tracks.Email:
                    result.AddRange (EmailFolderListProvider (scope, isNarrow));
                    break;
                case Tracks.Cal:
                    result.AddRange (CalFolderListProvider (scope, isNarrow));
                    break;
                case Tracks.Contact:
                    result.AddRange (ContactFolderListProvider (scope, isNarrow));
                    break;
                default:
                    NcAssert.CaseError (string.Format ("{0}", track));
                }
            }
        }

        private Tuple<Xml.Provision.MaxAgeFilterCode, uint> ParametersProvider (McFolder folder, bool isNarrow)
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
                switch (Ladder[CurrentRung][Tracks.Email]) {
                case EmailLevels.Def1d:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneDay_1, perFolderWindowSize);
                case EmailLevels.Def3d:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.ThreeDays_2, perFolderWindowSize);
                case EmailLevels.Def1w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneWeek_3, perFolderWindowSize);
                case EmailLevels.Def2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.TwoWeeks_4, perFolderWindowSize);
                case EmailLevels.All1m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.OneMonth_5, perFolderWindowSize);
                case EmailLevels.All3m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.ThreeMonths_6, perFolderWindowSize);
                case EmailLevels.All6m:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SixMonths_7, perFolderWindowSize);
                case EmailLevels.AllInf:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.SyncAll_0, perFolderWindowSize);
                default:
                    NcAssert.CaseError (string.Format ("{0}", Ladder [CurrentRung] [Tracks.Email]));
                    return null;
                }

            case McFolder.ClassCodeEnum.Calendar:
                switch (Ladder[CurrentRung][Tracks.Cal]) {
                case CalLevels.Def2w:
                    return Tuple.Create (Xml.Provision.MaxAgeFilterCode.TwoWeeks_4, perFolderWindowSize);
                case CalLevels.All1m:
                case CalLevels.All3m:
                case CalLevels.All6m:
                case CalLevels.AllInf:
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

        private List<McFolder> AllSyncedFolders ()
        {
            // A folder must be created on the server before it can be the subject of a Sync/Ping.
            return McFolder.ServerEndQueryAll (BEContext.Account.Id);
        }

        private List<McFolder> AllSyncedEmailAndCalendarFolders ()
        {
            return AllSyncedFolders ().Where (f => 
                Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) == McAbstrFolderEntry.ClassCodeEnum.Email ||
            Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) == McAbstrFolderEntry.ClassCodeEnum.Calendar).ToList ();
        }

        private List<McFolder> AllSyncedContactsTasksFolders ()
        {
            return AllSyncedFolders ().Where (f => 
                Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) == McAbstrFolderEntry.ClassCodeEnum.Contact ||
            Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (f.Type) == McAbstrFolderEntry.ClassCodeEnum.Tasks).ToList ();
        }

        // FIXME SyncKit will need to pull pending and also give options/filters.
        public Tuple<uint, List<Tuple<McFolder, List<McPending>>>> SyncKit (bool cantBeEmpty)
        {
            uint overallWindowSize = KBaseOverallWindowSize;
            switch (NcCommStatus.Instance.Speed) {
            case NetStatusSpeedEnum.CellFast:
                overallWindowSize *= 2;
                break;
            case NetStatusSpeedEnum.WiFi:
                overallWindowSize *= 3;
                break;
            }
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
            return (0 == retList.Count) ? null : Tuple.Create (overallWindowSize, retList);
        }

        private IEnumerable<McFolder> PingKit ()
        {
            var folders = FolderListProvider ();
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
            // TODO we may want to add a UI is waiting flag, and just fetch ONE so that the response will be faster.
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

            // Address background fetching if no immediate user need. TODO: we need to measure performance before we let BG fetching degrade latency.
            if (0 == pendings.Count) {
                if (0 < remaining) {
                    var folders = FolderListProvider ();
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

        public Tuple<PickActionEnum, AsCommand> Pick ()
        {
            // FIXME - make sure performFetch does nothing if we've not folder-sync'd yet.
            // If there is something waiting on the pending Q, do that.
            var next = McPending.QueryEligible (BEContext.Account.Id).FirstOrDefault ();
            if (null != next) {
                AsCommand cmd = null;
                switch (next.Operation) {
                case McPending.Operations.ContactSearch:
                    cmd = new AsSearchCommand (BEContext.ProtoControl, next);
                    break;
                case McPending.Operations.FolderCreate:
                    cmd = new AsFolderCreateCommand (BEContext.ProtoControl, next);
                    break;
                case McPending.Operations.FolderUpdate:
                    cmd = new AsFolderUpdateCommand (BEContext.ProtoControl, next);
                    break;
                case McPending.Operations.FolderDelete:
                    cmd = new AsFolderDeleteCommand (BEContext.ProtoControl, next);
                    break;
                case McPending.Operations.EmailSend:
                    cmd = new AsSendMailCommand (BEContext.ProtoControl, next);
                    break;
                case McPending.Operations.EmailForward:
                    cmd = new AsSmartForwardCommand (BEContext.ProtoControl, next);
                    break;
                case McPending.Operations.EmailReply:
                    cmd = new AsSmartReplyCommand (BEContext.ProtoControl, next);
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
                case McPending.Operations.AttachmentDownload:
                case McPending.Operations.EmailBodyDownload:
                case McPending.Operations.CalBodyDownload:
                case McPending.Operations.ContactBodyDownload:
                case McPending.Operations.TaskBodyDownload:
                    // TODO get a legit data type for fetch kit.
                    cmd = new AsItemOperationsCommand (BEContext.ProtoControl, 
                        Tuple.Create<IEnumerable<McPending>, IEnumerable<Tuple<McAbstrItem, string>>> (
                            new List<McPending> { next }, new List<Tuple<McAbstrItem, string>> ()));
                    break;
                case McPending.Operations.CalRespond:
                    cmd = new AsMeetingResponseCommand (BEContext.ProtoControl, next);
                    break;
                }
                return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.QOop, cmd);
            }
            // FIXME Either here, or in SyncKit(), check RequiredToAdvance against MoreAvailable for folders (non-narrow).
            var syncKit = SyncKit (false);
            if (null != syncKit) {
                return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Sync, new AsSyncCommand (BEContext.ProtoControl, syncKit));
            }
            var fetchKit = FetchKit ();
            if (null != fetchKit) {
                return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Fetch, new AsItemOperationsCommand (BEContext.ProtoControl, fetchKit));
            }
            return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Ping, new AsPingCommand (BEContext.ProtoControl, PingKit ()));
        }
    }
}
