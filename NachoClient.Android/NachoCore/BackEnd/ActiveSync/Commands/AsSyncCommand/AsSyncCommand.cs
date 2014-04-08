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
        private bool HadEmailMessageChanges;
        private bool HadContactChanges;
        private bool HadCalendarChanges;
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
                        new XElement (EmailNs + Xml.Email.FlagType, pending.FlagType),
                        new XElement (TasksNs + Xml.Tasks.StartDate, pending.Start.ToLocalTime ().ToAsUtcString ()),
                        new XElement (TasksNs + Xml.Tasks.UtcStartDate, pending.UtcStart.ToAsUtcString ()),
                        new XElement (TasksNs + Xml.Tasks.DueDate, pending.Due.ToLocalTime ().ToAsUtcString ()),
                        new XElement (TasksNs + Xml.Tasks.UtcDueDate, pending.UtcDue.ToAsUtcString ()))));
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
                        new XElement (EmailNs + Xml.Email.CompleteTime, pending.CompleteTime.ToAsUtcString ()),
                        new XElement (TasksNs + Xml.Tasks.DateCompleted, pending.DateCompleted.ToAsUtcString ()))));
        }

        private XElement ToCalCreate (McPending pending, McFolder folder)
        {
            var cal = McCalendar.QueryById<McCalendar> (pending.CalId);
            cal.ReadAncillaryData ();
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
            var cal = McCalendar.QueryById<McCalendar> (pending.CalId);
            cal.ReadAncillaryData ();
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
            var contact = McObject.QueryById<McContact> (pending.ContactId);
            contact.ReadAncillaryData (BackEnd.Instance.Db);
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
            var contact = McObject.QueryById<McContact> (pending.ContactId);
            contact.ReadAncillaryData (BackEnd.Instance.Db);
            return new XElement (m_ns + Xml.AirSync.Change, 
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                contact.ToXmlApplicationData ());
        }

        private XElement ToContactDelete (McPending pending, McFolder folder)
        {
            return new XElement (m_ns + Xml.AirSync.Delete,
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId));
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
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
                    case McFolderEntry.ClassCodeEnum.Calendar:
                        options.Add (new XElement (m_ns + Xml.AirSync.MimeSupport, (uint)Xml.AirSync.MimeSupportCode.AllMime_2));
                        options.Add (new XElement (m_ns + Xml.AirSync.FilterType, (uint)folder.AsSyncMetaFilterCode));
                        options.Add (new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                            new XElement (m_baseNs + Xml.AirSync.Type, (uint)Xml.AirSync.TypeCode.Mime_4),
                            new XElement (m_baseNs + Xml.AirSync.TruncationSize, "100000000")));
                        break;

                    case McFolderEntry.ClassCodeEnum.Contact:
                        options.Add (new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                            new XElement (m_baseNs + Xml.AirSync.Type, (uint)Xml.AirSync.TypeCode.PlainText_1),
                            new XElement (m_baseNs + Xml.AirSync.TruncationSize, "100000000")));
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
                    default:
                        NachoAssert.True (false);
                        break;
                    }
                    pending.MarkDispached ();
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
                foreach (var pending in PendingList) {
                    pending.ResolveAsDeferredForce ();
                }
                PendingList.Clear ();
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "ASYNCTOPFOOF");

            case Xml.AirSync.StatusCode.ProtocolError_4:
                var result = NcResult.Error (NcResult.SubKindEnum.Error_ProtocolError);
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
                    return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "ASYNCTOPPE");
                }

            case Xml.AirSync.StatusCode.ServerError_5:
                // TODO: should retry Sync a few times before resetting to Initial.
                foreach (var folder in FoldersInRequest) {
                    folder.AsSyncKey = McFolder.AsSyncKey_Initial;
                    folder.AsSyncMetaToClientExpected = true;
                    folder.Update ();
                }
                foreach (var pending in PendingList) {
                    pending.ResolveAsDeferredForce ();
                }
                PendingList.Clear ();
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "ASYNCTOPRS");

            case Xml.AirSync.StatusCode.FolderChange_12:
                foreach (var folder in FoldersInRequest) {
                    folder.AsSyncKey = McFolder.AsSyncKey_Initial;
                    folder.AsSyncMetaToClientExpected = true;
                    folder.Update ();
                }
                foreach (var pending in PendingList) {
                    pending.ResolveAsDeferredForce ();
                }
                PendingList.Clear ();
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "ASYNCTOPRFS");

            case Xml.AirSync.StatusCode.Retry_16:
                foreach (var folder in FoldersInRequest) {
                    folder.AsSyncKey = McFolder.AsSyncKey_Initial;
                    folder.AsSyncMetaToClientExpected = true;
                    folder.Update ();
                }
                foreach (var pending in PendingList) {
                    pending.ResolveAsDeferredForce ();
                }
                PendingList.Clear ();
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "ASYNCTOPRRR");

            default:
                Log.Error ("AsSyncCommand ProcessResponse UNHANDLED Top Level status: {0}", status);
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
                var folder = McFolderEntry.QueryByServerId<McFolder> (BEContext.Account.Id, serverId);
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
                    var xmlCommands = collection.Element (m_ns + Xml.AirSync.Commands);
                    ProcessCollectionCommands (folder, xmlCommands);

                    var xmlResponses = collection.Element (m_ns + Xml.AirSync.Responses);
                    ProcessCollectionResponses (folder, xmlResponses);

                    // Any pending not already resolved gets resolved as Success.
                    pendingInFolder = PendingList.Where (x => x.FolderServerId == folder.ServerId).ToList ();
                    foreach (var pending in pendingInFolder) {
                        PendingList.Remove (pending);
                        pending.ResolveAsSuccess (BEContext.ProtoControl);
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
                    // Defer all the outbound commands until after ReSync.
                    pendingInFolder = PendingList.Where (x => x.FolderServerId == folder.ServerId).ToList ();
                    foreach (var pending in pendingInFolder) {
                        PendingList.Remove (pending);
                        pending.ResolveAsDeferred (BEContext.ProtoControl,
                            McPending.DeferredEnum.UntilSync,
                            NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure));
                    }
                    break;

                case Xml.AirSync.StatusCode.ProtocolError_4:
                    pendingInFolder = PendingList.Where (x => x.FolderServerId == folder.ServerId).ToList ();
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
                    break;

                case Xml.AirSync.StatusCode.FolderChange_12:
                    FolderSyncIsMandated = true;
                    pendingInFolder = PendingList.Where (x => x.FolderServerId == folder.ServerId).ToList ();
                    foreach (var pending in pendingInFolder) {
                        PendingList.Remove (pending);
                        pending.ResolveAsDeferred (BEContext.ProtoControl,
                            McPending.DeferredEnum.UntilFSync,
                            NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure));
                    }
                    break;

                case Xml.AirSync.StatusCode.Retry_16:
                    folder.AsSyncMetaToClientExpected = true;
                    pendingInFolder = PendingList.Where (x => x.FolderServerId == folder.ServerId).ToList ();
                    foreach (var pending in pendingInFolder) {
                        PendingList.Remove (pending);
                        pending.ResolveAsDeferredForce ();
                    }
                    break;

                default:
                    Log.Error ("AsSyncCommand ProcessResponse UNHANDLED Collection status: {0}", status);
                    break;
                }
                folder.Update ();
            }
            // For any folders missing from the response, we need to note that there isn't more on the server-side.
            // Remember the loop above re-writes folders, so FoldersInRequest object will be stale!
            List<McFolder> reloadedFolders = new List<McFolder> ();
            foreach (var maybeStale in FoldersInRequest) {
                var folder = McFolder.QueryById<McFolder> (maybeStale.Id);
                if (0 == processedFolders.Where (f => folder.Id == f.Id).Count ()) {
                    folder.AsSyncMetaToClientExpected = false;
                    folder.Update ();
                }
                reloadedFolders.Add (folder);
            }
            BEContext.ProtoControl.SyncStrategy.ReportSyncResult (reloadedFolders);

            if (HadEmailMessageChanges) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
            }
            if (HadNewUnreadEmailMessageInInbox) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox));
            }
            if (HadContactChanges) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            }
            if (HadCalendarChanges) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            }
            if (FolderSyncIsMandated) {
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "SYNCREFSYNC0");
            } else if (BEContext.ProtoControl.SyncStrategy.IsMoreSyncNeeded ()) {
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "SYNCRESYNC0");
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
            foreach (var pending in PendingList) {
                pending.ResolveAsSuccess (BEContext.ProtoControl);
            }
            PendingList.Clear ();

            if (BEContext.ProtoControl.SyncStrategy.IsMoreSyncNeeded ()) {
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "SYNCRESYNC1");
            } else {
                return Event.Create ((uint)SmEvt.E.Success, "SYNCSUCCESS1");
            }
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
            // See if we are trying to do a bunch in parallel - if so go serial.
            var firstPending = PendingList.First ();
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
            ApplyStrategy ();
            return true;
        }
        // TODO - make this a generic extension.
        private bool ParseXmlBoolean (XElement bit)
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
                    switch (classCode) {
                    case Xml.AirSync.ClassCode.Contacts:
                        HadContactChanges = true;
                        ServerSaysAddContact (command, folder);
                        break;
                    case Xml.AirSync.ClassCode.Email:
                        HadEmailMessageChanges = true;
                        var emailMessage = ServerSaysAddEmail (command, folder);
                        if (null != emailMessage && Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == folder.Type &&
                            false == emailMessage.IsRead) {
                            HadNewUnreadEmailMessageInInbox = true;
                        }
                        break;
                    case Xml.AirSync.ClassCode.Calendar:
                        HadCalendarChanges = true;
                        ServerSaysAddCalendarItem (command, folder);
                        break;
                    default:
                        Log.Error ("AsSyncCommand ProcessCollectionCommands UNHANDLED class " + classCode);
                        break;
                    }
                    break;
                case Xml.AirSync.Change:
                    switch (classCode) {
                    case Xml.AirSync.ClassCode.Email:
                        ServerSaysChangeEmail (command, folder);
                        break;
                    case Xml.AirSync.ClassCode.Calendar:
                        ServerSaysChangeCalendarItem (command, folder);
                        break;
                    case Xml.AirSync.ClassCode.Contacts:
                        ServerSaysChangeContact (command, folder);
                        break;
                    default:
                        Log.Error ("AsSyncCommand ProcessCollectionCommands UNHANDLED class " + classCode);
                        break;
                    }
                    break;

                case Xml.AirSync.Delete:
                    var delServerId = command.Element (m_ns + Xml.AirSync.ServerId).Value;
                    switch (classCode) {
                    case Xml.AirSync.ClassCode.Email:
                        HadEmailMessageChanges = true;
                        var emailMessage = McFolderEntry.QueryByServerId<McEmailMessage> (BEContext.Account.Id, delServerId);
                        if (null != emailMessage) {
                            emailMessage.Delete ();
                        }
                        break;
                    case Xml.AirSync.ClassCode.Calendar:
                        HadCalendarChanges = true;
                        var cal = McFolderEntry.QueryByServerId<McCalendar> (BEContext.Account.Id, delServerId);
                        if (null != cal) {
                            cal.Delete ();
                        }
                        break;
                    case Xml.AirSync.ClassCode.Contacts:
                        HadContactChanges = true;
                        var contact = McFolderEntry.QueryByServerId<McContact> (BEContext.Account.Id, delServerId);
                        if (null != contact) {
                            contact.Delete ();
                        }
                        break;
                    default:
                        Log.Error ("AsSyncCommand ProcessCollectionCommands UNHANDLED class " + classCode);
                        break;
                    }
                    break;

                default:
                    Log.Error ("AsSyncCommand ProcessResponse UNHANDLED command " + command.Name.LocalName);
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
                    Log.Error ("AsSyncCommand ProcessCollectionResponses UNHANDLED response " + response.Name.LocalName);
                    break;
                }
            }
            // Because success is not reported in the response document.
            var pendingChanges = PendingList.Where (x => 
                x.State == McPending.StateEnum.Dispatched &&
                                 (x.Operation == McPending.Operations.CalUpdate ||
                                 x.Operation == McPending.Operations.ContactUpdate));
            foreach (var pendingChange in pendingChanges) {
                pendingChange.ResolveAsSuccess (BEContext.ProtoControl);
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
            var xmlStatus = xmlAdd.Element (m_ns + Xml.AirSync.Status);
            var status = (Xml.AirSync.StatusCode)uint.Parse (xmlStatus.Value);
            switch (status) {
            case Xml.AirSync.StatusCode.Success_1:
                PendingList.Remove (pending);
                pending.ResolveAsSuccess (BEContext.ProtoControl);
                success = true;
                break;

            case Xml.AirSync.StatusCode.ProtocolError_4:
            case Xml.AirSync.StatusCode.ClientError_6:
                PendingList.Remove (pending);
                pending.ResolveAsHardFail (BEContext.ProtoControl, 
                    NcResult.Error (NcResult.SubKindEnum.Error_ProtocolError));
                break;

            case Xml.AirSync.StatusCode.ServerWins_7:
                PendingList.Remove (pending);
                pending.ResolveAsHardFail (BEContext.ProtoControl,
                    NcResult.Error (NcResult.SubKindEnum.Error_ServerConflict));
                break;

            case Xml.AirSync.StatusCode.NoSpace_9:
                PendingList.Remove (pending);
                pending.ResolveAsUserBlocked (BEContext.ProtoControl,
                    McPending.BlockReasonEnum.UserRemediation,
                    NcResult.Error (NcResult.SubKindEnum.Error_NoSpace));
                break;

            case Xml.AirSync.StatusCode.LimitReWait_14:
                Log.Warn ("Received Sync Response status code LimitReWait_14, but we don't use HeartBeatInterval with Sync.");
                PendingList.Remove (pending);
                pending.ResolveAsSuccess (BEContext.ProtoControl);
                success = true;
                break;

            case Xml.AirSync.StatusCode.TooMany_15:
                var protocolState = BEContext.ProtoControl.ProtocolState;
                if (null != Limit) {
                    protocolState.AsSyncLimit = (uint)Limit;
                }
                PendingList.Remove (pending);
                pending.ResolveAsSuccess (BEContext.ProtoControl);
                success = true;
                break;

            default:
            case Xml.AirSync.StatusCode.NotFound_8:
                // Note: we don't send partial Sync requests.
            case Xml.AirSync.StatusCode.ResendFull_13:
                PendingList.Remove (pending);
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
            default:
                Log.Error ("AsSyncCommand ProcessCollectionResponses UNHANDLED class " + classCode);
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
                    var pending = McPending.QueryByServerId (folder.AccountId, serverId);
                    switch (status) {
                    case Xml.AirSync.StatusCode.ProtocolError_4:
                    case Xml.AirSync.StatusCode.ClientError_6:
                        PendingList.Remove (pending);
                        pending.ResolveAsHardFail (BEContext.ProtoControl, 
                            NcResult.Error (NcResult.SubKindEnum.Error_ProtocolError));
                        break;

                    case Xml.AirSync.StatusCode.ServerWins_7:
                        PendingList.Remove (pending);
                        pending.ResolveAsHardFail (BEContext.ProtoControl,
                            NcResult.Error (NcResult.SubKindEnum.Error_ServerConflict));
                        break;

                    case Xml.AirSync.StatusCode.NotFound_8:
                        folder.AsSyncMetaToClientExpected = true;
                        folder.Update ();
                        PendingList.Remove (pending);
                        pending.ResolveAsDeferred (BEContext.ProtoControl,
                            McPending.DeferredEnum.UntilSync,
                            NcResult.Error (NcResult.SubKindEnum.Error_ObjectNotFoundOnServer));
                        break;

                    case Xml.AirSync.StatusCode.NoSpace_9:
                        pending.ResolveAsUserBlocked (BEContext.ProtoControl,
                            McPending.BlockReasonEnum.UserRemediation,
                            NcResult.Error (NcResult.SubKindEnum.Error_NoSpace));
                        break;

                    case Xml.AirSync.StatusCode.LimitReWait_14:
                        Log.Warn ("Received Sync Response status code LimitReWait_14, but we don't use HeartBeatInterval with Sync.");
                        PendingList.Remove (pending);
                        pending.ResolveAsSuccess (BEContext.ProtoControl);
                        break;

                    case Xml.AirSync.StatusCode.TooMany_15:
                        var protocolState = BEContext.ProtoControl.ProtocolState;
                        if (null != Limit) {
                            protocolState.AsSyncLimit = (uint)Limit;
                            protocolState.Update ();
                        }
                        PendingList.Remove (pending);
                        pending.ResolveAsSuccess (BEContext.ProtoControl);
                        break;

                    default:
                        // Note: we don't send partial Sync requests.
                    case Xml.AirSync.StatusCode.ResendFull_13:
                        PendingList.Remove (pending);
                        pending.ResponsegXmlStatus = (uint)status;
                        pending.ResolveAsHardFail (BEContext.ProtoControl,
                            NcResult.Error (NcResult.SubKindEnum.Error_InappropriateStatus));
                        break;
                    }
                }
                return;
            default:
                Log.Error ("AsSyncCommand ProcessCollectionResponses UNHANDLED class " + classCode);
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
                return true;
            default:
                return false;
            }
        }
    }
}
