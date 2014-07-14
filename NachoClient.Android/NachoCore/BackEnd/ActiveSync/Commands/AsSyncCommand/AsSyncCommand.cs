using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;
using System.IO;

namespace NachoCore.ActiveSync
{
    public partial class AsSyncCommand : AsCommand
    {
        private List<McFolder> FoldersInRequest;
        private bool HadEmailMessageSetChanges;
        private bool HadContactSetChanges;
        private bool HadCalendarSetChanges;
        private bool HadTaskSetChanges;
        private bool HadEmailMessageChanges;
        private bool HadContactChanges;
        private bool HadCalendarChanges;
        private bool HadTaskChanges;
        private bool HadNewUnreadEmailMessageInInbox;
        private bool FolderSyncIsMandated;
        private Nullable<uint> Limit;
        private List<Tuple<McFolder, List<McPending>>> SyncKitList;
        private XNamespace EmailNs;
        private XNamespace TasksNs;
        private uint WindowSize;

        public static XNamespace Ns = Xml.AirSync.Ns;

        private void ApplyStrategy ()
        {
            var syncKit = BEContext.ProtoControl.SyncStrategy.SyncKit ();
            WindowSize = syncKit.Item1;
            SyncKitList = syncKit.Item2;
            FoldersInRequest = new List<McFolder> ();
            foreach (var tup in SyncKitList) {
                FoldersInRequest.Add (tup.Item1);
                PendingList.AddRange (tup.Item2);
            }
        }

        public AsSyncCommand (IBEContext beContext) : base (Xml.AirSync.Sync, Xml.AirSync.Ns, beContext)
        {
            Timeout = new TimeSpan (0, 0, 20);
            EmailNs = Xml.Email.Ns;
            TasksNs = Xml.Tasks.Ns;
            SuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_SyncSucceeded);
            FailureInd = NcResult.Error (NcResult.SubKindEnum.Error_SyncFailed);
            ApplyStrategy ();
            foreach (var pending in PendingList) {
                pending.MarkDispached ();
            }
        }

        private XElement ToEmailDelete (McPending pending)
        {
            // FIXME - add Class?
            return new XElement (m_ns + Xml.AirSync.Delete,
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId));
        }

        private XElement ToEmailMarkRead (McPending pending)
        {
            return new XElement (m_ns + Xml.AirSync.Change,
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                new XElement (m_ns + Xml.AirSync.ApplicationData,
                    new XElement (EmailNs + Xml.Email.Read, "1")));
        }

        private XElement ToEmailSetFlag (McPending pending)
        {
            return new XElement (m_ns + Xml.AirSync.Change,
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                new XElement (m_ns + Xml.AirSync.ApplicationData,
                    new XElement (EmailNs + Xml.Email.Flag,
                        new XElement (EmailNs + Xml.Email.Status, (uint)Xml.Email.FlagStatusCode.Set_2),
                        new XElement (EmailNs + Xml.Email.FlagType, pending.EmailSetFlag_FlagType),
                        new XElement (TasksNs + Xml.Tasks.StartDate, pending.EmailSetFlag_Start.ToLocalTime ().ToAsUtcString ()),
                        new XElement (TasksNs + Xml.Tasks.UtcStartDate, pending.EmailSetFlag_UtcStart.ToAsUtcString ()),
                        new XElement (TasksNs + Xml.Tasks.DueDate, pending.EmailSetFlag_Due.ToLocalTime ().ToAsUtcString ()),
                        new XElement (TasksNs + Xml.Tasks.UtcDueDate, pending.EmailSetFlag_UtcDue.ToAsUtcString ()))));
        }

        private XElement ToEmailClearFlag (McPending pending)
        {
            return new XElement (m_ns + Xml.AirSync.Change,
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                new XElement (m_ns + Xml.AirSync.ApplicationData,
                    new XElement (EmailNs + Xml.Email.Flag)));
        }

        private XElement ToEmailMarkFlagDone (McPending pending)
        {
            return new XElement (m_ns + Xml.AirSync.Change,
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                new XElement (m_ns + Xml.AirSync.ApplicationData,
                    new XElement (EmailNs + Xml.Email.Flag,
                        new XElement (EmailNs + Xml.Email.Status, (uint)Xml.Email.FlagStatusCode.MarkDone_1),
                        new XElement (EmailNs + Xml.Email.CompleteTime, pending.EmailMarkFlagDone_CompleteTime.ToAsUtcString ()),
                        new XElement (TasksNs + Xml.Tasks.DateCompleted, pending.EmailMarkFlagDone_DateCompleted.ToAsUtcString ()))));
        }

        private XElement ToCalCreate (McPending pending, McFolder folder)
        {
            var cal = McCalendar.QueryById<McCalendar> (pending.ItemId);
            var add = new XElement (m_ns + Xml.AirSync.Add, 
                          new XElement (m_ns + Xml.AirSync.ClientId, pending.ClientId));
            if (Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (folder.Type) !=
                McFolderEntry.ClassCodeEnum.Calendar) {
                add.Add (new XElement (m_ns + Xml.AirSync.Class, Xml.AirSync.ClassCode.Calendar));
            }
            add.Add (AsHelpers.ToXmlApplicationData (cal));
            return add;
        }

        private XElement ToCalUpdate (McPending pending, McFolder folder)
        {
            var cal = McCalendar.QueryById<McCalendar> (pending.ItemId);
            return new XElement (m_ns + Xml.AirSync.Change, 
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                AsHelpers.ToXmlApplicationData (cal));
        }

        private XElement ToCalDelete (McPending pending, McFolder folder)
        {
            return new XElement (m_ns + Xml.AirSync.Delete,
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId));
        }

        private XElement ToContactCreate (McPending pending, McFolder folder)
        {
            var contact = McObject.QueryById<McContact> (pending.ItemId);
            var add = new XElement (m_ns + Xml.AirSync.Add, 
                          new XElement (m_ns + Xml.AirSync.ClientId, pending.ClientId));
            if (Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (folder.Type) !=
                McFolderEntry.ClassCodeEnum.Contact) {
                add.Add (new XElement (m_ns + Xml.AirSync.Class, Xml.AirSync.ClassCode.Contacts));
            }
            add.Add (contact.ToXmlApplicationData ());
            return add;
        }

        private XElement ToContactUpdate (McPending pending, McFolder folder)
        {
            var contact = McObject.QueryById<McContact> (pending.ItemId);
            return new XElement (m_ns + Xml.AirSync.Change, 
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                contact.ToXmlApplicationData ());
        }

        private XElement ToContactDelete (McPending pending, McFolder folder)
        {
            return new XElement (m_ns + Xml.AirSync.Delete,
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId));
        }

        private XElement ToTaskCreate (McPending pending, McFolder folder)
        {
            var task = McObject.QueryById<McTask> (pending.ItemId);
            var add = new XElement (m_ns + Xml.AirSync.Add, 
                          new XElement (m_ns + Xml.AirSync.ClientId, pending.ClientId));
            if (Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (folder.Type) !=
                McFolderEntry.ClassCodeEnum.Tasks) {
                add.Add (new XElement (m_ns + Xml.AirSync.Class, Xml.AirSync.ClassCode.Tasks));
            }
            add.Add (task.ToXmlApplicationData ());
            return add;
        }

        private XElement ToTaskUpdate (McPending pending, McFolder folder)
        {
            var task = McObject.QueryById<McTask> (pending.ItemId);
            return new XElement (m_ns + Xml.AirSync.Change, 
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                task.ToXmlApplicationData ());
        }

        private XElement ToTaskDelete (McPending pending, McFolder folder)
        {
            return new XElement (m_ns + Xml.AirSync.Delete,
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId));
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            if (0 == SyncKitList.Count) {
                // Abort if there are no folders to Sync.
                throw new AsCommand.AbortCommandException ();
            }
            var collections = new XElement (m_ns + Xml.AirSync.Collections);
            foreach (var tup in SyncKitList) {
                var folder = tup.Item1;
                var pendingSubList = tup.Item2;
                var collection = new XElement (m_ns + Xml.AirSync.Collection,
                                     new XElement (m_ns + Xml.AirSync.SyncKey, folder.AsSyncKey),
                                     new XElement (m_ns + Xml.AirSync.CollectionId, folder.ServerId));
                // GetChanges.
                if (folder.AsSyncMetaDoGetChanges) {
                    collection.Add (new XElement (m_ns + Xml.AirSync.GetChanges));
                    collection.Add (new XElement (m_ns + Xml.AirSync.WindowSize, folder.AsSyncMetaWindowSize));
                
                    // WindowSize.
                    // Options.
                    var classCodeEnum = Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (folder.Type);
                    var options = new XElement (m_ns + Xml.AirSync.Options);
                    switch (classCodeEnum) {
                    case McFolderEntry.ClassCodeEnum.Email:
                        options.Add (new XElement (m_ns + Xml.AirSync.FilterType, (uint)folder.AsSyncMetaFilterCode));
                        options.Add (new XElement (m_ns + Xml.AirSync.MimeSupport, (uint)Xml.AirSync.MimeSupportCode.NoMime_0));
                        options.Add (new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                            new XElement (m_baseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.Html_2),
                            new XElement (m_baseNs + Xml.AirSyncBase.TruncationSize, "128"),
                            new XElement (m_baseNs + Xml.AirSyncBase.AllOrNone, "1"),
                            new XElement (m_baseNs + Xml.AirSyncBase.Preview, "255")));
                        options.Add (new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                            new XElement (m_baseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.PlainText_1),
                            new XElement (m_baseNs + Xml.AirSyncBase.TruncationSize, "128"),
                            new XElement (m_baseNs + Xml.AirSyncBase.AllOrNone, "1"),
                            new XElement (m_baseNs + Xml.AirSyncBase.Preview, "255")));
                        break;

                    case McFolderEntry.ClassCodeEnum.Calendar:
                        options.Add (new XElement (m_ns + Xml.AirSync.FilterType, (uint)folder.AsSyncMetaFilterCode));
                        options.Add (new XElement (m_ns + Xml.AirSync.MimeSupport, (uint)Xml.AirSync.MimeSupportCode.AllMime_2));
                        options.Add (new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                            new XElement (m_baseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.Mime_4),
                            new XElement (m_baseNs + Xml.AirSyncBase.TruncationSize, "100000000")));
                        break;

                    case McFolderEntry.ClassCodeEnum.Contact:
                        if (Xml.FolderHierarchy.TypeCode.Ric_19 == folder.Type) {
                            // Expressing BodyPreference for RIC gets Protocol Error.
                            options.Add (new XElement (m_ns + Xml.AirSync.MaxItems, "200"));
                        } else {
                            options.Add (new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                                new XElement (m_baseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.PlainText_1),
                                new XElement (m_baseNs + Xml.AirSyncBase.TruncationSize, "100000000")));
                        }
                        break;

                    case McFolderEntry.ClassCodeEnum.Tasks:
                        options.Add (new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                            new XElement (m_baseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.PlainText_1),
                            new XElement (m_baseNs + Xml.AirSyncBase.TruncationSize, "100000000")));
                        break;
                    }
                    if (options.HasElements) {
                        collection.Add (options);
                    }
                }
                // Commands.  
                var commands = new XElement (m_ns + Xml.AirSync.Commands);
                foreach (var pending in pendingSubList) {
                    switch (pending.Operation) {
                    case McPending.Operations.EmailDelete:
                        commands.Add (ToEmailDelete (pending));
                        break;
                    case McPending.Operations.EmailMarkRead:
                        commands.Add (ToEmailMarkRead (pending));
                        break;
                    case McPending.Operations.EmailSetFlag:
                        commands.Add (ToEmailSetFlag (pending));
                        break;
                    case McPending.Operations.EmailClearFlag:
                        commands.Add (ToEmailClearFlag (pending));
                        break;
                    case McPending.Operations.EmailMarkFlagDone:
                        commands.Add (ToEmailMarkFlagDone (pending));
                        break;
                    case McPending.Operations.CalCreate:
                        commands.Add (ToCalCreate (pending, folder));
                        break;
                    case McPending.Operations.CalUpdate:
                        commands.Add (ToCalUpdate (pending, folder));
                        break;
                    case McPending.Operations.CalDelete:
                        commands.Add (ToCalDelete (pending, folder));
                        break;
                    case McPending.Operations.ContactCreate:
                        commands.Add (ToContactCreate (pending, folder));
                        break;
                    case McPending.Operations.ContactUpdate:
                        commands.Add (ToContactUpdate (pending, folder));
                        break;
                    case McPending.Operations.ContactDelete:
                        commands.Add (ToContactDelete (pending, folder));
                        break;
                    case McPending.Operations.TaskCreate:
                        commands.Add (ToTaskCreate (pending, folder));
                        break;
                    case McPending.Operations.TaskUpdate:
                        commands.Add (ToTaskUpdate (pending, folder));
                        break;
                    case McPending.Operations.TaskDelete:
                        commands.Add (ToTaskDelete (pending, folder));
                        break;
                    default:
                        NcAssert.True (false);
                        break;
                    }
                }
                if (commands.HasElements) {
                    collection.Add (commands);
                }
                collections.Add (collection);
            }
            var sync = new XElement (m_ns + Xml.AirSync.Sync, collections);
            sync.Add (new XElement (m_ns + Xml.AirSync.WindowSize, WindowSize));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (sync);
            return doc;
        }

        public override Event ProcessTopLevelStatus (AsHttpOperation sender, uint status)
        {
            var globEvent = base.ProcessTopLevelStatus (sender, status);
            if (null != globEvent) {
                return globEvent;
            }
            switch ((Xml.AirSync.StatusCode)status) {
            case Xml.AirSync.StatusCode.Success_1:
                return null;

            case Xml.AirSync.StatusCode.SyncKeyInvalid_3: // FIXME see folder AsResetState.
                // FIXME - need resolution logic to deal with _Initial-level re-sync of a folder.
                // FoldersInRequest is NOT stale here.
                foreach (var folder in FoldersInRequest) {
                    folder.AsSyncKey = McFolder.AsSyncKey_Initial;
                    folder.AsSyncMetaToClientExpected = true;
                    folder.Update ();
                }
                ResolveAllDeferred ();
                return Event.Create ((uint)SmEvt.E.HardFail, "ASYNCTOPFOOF");

            case Xml.AirSync.StatusCode.ProtocolError_4:
                var result = NcResult.Error (NcResult.SubKindEnum.Error_ProtocolError);
                lock (PendingResolveLockObj) {
                    if (0 == PendingList.Count) {
                        // We're syncing because of SM, and something tastes bad to the server.
                        return Event.Create ((uint)SmEvt.E.HardFail, "ASYNCPE0");
                    } else if (1 == PendingList.Count) {
                        var pending = PendingList.First ();
                        pending.ResolveAsHardFail (BEContext.ProtoControl, result);
                        PendingList.Clear ();
                        return Event.Create ((uint)SmEvt.E.HardFail, "ASYNCPE1");
                    } else {
                        foreach (var pending in PendingList) {
                            pending.DeferredSerialIssueOnly = true;
                            pending.ResolveAsDeferred (BEContext.ProtoControl, DateTime.UtcNow, result);
                        }
                        PendingList.Clear ();
                        return Event.Create ((uint)SmEvt.E.HardFail, "ASYNCTOPPE");
                    }
                }
            case Xml.AirSync.StatusCode.ServerError_5:
                // TODO: should retry Sync a few times before resetting to Initial.
                foreach (var folder in FoldersInRequest) {
                    folder.AsSyncKey = McFolder.AsSyncKey_Initial;
                    folder.AsSyncMetaToClientExpected = true;
                    folder.Update ();
                }
                ResolveAllDeferred ();
                return Event.Create ((uint)SmEvt.E.TempFail, "ASYNCTOPRS");

            case Xml.AirSync.StatusCode.FolderChange_12:
                foreach (var folder in FoldersInRequest) {
                    folder.AsSyncKey = McFolder.AsSyncKey_Initial;
                    folder.AsSyncMetaToClientExpected = true;
                    folder.Update ();
                }
                ResolveAllDeferred ();
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "ASYNCTOPRFS");

            case Xml.AirSync.StatusCode.Retry_16:
                foreach (var folder in FoldersInRequest) {
                    folder.AsSyncKey = McFolder.AsSyncKey_Initial;
                    folder.AsSyncMetaToClientExpected = true;
                    folder.Update ();
                }
                ResolveAllDeferred ();
                return Event.Create ((uint)SmEvt.E.TempFail, "ASYNCTOPRRR");

            default:
                Log.Error (Log.LOG_AS, "AsSyncCommand ProcessResponse UNHANDLED Top Level status: {0}", status);
                return null;
            }
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            List<McFolder> processedFolders = new List<McFolder> ();
            var xmlLimit = doc.Root.Element (m_ns + Xml.AirSync.Limit);
            if (null != xmlLimit) {
                Limit = uint.Parse (xmlLimit.Value);
            }
            // ProcessTopLevelStatus will handle Status element, if  included.
            // If we get here, we know any TL Status is okay.
            var xmlCollections = doc.Root.Element (m_ns + Xml.AirSync.Collections);
            if (null == xmlCollections) {
                return Event.Create ((uint)SmEvt.E.Success, "SYNCSUCCODD");
            }
            var collections = xmlCollections.Elements (m_ns + Xml.AirSync.Collection);
            // Note: we may get back zero Collection items.
            foreach (var collection in collections) {
                List<McPending> pendingInFolder;
                // Note: CollectionId, Status and SyncKey are required to be present.
                var serverId = collection.Element (m_ns + Xml.AirSync.CollectionId).Value;
                var folder = McFolder.ServerEndQueryByServerId (BEContext.Account.Id, serverId);
                var oldSyncKey = folder.AsSyncKey;
                var xmlSyncKey = collection.Element (m_ns + Xml.AirSync.SyncKey);
                var xmlMoreAvailable = collection.Element (m_ns + Xml.AirSync.MoreAvailable);
                var xmlStatus = collection.Element (m_ns + Xml.AirSync.Status);

                if (null != xmlSyncKey) {
                    // The protocol requires SyncKey, but GOOG does not obey in the StatusCode.NotFound case.
                    folder.AsSyncKey = xmlSyncKey.Value;
                    folder.AsSyncMetaToClientExpected = (McFolder.AsSyncKey_Initial == oldSyncKey) || (null != xmlMoreAvailable);
                } else {
                    Log.Warn (Log.LOG_SYNC, "SyncKey missing from XML.");
                }
                processedFolders.Add (folder);
                Log.Info (Log.LOG_SYNC, "MoreAvailable presence {0}", (null != xmlMoreAvailable));
                Log.Info (Log.LOG_SYNC, "Folder:{0}, Old SyncKey:{1}, New SyncKey:{2}", folder.ServerId, oldSyncKey, folder.AsSyncKey);
                var status = (Xml.AirSync.StatusCode)uint.Parse (xmlStatus.Value);
                switch (status) {
                case Xml.AirSync.StatusCode.Success_1:
                    var xmlResponses = collection.Element (m_ns + Xml.AirSync.Responses);
                    ProcessCollectionResponses (folder, xmlResponses);

                    var xmlCommands = collection.Element (m_ns + Xml.AirSync.Commands);
                    ProcessCollectionCommands (folder, xmlCommands);

                    lock (PendingResolveLockObj) {
                        // Any pending not already resolved gets resolved as Success.
                        pendingInFolder = PendingList.Where (x => x.ParentId == folder.ServerId).ToList ();
                        foreach (var pending in pendingInFolder) {
                            PendingList.Remove (pending);
                            pending.ResolveAsSuccess (BEContext.ProtoControl);
                        }
                    }
                    break;

                case Xml.AirSync.StatusCode.ServerError_5:
                    // TODO: try ReSync again a FEW times before resetting the SyncKey value.
                case Xml.AirSync.StatusCode.SyncKeyInvalid_3:
                    /* FIXME: The client SHOULD either delete any items that were added since the last successful 
                     * Sync or the client MUST add those items back to the server after completing the full
                     * resynchronization.
                     */
                    folder.AsSyncKey = McFolder.AsSyncKey_Initial;
                    folder.AsSyncMetaToClientExpected = true;
                    lock (PendingResolveLockObj) {
                        // Defer all the outbound commands until after ReSync.
                        pendingInFolder = PendingList.Where (x => x.ParentId == folder.ServerId).ToList ();
                        foreach (var pending in pendingInFolder) {
                            PendingList.Remove (pending);
                            pending.ResolveAsDeferred (BEContext.ProtoControl,
                                McPending.DeferredEnum.UntilSync,
                                NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure));
                        }
                    }
                    break;

                case Xml.AirSync.StatusCode.ProtocolError_4:
                    lock (PendingResolveLockObj) {
                        pendingInFolder = PendingList.Where (x => x.ParentId == folder.ServerId).ToList ();
                        var result = NcResult.Error (NcResult.SubKindEnum.Error_ProtocolError);
                        if (1 == pendingInFolder.Count ()) {
                            var pending = pendingInFolder.First ();
                            PendingList.Remove (pending);
                            pending.ResolveAsHardFail (BEContext.ProtoControl, result);
                        } else {
                            // Go into serial mode for these pending to weed out the bad apple.
                            // TODO: why not DeferredForce?
                            foreach (var pending in pendingInFolder) {
                                PendingList.Remove (pending);
                                pending.DeferredSerialIssueOnly = true;
                                pending.ResolveAsDeferred (BEContext.ProtoControl, DateTime.UtcNow, result);
                            }
                        }
                    }
                    break;

                case Xml.AirSync.StatusCode.FolderChange_12:
                    FolderSyncIsMandated = true;
                    lock (PendingResolveLockObj) {
                        pendingInFolder = PendingList.Where (x => x.ParentId == folder.ServerId).ToList ();
                        foreach (var pending in pendingInFolder) {
                            PendingList.Remove (pending);
                            pending.ResolveAsDeferred (BEContext.ProtoControl,
                                McPending.DeferredEnum.UntilFSync,
                                NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure));
                        }
                    }
                    break;

                case Xml.AirSync.StatusCode.Retry_16:
                    folder.AsSyncMetaToClientExpected = true;
                    lock (PendingResolveLockObj) {
                        pendingInFolder = PendingList.Where (x => x.ParentId == folder.ServerId).ToList ();
                        foreach (var pending in pendingInFolder) {
                            PendingList.Remove (pending);
                            pending.ResolveAsDeferredForce ();
                        }
                    }
                    break;

                default:
                    Log.Error (Log.LOG_AS, "AsSyncCommand ProcessResponse UNHANDLED Collection status: {0}", status);
                    break;
                }
                folder.Update ();
            }
            // For any folders missing from the response, we need to note that there isn't more on the server-side.
            // Remember the loop above re-writes folders, so FoldersInRequest object will be stale!
            List<McFolder> reloadedFolders = new List<McFolder> ();
            foreach (var maybeStale in FoldersInRequest) {
                var folder = McFolder.ServerEndQueryById (maybeStale.Id);
                if (0 == processedFolders.Where (f => folder.Id == f.Id).Count ()) {
                    folder.AsSyncMetaToClientExpected = false;
                    folder.Update ();
                }
                reloadedFolders.Add (folder);
            }
            BEContext.ProtoControl.SyncStrategy.ReportSyncResult (reloadedFolders);

            if (HadEmailMessageSetChanges) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
            }
            if (HadNewUnreadEmailMessageInInbox) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox));
            }
            if (HadContactSetChanges) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            }
            if (HadCalendarSetChanges) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            }
            if (HadTaskSetChanges) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_TaskSetChanged));
            }
            // TODO: Should we sent these per-message?
            if (HadEmailMessageChanges) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageChanged));
            }
            if (HadContactChanges) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactChanged));
            }
            if (HadCalendarChanges) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarChanged));
            }
            if (HadTaskChanges) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_TaskChanged));
            }
            if (FolderSyncIsMandated) {
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "SYNCREFSYNC0");
            } else {
                return Event.Create ((uint)SmEvt.E.Success, "SYNCSUCCESS0");
            }
        }
        // Called when we get an empty Sync response body.
        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response)
        {
            // FoldersInRequest NOT stale here.
            foreach (var folder in FoldersInRequest) {
                folder.AsSyncMetaToClientExpected = false;
                folder.Update ();
            }
            BEContext.ProtoControl.SyncStrategy.ReportSyncResult (FoldersInRequest);
            lock (PendingResolveLockObj) {
                foreach (var pending in PendingList) {
                    pending.ResolveAsSuccess (BEContext.ProtoControl);
                }
                PendingList.Clear ();
            }
            return Event.Create ((uint)SmEvt.E.Success, "SYNCSUCCESS1");
        }

        public override void StatusInd (bool didSucceed)
        {
            if (didSucceed) {
                McPending.MakeEligibleOnSync (BEContext.Account.Id);
            }
            base.StatusInd (didSucceed);
        }

        public override bool WasAbleToRephrase ()
        {
            lock (PendingResolveLockObj) {
                // See if we are trying to do a bunch in parallel - if so go serial.
                var firstPending = PendingList.FirstOrDefault ();
                if (null == firstPending) {
                    return false;
                }
                if (1 >= PendingList.Count || firstPending.DeferredSerialIssueOnly) {
                    // We are already doing serial.
                    return false;
                }
                foreach (var pending in PendingList) {
                    pending.DeferredSerialIssueOnly = true;
                    if (pending == firstPending) {
                        pending.Update ();
                        continue;
                    }
                    pending.ResolveAsDeferredForce ();
                }
                PendingList.Clear ();
            }
            ApplyStrategy ();
            return true;
        }
        // TODO - make this a generic extension.
        private static bool ParseXmlBoolean (XElement bit)
        {
            if (bit.IsEmpty) {
                return true;
            }
            switch (bit.Value) {
            case "0":
                return false;
            case "1":
                return true;
            default:
                throw new Exception ();
            }
        }

        private string GetClassCode (XElement command, McFolder folder)
        {
            var xmlClass = command.Element (m_ns + Xml.AirSync.Class);
            // If the Class element is present, respect it. Otherwise key off
            // the type of the folder.
            if (null != xmlClass) {
                return xmlClass.Value;
            } else {
                return Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (folder.Type);
            }
        }

        private void ProcessCollectionCommands (McFolder folder, XElement xmlCommands)
        {
            if (null == xmlCommands) {
                return;
            }
            var commands = xmlCommands.Elements ();
            foreach (var command in commands) {
                var classCode = GetClassCode (command, folder);
                switch (command.Name.LocalName) {
                case Xml.AirSync.Add:
                    var addServerId = command.Element (m_ns + Xml.AirSync.ServerId).Value;
                    var pathElem = new McPath (BEContext.Account.Id);
                    pathElem.ServerId = addServerId;
                    pathElem.ParentId = folder.ServerId;
                    pathElem.Insert ();
                    var applyAdd = new ApplyItemAdd (BEContext.Account.Id) {
                        ClassCode = classCode,
                        ServerId = addServerId,
                        XmlCommand = command,
                        Folder = folder,
                    };
                    applyAdd.ProcessServerCommand ();
                    var xmlApplicationData = command.ElementAnyNs ("ApplicationData");
                    switch (classCode) {
                    case Xml.AirSync.ClassCode.Email:
                        HadEmailMessageSetChanges = true;
                        if (Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == folder.Type &&
                            null != xmlApplicationData) {
                            var xmlRead = xmlApplicationData.ElementAnyNs ("Read");
                            if (null != xmlRead && "0" == xmlRead.Value) {
                                HadNewUnreadEmailMessageInInbox = true;
                            }
                        }
                        break;
                    case Xml.AirSync.ClassCode.Contacts:
                        HadContactSetChanges = true;
                        break;
                    case Xml.AirSync.ClassCode.Calendar:
                        HadCalendarSetChanges = true;
                        break;
                    case Xml.AirSync.ClassCode.Tasks:
                        HadTaskSetChanges = true;
                        break;
                    }
                    break;
                case Xml.AirSync.Change:
                    var chgServerId = command.Element (m_ns + Xml.AirSync.ServerId).Value;
                    var applyChange = new ApplyItemChange (BEContext.Account.Id) {
                        ClassCode = classCode,
                        ServerId = chgServerId,
                        XmlCommand = command,
                        Folder = folder,
                    };
                    applyChange.ProcessServerCommand ();
                    switch (classCode) {
                    case Xml.AirSync.ClassCode.Email:
                        HadEmailMessageChanges = true;
                        break;
                    case Xml.AirSync.ClassCode.Calendar:
                        HadCalendarChanges = true;
                        break;
                    case Xml.AirSync.ClassCode.Contacts:
                        HadContactChanges = true;
                        break;
                    case Xml.AirSync.ClassCode.Tasks:
                        HadTaskChanges = true;
                        break;
                    default:
                        Log.Error (Log.LOG_AS, "AsSyncCommand ProcessCollectionCommands UNHANDLED class " + classCode);
                        break;
                    }
                    break;

                case Xml.AirSync.Delete:
                    var delServerId = command.Element (m_ns + Xml.AirSync.ServerId).Value;
                    pathElem = McPath.QueryByServerId (BEContext.Account.Id, delServerId);
                    pathElem.Delete ();
                    var applyDelete = new ApplyItemDelete (BEContext.Account.Id) {
                        ClassCode = classCode,
                        ServerId = delServerId,
                    };
                    applyDelete.ProcessServerCommand ();
                    switch (classCode) {
                    case Xml.AirSync.ClassCode.Email:
                        HadEmailMessageSetChanges = true;
                        break;
                    case Xml.AirSync.ClassCode.Calendar:
                        HadCalendarSetChanges = true;
                        break;
                    case Xml.AirSync.ClassCode.Contacts:
                        HadContactSetChanges = true;
                        break;
                    case Xml.AirSync.ClassCode.Tasks:
                        HadTaskSetChanges = true;
                        break;
                    default:
                        Log.Error (Log.LOG_AS, "AsSyncCommand ProcessCollectionCommands UNHANDLED class " + classCode);
                        break;
                    }
                    break;

                case Xml.AirSync.SoftDelete:
                    // TODO: Implement SoftDelete if we want to constrain storage. Need to be careful 
                    // ignore SoftDelete for in-window items when doing quick-fetch.
                    break;

                default:
                    Log.Error (Log.LOG_AS, "AsSyncCommand ProcessResponse UNHANDLED command " + command.Name.LocalName);
                    break;
                }
            }
        }

        private void ProcessCollectionResponses (McFolder folder, XElement xmlResponses)
        {
            if (null == xmlResponses) {
                return;
            }
            var responses = xmlResponses.Elements ();
            foreach (var response in responses) {
                var classCode = GetClassCode (response, folder);
                // Underlying McPending are resolved by this switch statement and the loop below.
                switch (response.Name.LocalName) {
                case Xml.AirSync.Add:
                    ProcessCollectionAddResponse (folder, response, classCode);
                    break;

                case Xml.AirSync.Change:
                    ProcessCollectionChangeResponse (folder, response, classCode);
                    break;

                case Xml.AirSync.Fetch:
                    ProcessCollectionFetchResponse (folder, response, classCode);
                    break;

                default:
                    Log.Error (Log.LOG_AS, "AsSyncCommand ProcessCollectionResponses UNHANDLED response " + response.Name.LocalName);
                    break;
                }
            }
            // Because success is not reported in the response document.
            lock (PendingResolveLockObj) {
                var pendingChanges = PendingList.Where (x => 
                x.State == McPending.StateEnum.Dispatched &&
                                     (x.Operation == McPending.Operations.CalUpdate ||
                                     x.Operation == McPending.Operations.ContactUpdate));
                foreach (var pendingChange in pendingChanges) {
                    pendingChange.ResolveAsSuccess (BEContext.ProtoControl);
                }
            }
        }

        private void ProcessCollectionAddResponse (McFolder folder, XElement xmlAdd, string classCode)
        {
            McPending pending;
            McItem item;
            bool success = false;

            // Status and ClientId are required to be present.
            var xmlClientId = xmlAdd.Element (m_ns + Xml.AirSync.ClientId);
            var clientId = xmlClientId.Value;
            pending = McPending.QueryByClientId (folder.AccountId, clientId); 
            // FIXME - need to use lock for PendingList. need to see pending in list before processing.
            var xmlStatus = xmlAdd.Element (m_ns + Xml.AirSync.Status);
            var status = (Xml.AirSync.StatusCode)uint.Parse (xmlStatus.Value);
            switch (status) {
            case Xml.AirSync.StatusCode.Success_1:
                PendingList.RemoveAll (x => x.Id == pending.Id);
                pending.ResolveAsSuccess (BEContext.ProtoControl);
                success = true;
                break;

            case Xml.AirSync.StatusCode.ProtocolError_4:
            case Xml.AirSync.StatusCode.ClientError_6:
                PendingList.RemoveAll (x => x.Id == pending.Id);
                pending.ResolveAsHardFail (BEContext.ProtoControl, 
                    NcResult.Error (NcResult.SubKindEnum.Error_ProtocolError));
                break;

            case Xml.AirSync.StatusCode.ServerWins_7:
                PendingList.RemoveAll (x => x.Id == pending.Id);
                pending.ResolveAsHardFail (BEContext.ProtoControl,
                    NcResult.Error (NcResult.SubKindEnum.Error_ServerConflict));
                break;

            case Xml.AirSync.StatusCode.NoSpace_9:
                PendingList.RemoveAll (x => x.Id == pending.Id);
                pending.ResolveAsUserBlocked (BEContext.ProtoControl,
                    McPending.BlockReasonEnum.UserRemediation,
                    NcResult.Error (NcResult.SubKindEnum.Error_NoSpace));
                break;

            case Xml.AirSync.StatusCode.LimitReWait_14:
                Log.Warn (Log.LOG_AS, "Received Sync Response status code LimitReWait_14, but we don't use HeartBeatInterval with Sync.");
                PendingList.RemoveAll (x => x.Id == pending.Id);
                pending.ResolveAsSuccess (BEContext.ProtoControl);
                success = true;
                break;

            case Xml.AirSync.StatusCode.TooMany_15:
                var protocolState = BEContext.ProtoControl.ProtocolState;
                if (null != Limit) {
                    protocolState.AsSyncLimit = (uint)Limit;
                }
                PendingList.RemoveAll (x => x.Id == pending.Id);
                pending.ResolveAsSuccess (BEContext.ProtoControl);
                success = true;
                break;

            default:
            case Xml.AirSync.StatusCode.NotFound_8:
                // Note: we don't send partial Sync requests.
            case Xml.AirSync.StatusCode.ResendFull_13:
                PendingList.RemoveAll (x => x.Id == pending.Id);
                pending.ResponsegXmlStatus = (uint)status; // FIXME move this up.
                pending.ResolveAsHardFail (BEContext.ProtoControl,
                    NcResult.Error (NcResult.SubKindEnum.Error_InappropriateStatus));
                break;
            }

            if (!success) {
                return;
            }

            switch (classCode) {
            case Xml.AirSync.ClassCode.Email:
                item = McItem.QueryByClientId<McEmailMessage> (BEContext.Account.Id, clientId);
                break;
            case Xml.AirSync.ClassCode.Contacts:
                item = McItem.QueryByClientId<McContact> (BEContext.Account.Id, clientId);
                break;
            case Xml.AirSync.ClassCode.Calendar:
                item = McItem.QueryByClientId<McCalendar> (BEContext.Account.Id, clientId);
                break;
            case Xml.AirSync.ClassCode.Tasks:
                item = McItem.QueryByClientId<McTask> (BEContext.Account.Id, clientId);
                break;
            default:
                Log.Error (Log.LOG_AS, "AsSyncCommand ProcessCollectionResponses UNHANDLED class " + classCode);
                return;
            }
            var xmlServerId = xmlAdd.Element (m_ns + Xml.AirSync.ServerId);
            var serverId = xmlServerId.Value;
            if (null != serverId) {
                item.ServerId = serverId;
            }
            item.Update ();
        }

        private void ProcessCollectionChangeResponse (McFolder folder, XElement xmlChange, string classCode)
        {
            // Note: we are only supposed to see Change in this context if there was a failure.
            switch (classCode) {
            case Xml.AirSync.ClassCode.Email:
            case Xml.AirSync.ClassCode.Contacts:
            case Xml.AirSync.ClassCode.Calendar:
                var xmlStatus = xmlChange.Element (m_ns + Xml.AirSync.Status);
                var xmlServerId = xmlChange.Element (m_ns + Xml.AirSync.ServerId);
                if (null != xmlStatus && null != xmlServerId) {
                    // If we don't have Status and ServerId - how do we identify & react?
                    var status = (Xml.AirSync.StatusCode)uint.Parse (xmlStatus.Value);
                    var serverId = xmlServerId.Value;
                    // FIXME - need to use lock for PendingList. need to see pending in list before processing.
                    var pending = McPending.QueryByServerId (folder.AccountId, serverId);
                    switch (status) {
                    case Xml.AirSync.StatusCode.ProtocolError_4:
                    case Xml.AirSync.StatusCode.ClientError_6:
                        PendingList.RemoveAll (x => x.Id == pending.Id);
                        pending.ResolveAsHardFail (BEContext.ProtoControl, 
                            NcResult.Error (NcResult.SubKindEnum.Error_ProtocolError));
                        break;

                    case Xml.AirSync.StatusCode.ServerWins_7:
                        PendingList.RemoveAll (x => x.Id == pending.Id);
                        pending.ResolveAsHardFail (BEContext.ProtoControl,
                            NcResult.Error (NcResult.SubKindEnum.Error_ServerConflict));
                        break;

                    case Xml.AirSync.StatusCode.NotFound_8:
                        folder.AsSyncMetaToClientExpected = true;
                        folder.Update ();
                        PendingList.RemoveAll (x => x.Id == pending.Id);
                        pending.ResolveAsDeferred (BEContext.ProtoControl,
                            McPending.DeferredEnum.UntilSync,
                            NcResult.Error (NcResult.SubKindEnum.Error_ObjectNotFoundOnServer));
                        break;

                    case Xml.AirSync.StatusCode.NoSpace_9:
                        PendingList.RemoveAll (x => x.Id == pending.Id);
                        pending.ResolveAsUserBlocked (BEContext.ProtoControl,
                            McPending.BlockReasonEnum.UserRemediation,
                            NcResult.Error (NcResult.SubKindEnum.Error_NoSpace));
                        break;

                    case Xml.AirSync.StatusCode.LimitReWait_14:
                        Log.Warn (Log.LOG_AS, "Received Sync Response status code LimitReWait_14, but we don't use HeartBeatInterval with Sync.");
                        PendingList.RemoveAll (x => x.Id == pending.Id);
                        pending.ResolveAsSuccess (BEContext.ProtoControl);
                        break;

                    case Xml.AirSync.StatusCode.TooMany_15:
                        var protocolState = BEContext.ProtoControl.ProtocolState;
                        if (null != Limit) {
                            protocolState.AsSyncLimit = (uint)Limit;
                            protocolState.Update ();
                        }
                        PendingList.RemoveAll (x => x.Id == pending.Id);
                        pending.ResolveAsSuccess (BEContext.ProtoControl);
                        break;

                    default:
                        // Note: we don't send partial Sync requests.
                    case Xml.AirSync.StatusCode.ResendFull_13:
                        PendingList.RemoveAll (x => x.Id == pending.Id);
                        pending.ResponsegXmlStatus = (uint)status;
                        pending.ResolveAsHardFail (BEContext.ProtoControl,
                            NcResult.Error (NcResult.SubKindEnum.Error_InappropriateStatus));
                        break;
                    }
                }
                return;
            default:
                Log.Error (Log.LOG_AS, "AsSyncCommand ProcessCollectionResponses UNHANDLED class " + classCode);
                return;
            }
        }

        private void ProcessCollectionFetchResponse (McFolder folder, XElement xmlFetch, string classCode)
        {
            // Note: we are only supposed to see Fetch here if it succeeded.
            // We don't implement fetch yet. When we do, we will need to resolve all the McPendings,
            // even those that aren't in the response document.
        }

        public static bool IsSyncCommand (McPending.Operations op)
        {
            switch (op) {
            case McPending.Operations.EmailDelete:
            case McPending.Operations.EmailMarkRead:
            case McPending.Operations.EmailSetFlag:
            case McPending.Operations.EmailClearFlag:
            case McPending.Operations.EmailMarkFlagDone:
            case McPending.Operations.CalCreate:
            case McPending.Operations.CalUpdate:
            case McPending.Operations.CalDelete:
            case McPending.Operations.ContactCreate:
            case McPending.Operations.ContactUpdate:
            case McPending.Operations.ContactDelete:
            case McPending.Operations.TaskCreate:
            case McPending.Operations.TaskUpdate:
            case McPending.Operations.TaskDelete:
                return true;
            default:
                return false;
            }
        }
    }
}
