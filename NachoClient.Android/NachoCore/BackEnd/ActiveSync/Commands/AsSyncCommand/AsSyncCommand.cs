using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Wbxml;

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
        private bool HadDeletes;
        private bool FolderSyncIsMandated;
        private Nullable<uint> Limit;
        private List<SyncKit.PerFolder> SyncKitList;
        private XNamespace EmailNs;
        private XNamespace TasksNs;
        private int WindowSize;

        public TimeSpan WaitInterval { get; set; }

        private bool IsNarrow;
        public bool IsPinging { get; protected set; }

        public static XNamespace Ns = Xml.AirSync.Ns;

        public AsSyncCommand (IBEContext beContext, SyncKit syncKit)
            : base (Xml.AirSync.Sync, Xml.AirSync.Ns, beContext)
        {
            EmailNs = Xml.Email.Ns;
            TasksNs = Xml.Tasks.Ns;
            SuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_SyncSucceeded);
            FailureInd = NcResult.Error (NcResult.SubKindEnum.Error_SyncFailed);
            WindowSize = syncKit.OverallWindowSize;
            WaitInterval = syncKit.WaitInterval;
            IsNarrow = syncKit.IsNarrow;
            IsPinging = syncKit.IsPinging;
            SyncKitList = syncKit.PerFolders;
            FoldersInRequest = new List<McFolder> ();
            foreach (var perFolder in SyncKitList) {
                FoldersInRequest.Add (perFolder.Folder);
                PendingList.AddRange (perFolder.Commands);
            }
            NcModel.Instance.RunInTransaction (() => {
                foreach (var pending in PendingList) {
                    pending.MarkDispatched ();
                }
            });
        }

        public override double TimeoutInSeconds {
            get {
                // Add a 10-second fudge so that orderly timeout doesn't look like a network failure.
                if (TimeSpan.Zero == WaitInterval) {
                    return 0.0;
                } else {
                    return WaitInterval.TotalSeconds + 10;
                }
            }
        }

        private XElement ToEmailDelete (McPending pending)
        {
            return new XElement (m_ns + Xml.AirSync.Delete,
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId));
        }

        private XElement ToEmailMarkRead (McPending pending)
        {
            return new XElement (m_ns + Xml.AirSync.Change,
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                new XElement (m_ns + Xml.AirSync.ApplicationData,
                    new XElement (EmailNs + Xml.Email.Read, pending.EmailSetFlag_FlagType == McPending.MarkReadFlag ? "1" : "0")));
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
                McAbstrFolderEntry.ClassCodeEnum.Calendar) {
                add.Add (new XElement (m_ns + Xml.AirSync.Class, Xml.AirSync.ClassCode.Calendar));
            }
            add.Add (AsHelpers.ToXmlApplicationData (cal, BEContext));
            return add;
        }

        private XElement ToCalUpdate (McPending pending, McFolder folder)
        {
            var cal = McCalendar.QueryById<McCalendar> (pending.ItemId);
            return new XElement (m_ns + Xml.AirSync.Change, 
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                AsHelpers.ToXmlApplicationData (cal, BEContext, pending.CalUpdate_SendBody));
        }

        private XElement ToCalDelete (McPending pending, McFolder folder)
        {
            return new XElement (m_ns + Xml.AirSync.Delete,
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId));
        }

        private XElement ToContactCreate (McPending pending, McFolder folder)
        {
            var contact = McContact.QueryById<McContact> (pending.ItemId);
            var add = new XElement (m_ns + Xml.AirSync.Add, 
                          new XElement (m_ns + Xml.AirSync.ClientId, pending.ClientId));
            if (Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (folder.Type) !=
                McAbstrFolderEntry.ClassCodeEnum.Contact) {
                add.Add (new XElement (m_ns + Xml.AirSync.Class, Xml.AirSync.ClassCode.Contacts));
            }
            add.Add (contact.ToXmlApplicationData ());
            return add;
        }

        private XElement ToContactUpdate (McPending pending, McFolder folder)
        {
            var contact = McAbstrObject.QueryById<McContact> (pending.ItemId);
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
            var task = McAbstrObject.QueryById<McTask> (pending.ItemId);
            var add = new XElement (m_ns + Xml.AirSync.Add, 
                          new XElement (m_ns + Xml.AirSync.ClientId, pending.ClientId));
            if (Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (folder.Type) !=
                McAbstrFolderEntry.ClassCodeEnum.Tasks) {
                add.Add (new XElement (m_ns + Xml.AirSync.Class, Xml.AirSync.ClassCode.Tasks));
            }
            add.Add (task.ToXmlApplicationData ());
            return add;
        }

        private XElement ToTaskUpdate (McPending pending, McFolder folder)
        {
            var task = McAbstrObject.QueryById<McTask> (pending.ItemId);
            return new XElement (m_ns + Xml.AirSync.Change, 
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                task.ToXmlApplicationData ());
        }

        private XElement ToTaskDelete (McPending pending, McFolder folder)
        {
            return new XElement (m_ns + Xml.AirSync.Delete,
                new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId));
        }

        private XElement MimeSupportElement (Xml.AirSync.MimeSupportCode mimeSupport)
        {
            return new XElement (m_ns + Xml.AirSync.MimeSupport, (uint)mimeSupport);
        }

        private XElement BodyPreferenceElement (Xml.AirSync.TypeCode bodyType)
        {
            return new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                new XElement (m_baseNs + Xml.AirSyncBase.Type, (uint)bodyType),
                new XElement (m_baseNs + Xml.AirSyncBase.TruncationSize, "100000000"));
        }

        private uint WaitIntervalToWaitMinutes ()
        {
            return Math.Min (Math.Max (0, (uint) WaitInterval.TotalMinutes), 59);
        }

        protected override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var collections = new XElement (m_ns + Xml.AirSync.Collections);
            foreach (var perFolder in SyncKitList) {
                var folder = perFolder.Folder;
                var pendingSubList = perFolder.Commands;
                var collection = new XElement (m_ns + Xml.AirSync.Collection,
                                     new XElement (m_ns + Xml.AirSync.SyncKey, folder.AsSyncKey),
                                     new XElement (m_ns + Xml.AirSync.CollectionId, folder.ServerId));
                // GetChanges.
                if (perFolder.GetChanges && McFolder.AsSyncKey_Initial != folder.AsSyncKey) {
                    collection.Add (new XElement (m_ns + Xml.AirSync.GetChanges));
                    collection.Add (new XElement (m_ns + Xml.AirSync.WindowSize, perFolder.WindowSize));
                
                    // WindowSize.
                    // Options.
                    var classCodeEnum = Xml.FolderHierarchy.TypeCodeToAirSyncClassCodeEnum (folder.Type);
                    var options = new XElement (m_ns + Xml.AirSync.Options);
                    switch (classCodeEnum) {
                    case McAbstrFolderEntry.ClassCodeEnum.Email:
                        options.Add (new XElement (m_ns + Xml.AirSync.FilterType, (uint)perFolder.FilterCode));
                        options.Add (MimeSupportElement (Xml.AirSync.MimeSupportCode.NoMime_0));
                        // Some servers support a preview option, but that is limited by the spec to 255 bytes.
                        // The app wants more than that, so it can have some useful text left after stripping
                        // away all the junk.  For all servers, ask for a plain text body truncated to 500 bytes.
                        options.Add (new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                            new XElement (m_baseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.PlainText_1),
                            new XElement (m_baseNs + Xml.AirSyncBase.TruncationSize, "500")));
                        break;

                    case McAbstrFolderEntry.ClassCodeEnum.Calendar:
                        options.Add (new XElement (m_ns + Xml.AirSync.FilterType, (uint)perFolder.FilterCode));
                        if (BEContext.Server.HostIsAsGMail () || BEContext.Server.HostIsAsHotMail ()) {
                            // GFE will only give us plain text, no matter what we ask for.
                            // Hotmail will give us anything except MIME, but the HTML and RTF
                            // will be unformatted.  So we may as well just ask for plain text.
                            options.Add (MimeSupportElement (Xml.AirSync.MimeSupportCode.NoMime_0));
                            options.Add (BodyPreferenceElement (Xml.AirSync.TypeCode.PlainText_1));
                        } else if (14.0 > Convert.ToDouble (BEContext.ProtocolState.AsProtocolVersion, System.Globalization.CultureInfo.InvariantCulture)) {
                            // Exchange 2007 will fail if we ask for MIME.  But it can handle
                            // any other format.  So ask for either HTML or plain text, with a
                            // preference for HTML
                            options.Add (MimeSupportElement (Xml.AirSync.MimeSupportCode.NoMime_0));
                            options.Add (BodyPreferenceElement (Xml.AirSync.TypeCode.Html_2));
                            options.Add (BodyPreferenceElement (Xml.AirSync.TypeCode.PlainText_1));
                        } else {
                            // The others, Exchange 2010 and Office365, will give us MIME.
                            // MIME is not our preferred format, but it is the only way to get attachments.
                            options.Add (MimeSupportElement (Xml.AirSync.MimeSupportCode.AllMime_2));
                            options.Add (BodyPreferenceElement (Xml.AirSync.TypeCode.Mime_4));
                        }
                        break;

                    case McAbstrFolderEntry.ClassCodeEnum.Contact:
                        if (Xml.FolderHierarchy.TypeCode.Ric_19 == folder.Type) {
                            // Expressing BodyPreference for RIC gets Protocol Error.
                            if (14.0 <= Convert.ToDouble (BEContext.ProtocolState.AsProtocolVersion, System.Globalization.CultureInfo.InvariantCulture)) {
                                options.Add (new XElement (m_ns + Xml.AirSync.MaxItems, "200"));
                            }
                        } else {
                            options.Add (BodyPreferenceElement (Xml.AirSync.TypeCode.PlainText_1));
                        }
                        break;

                    case McAbstrFolderEntry.ClassCodeEnum.Tasks:
                        options.Add (BodyPreferenceElement (Xml.AirSync.TypeCode.PlainText_1));
                        break;
                    }
                    if (options.HasElements) {
                        collection.Add (options);
                    }
                } else if (McFolder.AsSyncKey_Initial != folder.AsSyncKey && BEContext.Server.HostIsAsGMail ()) {
                    // If we perform a Sync-based command and don't include Options + FilterType, GFE
                    // Will go into a MoreAvailable=1 w/no changes tailspin until a new message arrives.
                    collection.Add (new XElement (m_ns + Xml.AirSync.Options,
                        new XElement (m_ns + Xml.AirSync.FilterType, (uint)perFolder.FilterCode)));
                }
                // Commands.  
                bool addedHardDeleteTag = false;
                var commands = new XElement (m_ns + Xml.AirSync.Commands);
                foreach (var pending in pendingSubList) {
                    switch (pending.Operation) {
                    case McPending.Operations.EmailDelete:
                        // Looks like we only do delete when it is a hard delete - see NcProtoControl:DeleteEmailCmd (456)
                        if (! addedHardDeleteTag) {
                            collection.Add (new XElement (m_ns + Xml.AirSync.DeletesAsMoves, 0));
                            addedHardDeleteTag = true;
                        }
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
                    case McPending.Operations.Sync:
                        // we don't express this.
                        break;
                    default:
                        NcAssert.CaseError (pending.Operation.ToString ());
                        break;
                    }
                }
                if (commands.HasElements) {
                    collection.Add (commands);
                }
                collections.Add (collection);
            }
            var sync = new XElement (m_ns + Xml.AirSync.Sync, collections);
            // use Wait instead of HeartbeatInterval since Wait is also supported in 12.1 while HeartbeatInterval is not 
            if (TimeSpan.Zero != WaitInterval) {
                sync.Add (new XElement (m_ns + Xml.AirSync.Wait, WaitIntervalToWaitMinutes ())); 
            }
            sync.Add (new XElement (m_ns + Xml.AirSync.WindowSize, WindowSize));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (sync);
            return doc;
        }

        public override Event ProcessTopLevelStatus (AsHttpOperation sender, uint status, XDocument doc)
        {
            var globEvent = base.ProcessTopLevelStatus (sender, status, doc);
            if (null != globEvent) {
                return globEvent;
            }
            switch ((Xml.AirSync.StatusCode)status) {
            case Xml.AirSync.StatusCode.Success_1:
                return null;

            case Xml.AirSync.StatusCode.SyncKeyInvalid_3:
                Log.Warn (Log.LOG_AS, "{0}: Status: SyncKeyInvalid_3", CmdNameWithAccount);
                // FoldersInRequest is NOT stale here.
                foreach (var folder in FoldersInRequest) {
                    folder.UpdateResetSyncState ();
                }
                ResolveAllDeferred ();
                // Overkill, but ensure that UI knows that the rug was pulled from under it.
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_TaskSetChanged));
                return Event.Create ((uint)SmEvt.E.HardFail, "ASYNCTOPFOOF");

            case Xml.AirSync.StatusCode.ProtocolError_4:
                Log.Warn (Log.LOG_AS, "{0}: Status: ProtocolError_4", CmdNameWithAccount);
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
                Log.Warn (Log.LOG_AS, "{0}: Status: ServerError_5", CmdNameWithAccount);
                // TODO: detect a loop, and reset folder state if looping.
                ResolveAllDeferred ();
                return Event.Create ((uint)SmEvt.E.TempFail, "ASYNCTOPRS");

            case Xml.AirSync.StatusCode.FolderChange_12:
                Log.Warn (Log.LOG_AS, "{0}: Status: FolderChange_12", CmdNameWithAccount);
                ResolveAllDeferred ();
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "ASYNCTOPRFS");

            case Xml.AirSync.StatusCode.TooMany_15:
                Log.Warn (Log.LOG_AS, "{0}: Status: TooMany_15", CmdNameWithAccount);
                var xmlLimit = doc.Root.ElementAnyNs (Xml.AirSync.Limit);
                if (null != xmlLimit && null != xmlLimit.Value) {
                    try {
                        var limit = uint.Parse (xmlLimit.Value);
                        var protocolState = BEContext.ProtoControl.ProtocolState;
                        protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                            var target = (McProtocolState)record;
                            target.AsSyncLimit = limit;
                            return true;
                        });
                    } catch (Exception ex) {
                        Log.Error (Log.LOG_AS, "{0}: exception parsing Limit: {1}", CmdNameWithAccount, ex.ToString ());
                    }
                } else {
                    Log.Error (Log.LOG_AS, "{0}: TLS TooMany_15 w/out Limit", CmdNameWithAccount);
                }
                ResolveAllDeferred ();
                return Event.Create ((uint)SmEvt.E.TempFail, "ASYNCTOPTM");

            case Xml.AirSync.StatusCode.Retry_16:
                Log.Warn (Log.LOG_AS, "{0}: Status: Retry_16", CmdNameWithAccount);
                ResolveAllDeferred ();
                return Event.Create ((uint)SmEvt.E.TempFail, "ASYNCTOPRRR");

            default:
                Log.Error (Log.LOG_AS, "{0}: ProcessResponse UNHANDLED Top Level status: {1}", CmdNameWithAccount, status);
                return null;
            }
        }

        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken)
        {
            if (!SiezePendingCleanup ()) {
                return Event.Create ((uint)SmEvt.E.TempFail, "SYNCCANCEL0");
            }
            List<McFolder> SawMoreAvailableNoCommands = new List<McFolder> ();
            bool SawCommandsInAnyFolder = false;
            bool HasBeenCancelled = false;
            List<McFolder> processedFolders = new List<McFolder> ();
            var xmlLimit = doc.Root.Element (m_ns + Xml.AirSync.Limit);
            if (null != xmlLimit) {
                Limit = uint.Parse (xmlLimit.Value);
            }
            // ProcessTopLevelStatus will handle Status element, if  included.
            // If we get here, we know any TL Status is okay.
            //
            // Is this the right place for the following?
            if (IsPinging) {
                MarkFoldersPinged ();
            }
            var xmlCollections = doc.Root.Element (m_ns + Xml.AirSync.Collections);
            if (null == xmlCollections) {
                return Event.Create ((uint)SmEvt.E.Success, "SYNCSUCCODD");
            }
            var collections = xmlCollections.Elements (m_ns + Xml.AirSync.Collection);
            // Note: we may get back zero Collection items.
            foreach (var collection in collections) {
                // Check for cancellation at the start of processing a folder for consistency in processing each folder.
                // Even if we have been cancelled, we still process command-responses and status of pending to-server ops.
                HasBeenCancelled = cToken.IsCancellationRequested;
                List<McPending> pendingInFolder;
                // Note: CollectionId, Status and SyncKey are required to be present.
                var serverId = collection.Element (m_ns + Xml.AirSync.CollectionId).Value;
                var folder = McFolder.ServerEndQueryByServerId (AccountId, serverId);
                var oldSyncKey = folder.AsSyncKey;
                var xmlSyncKey = collection.Element (m_ns + Xml.AirSync.SyncKey);
                var xmlMoreAvailable = collection.Element (m_ns + Xml.AirSync.MoreAvailable);
                var xmlCommands = collection.Element (m_ns + Xml.AirSync.Commands);
                if (null == xmlCommands) {
                    if (null != xmlMoreAvailable) {
                        SawMoreAvailableNoCommands.Add (folder);
                    }
                } else {
                    SawCommandsInAnyFolder = true;
                }
                var xmlStatus = collection.Element (m_ns + Xml.AirSync.Status);
                if (HasBeenCancelled) {
                    Log.Info (Log.LOG_HTTP, "{0}: Bypassing folder update and commands due to cancellation: {1}", CmdNameWithAccount, NcXmlFilterState.ShortHash (folder.ServerId));
                } else {
                    // The protocol requires SyncKey, but GOOG does not obey in the StatusCode.NotFound case.
                    // If we have been cancelled, don't advance sync state for the folder.
                    if (null != xmlSyncKey) {
                        var now = DateTime.UtcNow;
                        folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                            var target = (McFolder)record;
                            target.AsSyncKey = xmlSyncKey.Value;
                            target.AsSyncMetaToClientExpected = (McFolder.AsSyncKey_Initial == oldSyncKey) || (null != xmlMoreAvailable);
                            if (null != xmlCommands) {
                                target.HasSeenServerCommand = true;
                            }
                            target.SyncAttemptCount += 1;
                            target.LastSyncAttempt = now;
                            return true;
                        });
                    } else {
                        Log.Warn (Log.LOG_SYNC, "{0}: SyncKey missing from XML.", CmdNameWithAccount);
                    }
                    Log.Info (Log.LOG_SYNC, "{0}: MoreAvailable presence {1}", CmdNameWithAccount, (null != xmlMoreAvailable));
                    Log.Info (Log.LOG_SYNC, "{0}: Folder:{1}, Old SyncKey:{2}, New SyncKey:{3}", CmdNameWithAccount, NcXmlFilterState.ShortHash (folder.ServerId), oldSyncKey, folder.AsSyncKey);
                }
                processedFolders.Add (folder);
                var status = (Xml.AirSync.StatusCode)uint.Parse (xmlStatus.Value);
                switch (status) {
                case Xml.AirSync.StatusCode.Success_1:
                    if (0 != folder.AsSyncFailRun) {
                        folder = folder.UpdateReset_AsSyncFailRun ();
                    }
                    var xmlResponses = collection.Element (m_ns + Xml.AirSync.Responses);
                    ProcessCollectionResponses (folder, xmlResponses);
                    if (!HasBeenCancelled) {
                        if (null != xmlCommands && xmlCommands.Elements ().Any ()) {
                            using (NcAbate.BackEndAbatement ()) {
                                ProcessCollectionCommands (folder, xmlCommands);
                            }
                        }
                    }
                    lock (PendingResolveLockObj) {
                        // Any pending not already resolved gets resolved as Success.
                        pendingInFolder = PendingList.Where (x => 
                            x.ParentId == folder.ServerId ||
                            (x.ServerId == folder.ServerId && x.Operation == McPending.Operations.Sync)
                        ).ToList ();
                        foreach (var pending in pendingInFolder) {
                            PendingList.Remove (pending);
                            pending.ResolveAsSuccess (BEContext.ProtoControl);
                        }
                    }
                    if (!HasBeenCancelled) {
                        if (Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == folder.Type) {
                            var protocolState = BEContext.ProtocolState;
                            if (!protocolState.HasSyncedInbox) {
                                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                                    var target = (McProtocolState)record;
                                    target.HasSyncedInbox = true;
                                    return true;
                                });
                            }
                        }
                        // If we have been cancelled, this sync can't cause an epoch scrub.
                        if (folder.AsSyncEpochScrubNeeded && !folder.AsSyncMetaToClientExpected) {
                            folder.PerformSyncEpochScrub ();
                        }
                    }
                    break;

                case Xml.AirSync.StatusCode.SyncKeyInvalid_3:
                case Xml.AirSync.StatusCode.ServerError_5:
                    Log.Warn (Log.LOG_AS, "{0}: Status: {1}", CmdNameWithAccount, status.ToString ());
                    if (4 > folder.AsSyncFailRun) {
                        // Let the scrubber re-enable.
                        folder = folder.UpdateIncrement_AsSyncFailRunToClientExpected (false);
                        Log.Warn (Log.LOG_AS, "{0}: AsSyncFailRun {1}", CmdNameWithAccount, folder.AsSyncFailRun);
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
                    } else {
                        // Initiate re-sync of folder.
                        Log.Error (Log.LOG_AS, "{0}: UpdateResetSyncState after AsSyncFailRun {1}", CmdNameWithAccount, folder.AsSyncFailRun);
                        folder = folder.UpdateReset_AsSyncFailRun ();
                        folder = folder.UpdateResetSyncState ();
                        // Overkill, but ensure that UI knows that the rug was pulled from under it.
                        HadEmailMessageSetChanges = true;
                        HadContactSetChanges = true;
                        HadCalendarSetChanges = true;
                        HadTaskChanges = true;
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
                    }
                    break;

                case Xml.AirSync.StatusCode.ProtocolError_4:
                    Log.Warn (Log.LOG_AS, "{0}: Status: ProtocolError_4", CmdNameWithAccount);
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
                    Log.Warn (Log.LOG_AS, "{0}: Status: FolderChange_12", CmdNameWithAccount);
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
                    Log.Warn (Log.LOG_AS, "{0}: Status: Retry_16", CmdNameWithAccount);
                    folder = folder.UpdateSet_AsSyncMetaToClientExpected (true);
                    lock (PendingResolveLockObj) {
                        pendingInFolder = PendingList.Where (x => x.ParentId == folder.ServerId).ToList ();
                        foreach (var pending in pendingInFolder) {
                            PendingList.Remove (pending);
                            pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                        }
                    }
                    break;

                default:
                    Log.Error (Log.LOG_AS, "{0}: ProcessResponse UNHANDLED Collection status: {1}", CmdNameWithAccount, status);
                    break;
                }
            }

            // If we're getting MoreAvailable with NO Command(s) for the entire Sync, this may be an Error.
            if (0 != SawMoreAvailableNoCommands.Count && !SawCommandsInAnyFolder) {
                foreach (var errFolder in SawMoreAvailableNoCommands) {
                    // We've seen this be innocuous in Office365.
                    // http://localhost:8000/bugfix/alpha/logs/us-east-1:d4d26796-9ff3-4d36-bfee-f7e4138c0237/2015-02-20T01:11:29.955Z/1/
                    Log.Warn (Log.LOG_AS, "{0}: MoreAvailable with no commands in folder ServerId {1}.", CmdNameWithAccount, errFolder.ServerId);
                }
            }

            // For any folders missing from the response, we need to note that there isn't more on the server-side.
            // Remember the loop above re-writes folders, so FoldersInRequest object will be stale!
            foreach (var maybeStale in FoldersInRequest) {
                var folder = McFolder.ServerEndQueryById (maybeStale.Id);
                if (0 == processedFolders.Where (f => folder.Id == f.Id).Count ()) {
                    // This is a grey area in the spec. I've seen HotMail exclude a folder from a response where there IS more waiting on the server.
                    // This was the old code:
                    // folder = folder.UpdateSet_AsSyncMetaToClientExpected (false);
                    // I suspect it may have been driven by GOOG doing the opposite - omitting when there is nothing. We will have to test and see.
                    Log.Info (Log.LOG_AS, "{0}: McFolder {1} not included in Sync response.", CmdNameWithAccount, folder.ServerId);
                }
            }
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
            if (HadDeletes) {
                // We know that there will be updates to Deleted folder.
                var deletedFolder = McFolder.GetDefaultDeletedFolder (AccountId);
                if (null != deletedFolder) {
                    deletedFolder.UpdateSet_AsSyncMetaToClientExpected (true);
                }
            }
            if (FolderSyncIsMandated) {
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "SYNCREFSYNC0");
            } else {
                return SuccessEvent ("SYNCSUCCESS0");
            }
        }

        private Event SuccessEvent (string mnemonic)
        {
            if (IsNarrow) {
                var protocolState = BEContext.ProtocolState;
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.LastNarrowSync = DateTime.UtcNow;
                    return true;
                });
            }
            return Event.Create ((uint)SmEvt.E.Success, mnemonic);
        }

        // Called when we get an empty Sync response body.
        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, CancellationToken cToken)
        {
            if (!SiezePendingCleanup ()) {
                return Event.Create ((uint)SmEvt.E.TempFail, "SYNCCANCEL1");
            }
            // Is this the right place for this?
            if (IsPinging) {
                MarkFoldersPinged ();
            }
            // FoldersInRequest NOT stale here.
            var now = DateTime.UtcNow;
            foreach (var iterFolder in FoldersInRequest) {
                iterFolder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.AsSyncMetaToClientExpected = false;
                    target.SyncAttemptCount += 1;
                    target.LastSyncAttempt = now;
                    return true;
                });
            }
            lock (PendingResolveLockObj) {
                ProcessImplicitResponses (PendingList);
            }
            return SuccessEvent ("SYNCSUCCESS1");
        }

        public override void StatusInd (bool didSucceed)
        {
            if (didSucceed) {
                McPending.MakeEligibleOnSync (AccountId);
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
                foreach (var iter in PendingList) {
                    var pending = iter.UpdateWithOCApply<McPending> ((record) => {
                        var target = (McPending)record;
                        target.DeferredSerialIssueOnly = true;
                        return true;
                    });
                    if (pending == firstPending) {
                        continue;
                    }
                    pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                }
                PendingList.Clear ();
            }
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
            var commands = xmlCommands.Elements ();
            foreach (var command in commands) {
                var classCode = GetClassCode (command, folder);
                switch (command.Name.LocalName) {
                case Xml.AirSync.Add:
                    var addServerId = command.Element (m_ns + Xml.AirSync.ServerId).Value;
                    Log.Debug (Log.LOG_AS, "{0}: Command Add {1} ServerId {2}", CmdNameWithAccount, classCode, addServerId);
                    var pathElem = new McPath (AccountId);
                    pathElem.ServerId = addServerId;
                    pathElem.ParentId = folder.ServerId;
                    NcModel.Instance.RunInTransaction (() => {
                        pathElem.Insert (BEContext.Server.HostIsAsGMail ());
                        var applyAdd = new ApplyItemAdd (AccountId) {
                            ClassCode = classCode,
                            ServerId = addServerId,
                            XmlCommand = command,
                            Folder = folder,
                        };
                        applyAdd.ProcessServerCommand ();
                    });
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
                    Log.Info (Log.LOG_AS, "{0}: Command Change {1} ServerId {2}", CmdNameWithAccount, classCode, chgServerId);
                    var applyChange = new ApplyItemChange (AccountId) {
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
                        Log.Error (Log.LOG_AS, "{0}: ProcessCollectionCommands UNHANDLED class {1}", CmdNameWithAccount, classCode);
                        break;
                    }
                    break;

                case Xml.AirSync.Delete:
                case Xml.AirSync.SoftDelete:
                    var delServerId = command.Element (m_ns + Xml.AirSync.ServerId).Value;
                    Log.Info (Log.LOG_AS, "{0}: Command (Soft)Delete {1} ServerId {2}", CmdNameWithAccount, classCode, delServerId);
                    NcModel.Instance.RunInTransaction (() => {
                        pathElem = McPath.QueryByServerId (AccountId, delServerId);
                        if (null != pathElem) {
                            pathElem.Delete ();
                        } else {
                            Log.Info (Log.LOG_AS, "{0}: McPath for Command {1}, ServerId {2} not in DB - may have been subject of MoveItems.", CmdNameWithAccount, command.Name.LocalName, delServerId);
                        }
                        var applyDelete = new ApplyItemDelete (AccountId) {
                            ClassCode = classCode,
                            ServerId = delServerId,
                        };
                        applyDelete.ProcessServerCommand ();
                    });
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
                        Log.Error (Log.LOG_AS, "{0}: ProcessCollectionCommands UNHANDLED class {1}", CmdNameWithAccount, classCode);
                        break;
                    }
                    break;

                default:
                    Log.Error (Log.LOG_AS, "{0}: ProcessResponse UNHANDLED command {1}", CmdNameWithAccount, command.Name.LocalName);
                    break;
                }
            }
        }

        private void ProcessCollectionResponses (McFolder folder, XElement xmlResponses)
        {
            if (null != xmlResponses) {
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
                        Log.Error (Log.LOG_AS, "{0}: ProcessCollectionResponses UNHANDLED response {1}", CmdNameWithAccount, response.Name.LocalName);
                        break;
                    }
                }
            }
            // Because success is not reported in the response document for some commands.
            lock (PendingResolveLockObj) {
                var noResponsePendings = PendingList.Where (x => x.ParentId == folder.ServerId);
                ProcessImplicitResponses (noResponsePendings);
            }
        }

        // The list MUST be a subset of the OBJECTs in the PendingList.
        private void ProcessImplicitResponses (IEnumerable<McPending> fromPendingList)
        {
            Log.Info (Log.LOG_AS, "{0}: ProcessImplicitResponses: Start", CmdNameWithAccount);
            var cachedFromPendingList = fromPendingList.ToList ();
            foreach (var pending in cachedFromPendingList) {
                // For Adds we need to add to McPath, and for Deletes we need to delete from McPath.
                if (IsSyncAddCommand (pending.Operation)) {
                    Log.Error (Log.LOG_AS, "{0}: ProcessImplicitResponses: Add command did not receive response.", CmdNameWithAccount);
                }
                if (IsSyncDeleteCommand (pending.Operation)) {
                    HadDeletes = true;
                    var pathElem = McPath.QueryByServerId (pending.AccountId, pending.ServerId);
                    if (null == pathElem) {
                        Log.Error (Log.LOG_AS, "{0}: ProcessImplicitResponses: McPath entry missing for Delete of {1}", CmdNameWithAccount, pending.ServerId);
                    } else {
                        NcModel.Instance.RunInTransaction (() => {
                            pathElem.Delete ();
                        });
                    }
                }
                // user-directed sync responses get processed here too.
                pending.ResolveAsSuccess (BEContext.ProtoControl);
                PendingList.RemoveAll (x => pending.Id == x.Id);
            }
            Log.Info (Log.LOG_AS, "{0}: ProcessImplicitResponses: Finished", CmdNameWithAccount);
        }

        private void ProcessCollectionAddResponse (McFolder folder, XElement xmlAdd, string classCode)
        {
            McPending pending;
            McAbstrItem item;
            bool success = false;

            // Status and ClientId are required to be present.
            var xmlClientId = xmlAdd.Element (m_ns + Xml.AirSync.ClientId);
            var clientId = xmlClientId.Value;
            XElement xmlServerId = null;
            lock (PendingResolveLockObj) {
                pending = McPending.QueryByClientId (folder.AccountId, clientId); 
                if (null == pending) {
                    Log.Error (Log.LOG_AS, "{0}: ProcessCollectionAddResponse: could not find McPending with ClientId of {1}.",
                        CmdNameWithAccount, clientId);
                    return;
                }
                var xmlStatus = xmlAdd.Element (m_ns + Xml.AirSync.Status);
                var status = (Xml.AirSync.StatusCode)uint.Parse (xmlStatus.Value);
                pending.ResponsegXmlStatus = (uint)status;
                switch (status) {
                case Xml.AirSync.StatusCode.Success_1:
                    PendingList.RemoveAll (x => x.Id == pending.Id);
                    pending.ResolveAsSuccess (BEContext.ProtoControl);
                    success = true;
                    break;

                case Xml.AirSync.StatusCode.ProtocolError_4:
                case Xml.AirSync.StatusCode.ClientError_6:
                    Log.Warn (Log.LOG_AS, "{0}: Status: {1}", CmdNameWithAccount, status);
                    PendingList.RemoveAll (x => x.Id == pending.Id);
                    pending.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_ProtocolError));
                    break;

                case Xml.AirSync.StatusCode.ServerError_5:
                    // HotMail will send this inside a command response, along with the ServerId!
                    // In this case, this is actually a successful add.
                    PendingList.RemoveAll (x => x.Id == pending.Id);
                    xmlServerId = xmlAdd.Element (m_ns + Xml.AirSync.ServerId);
                    if (null != xmlServerId && null != xmlServerId.Value) {
                        Log.Warn (Log.LOG_AS, "{0}: Status: ServerError_5 w/ServerId", CmdNameWithAccount);
                        pending.ResolveAsSuccess (BEContext.ProtoControl);
                        success = true;
                    } else {
                        Log.Warn (Log.LOG_AS, "{0}: Status: ServerError_5", CmdNameWithAccount);
                        pending.ResolveAsDeferred (BEContext.ProtoControl, DateTime.UtcNow,
                            NcResult.Error (NcResult.SubKindEnum.Info_ServiceUnavailable));
                    }
                    break;

                case Xml.AirSync.StatusCode.ServerWins_7:
                    Log.Warn (Log.LOG_AS, "{0}: Status: ServerWins_7", CmdNameWithAccount);
                    PendingList.RemoveAll (x => x.Id == pending.Id);
                    pending.ResolveAsHardFail (BEContext.ProtoControl,
                        NcResult.Error (NcResult.SubKindEnum.Error_ServerConflict));
                    break;

                case Xml.AirSync.StatusCode.NoSpace_9:
                    Log.Warn (Log.LOG_AS, "{0}: Status: NoSpace_9", CmdNameWithAccount);
                    PendingList.RemoveAll (x => x.Id == pending.Id);
                    pending.ResolveAsUserBlocked (BEContext.ProtoControl,
                        McPending.BlockReasonEnum.UserRemediation,
                        NcResult.Error (NcResult.SubKindEnum.Error_NoSpace));
                    break;

                case Xml.AirSync.StatusCode.LimitReWait_14:
                    Log.Warn (Log.LOG_AS, "{0}: Status: LimitReWait_14", CmdNameWithAccount);
                    Log.Warn (Log.LOG_AS, "Received Sync Response status code LimitReWait_14, but we don't use HeartBeatInterval with Sync.");
                    PendingList.RemoveAll (x => x.Id == pending.Id);
                    pending.ResolveAsSuccess (BEContext.ProtoControl);
                    success = true;
                    break;

                case Xml.AirSync.StatusCode.TooMany_15:
                    Log.Warn (Log.LOG_AS, "{0}: Status: TooMany_15", CmdNameWithAccount);
                    var protocolState = BEContext.ProtoControl.ProtocolState;
                    if (null != Limit) {
                        protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                            var target = (McProtocolState)record;
                            target.AsSyncLimit = (uint)Limit;
                            return true;
                        });
                    }
                    PendingList.RemoveAll (x => x.Id == pending.Id);
                    pending.ResolveAsSuccess (BEContext.ProtoControl);
                    success = true;
                    break;

                default:
                case Xml.AirSync.StatusCode.NotFound_8:
                // Note: we don't send partial Sync requests.
                case Xml.AirSync.StatusCode.ResendFull_13:
                    Log.Warn (Log.LOG_AS, "{0}: Status: {1}", CmdNameWithAccount, status);
                    PendingList.RemoveAll (x => x.Id == pending.Id);
                    pending.ResolveAsHardFail (BEContext.ProtoControl,
                        NcResult.Error (NcResult.SubKindEnum.Error_InappropriateStatus));
                    break;
                }
            }
            if (!success) {
                return;
            }

            switch (classCode) {
            case Xml.AirSync.ClassCode.Email:
                item = McAbstrItem.QueryByClientId<McEmailMessage> (AccountId, clientId);
                break;
            case Xml.AirSync.ClassCode.Contacts:
                item = McAbstrItem.QueryByClientId<McContact> (AccountId, clientId);
                break;
            case Xml.AirSync.ClassCode.Calendar:
                item = McAbstrItem.QueryByClientId<McCalendar> (AccountId, clientId);
                break;
            case Xml.AirSync.ClassCode.Tasks:
                item = McAbstrItem.QueryByClientId<McTask> (AccountId, clientId);
                break;
            default:
                Log.Error (Log.LOG_AS, "{0} ProcessCollectionResponses UNHANDLED class {1}", CmdNameWithAccount, classCode);
                return;
            }
            if (null == item) {
                Log.Warn (Log.LOG_AS, "{0}: item not found ClientId {1}", CmdNameWithAccount, clientId);
                return;
            }
            xmlServerId = xmlAdd.Element (m_ns + Xml.AirSync.ServerId);
            var serverId = xmlServerId.Value;
            if (null == serverId) {
                Log.Error (Log.LOG_AS, "{0}: Add command response without ServerId.", CmdNameWithAccount);
            } else {
                var pathElem = new McPath (AccountId) {
                    ServerId = serverId,
                    ParentId = folder.ServerId,
                };
                NcModel.Instance.RunInTransaction (() => {
                    pathElem.Insert ();
                    if (item is McEmailMessage) {
                        item = item.UpdateWithOCApply<McEmailMessage> ((record) => {
                            var target = (McEmailMessage)record;
                            target.ServerId = serverId;
                            return true;
                        });
                    } else {
                        item.ServerId = serverId;
                        item.Update ();
                    }
                });
            }
        }

        private void ProcessCollectionChangeResponse (McFolder folder, XElement xmlChange, string classCode)
        {
            // Note: we are only supposed to see Change in this context if there was a failure.
            // HotMail didn't read that memo.
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
                    lock (PendingResolveLockObj) {
                        var pending = McPending.QueryByServerId (folder.AccountId, serverId).FirstOrDefault ();
                        if (null == pending) {
                            Log.Error (Log.LOG_AS, "{0}: ProcessCollectionChangeResponse: could not find McPending with ServerId of {1}.",
                                CmdNameWithAccount, serverId);
                            return;
                        }
                        switch (status) {
                        case Xml.AirSync.StatusCode.Success_1:
                            // Let implicit responses code take care of it (HotMail).
                            break;

                        case Xml.AirSync.StatusCode.ProtocolError_4:
                        case Xml.AirSync.StatusCode.ClientError_6:
                            Log.Warn (Log.LOG_AS, "{0}: Status: {1}", CmdNameWithAccount, status);
                            PendingList.RemoveAll (x => x.Id == pending.Id);
                            pending.ResolveAsHardFail (BEContext.ProtoControl, 
                                NcResult.Error (NcResult.SubKindEnum.Error_ProtocolError));
                            break;

                        case Xml.AirSync.StatusCode.ServerWins_7:
                            Log.Warn (Log.LOG_AS, "{0}: Status: ServerWins_7", CmdNameWithAccount);
                            PendingList.RemoveAll (x => x.Id == pending.Id);
                            pending.ResolveAsHardFail (BEContext.ProtoControl,
                                NcResult.Error (NcResult.SubKindEnum.Error_ServerConflict));
                            break;

                        case Xml.AirSync.StatusCode.NotFound_8:
                            Log.Warn (Log.LOG_AS, "{0}: Status: NotFound_8", CmdNameWithAccount);
                            folder.UpdateSet_AsSyncMetaToClientExpected (true);
                            PendingList.RemoveAll (x => x.Id == pending.Id);
                            pending.ResolveAsDeferred (BEContext.ProtoControl,
                                McPending.DeferredEnum.UntilSync,
                                NcResult.Error (NcResult.SubKindEnum.Error_ObjectNotFoundOnServer));
                            break;

                        case Xml.AirSync.StatusCode.NoSpace_9:
                            Log.Warn (Log.LOG_AS, "{0}: Status: NoSpace_9", CmdNameWithAccount);
                            PendingList.RemoveAll (x => x.Id == pending.Id);
                            pending.ResolveAsUserBlocked (BEContext.ProtoControl,
                                McPending.BlockReasonEnum.UserRemediation,
                                NcResult.Error (NcResult.SubKindEnum.Error_NoSpace));
                            break;

                        case Xml.AirSync.StatusCode.LimitReWait_14:
                            Log.Warn (Log.LOG_AS, "{0}: Received Sync Response status code LimitReWait_14, but we don't use HeartBeatInterval with Sync.", CmdNameWithAccount);
                            PendingList.RemoveAll (x => x.Id == pending.Id);
                            pending.ResolveAsSuccess (BEContext.ProtoControl);
                            break;

                        case Xml.AirSync.StatusCode.TooMany_15:
                            Log.Warn (Log.LOG_AS, "{0}: Status: TooMany_15", CmdNameWithAccount);
                            var protocolState = BEContext.ProtoControl.ProtocolState;
                            if (null != Limit) {
                                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                                    var target = (McProtocolState)record;
                                    target.AsSyncLimit = (uint)Limit;
                                    return true;
                                });
                            }
                            PendingList.RemoveAll (x => x.Id == pending.Id);
                            pending.ResolveAsSuccess (BEContext.ProtoControl);
                            break;

                        default:
                        // Note: we don't send partial Sync requests.
                        case Xml.AirSync.StatusCode.ResendFull_13:
                            Log.Warn (Log.LOG_AS, "{0}: Status: {1}", CmdNameWithAccount, status);
                            PendingList.RemoveAll (x => x.Id == pending.Id);
                            pending.ResponsegXmlStatus = (uint)status;
                            pending.ResolveAsHardFail (BEContext.ProtoControl,
                                NcResult.Error (NcResult.SubKindEnum.Error_InappropriateStatus));
                            break;
                        }
                    }
                }
                return;
            default:
                Log.Error (Log.LOG_AS, "{0}: ProcessCollectionResponses UNHANDLED class {1}", CmdNameWithAccount, classCode);
                return;
            }
        }

        private void ProcessCollectionFetchResponse (McFolder folder, XElement xmlFetch, string classCode)
        {
            // TODO: we are only supposed to see Fetch here if it succeeded.
            // We don't implement fetch yet. When we do, we will need to resolve all the McPendings,
            // even those that aren't in the response document.
        }

        public static bool IsSyncCommand (McPending.Operations op)
        {
            return (IsSyncAddCommand (op) || IsSyncDeleteCommand (op) || IsSyncUpdateCommand (op));
        }

        public static bool IsSyncUpdateCommand (McPending.Operations op)
        {
            switch (op) {
            case McPending.Operations.EmailMarkRead:
            case McPending.Operations.EmailSetFlag:
            case McPending.Operations.EmailClearFlag:
            case McPending.Operations.EmailMarkFlagDone:
            case McPending.Operations.CalUpdate:
            case McPending.Operations.ContactUpdate:
            case McPending.Operations.TaskUpdate:
                return true;
            default:
                return false;
            }
        }

        public static bool IsSyncAddCommand (McPending.Operations op)
        {
            switch (op) {
            case McPending.Operations.CalCreate:
            case McPending.Operations.ContactCreate:
            case McPending.Operations.TaskCreate:
                return true;
            default:
                return false;
            }
        }

        public static bool IsSyncDeleteCommand (McPending.Operations op)
        {
            switch (op) {
            case McPending.Operations.EmailDelete:
            case McPending.Operations.CalDelete:
            case McPending.Operations.ContactDelete:
            case McPending.Operations.TaskDelete:
                return true;
            default:
                return false;
            }
        }

        // PushAssist support.
        public string PushAssistRequestUrl ()
        {
            Op = new AsHttpOperation (CommandName, this, BEContext);
            return ServerUri (Op).ToString ();
        }

        public NcHttpHeaders PushAssistRequestHeaders ()
        {
            Op = new AsHttpOperation (CommandName, this, BEContext);
            NcHttpRequest request;
            if (!Op.CreateHttpRequest (out request, System.Threading.CancellationToken.None)) {
                return null;
            }
            var headers = request.Headers;
            request.Dispose ();
            return headers;
        }

        public byte[] PushAssistRequestData ()
        {
            Op = new AsHttpOperation (CommandName, this, BEContext);
            return ToXDocument (Op).ToWbxml (doFiltering: false);
        }

        private void MarkFoldersPinged ()
        {
            foreach (var iterFolder in FoldersInRequest) {
                iterFolder.UpdateSet_AsSyncLastPing (DateTime.UtcNow);
            }
            var protocolState = BEContext.ProtocolState;
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.LastPing = DateTime.UtcNow;
                return true;
            });
        }
    }
}
