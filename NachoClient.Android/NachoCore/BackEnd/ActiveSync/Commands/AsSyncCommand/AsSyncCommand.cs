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
        private List<McPending> EmailDeletes, EmailMarkReads, EmailSetFlags, EmailClearFlags, EmailMarkFlagDones, CalCreates;

        private void PareListsToPendingList ()
        {
            // Make sure only PendingList members are in the lists. TODO: Yes O(N**2), tiny N.
            EmailDeletes.RemoveAll (pending => 0 > PendingList.IndexOf (pending));
            EmailMarkReads.RemoveAll (pending => 0 > PendingList.IndexOf (pending));
            EmailSetFlags.RemoveAll (pending => 0 > PendingList.IndexOf (pending));
            EmailClearFlags.RemoveAll (pending => 0 > PendingList.IndexOf (pending));
            EmailMarkFlagDones.RemoveAll (pending => 0 > PendingList.IndexOf (pending));
            CalCreates.RemoveAll (pending => 0 > PendingList.IndexOf (pending));
        }

        public AsSyncCommand (IBEContext dataSource) : base (Xml.AirSync.Sync, Xml.AirSync.Ns, dataSource)
        {
            Timeout = new TimeSpan (0, 0, 20);
            SuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_SyncSucceeded);
            FailureInd = NcResult.Error (NcResult.SubKindEnum.Error_SyncFailed);

            var candidateList = new List<McPending> ();
            EmailDeletes = McPending.QueryByOperation (dataSource.Account.Id, McPending.Operations.EmailDelete);
            candidateList.AddRange (EmailDeletes);
            EmailMarkReads = McPending.QueryByOperation (dataSource.Account.Id, McPending.Operations.EmailMarkRead);
            candidateList.AddRange (EmailMarkReads);
            EmailSetFlags = McPending.QueryByOperation (dataSource.Account.Id, McPending.Operations.EmailSetFlag);
            candidateList.AddRange (EmailSetFlags);
            EmailClearFlags = McPending.QueryByOperation (dataSource.Account.Id, McPending.Operations.EmailClearFlag);
            candidateList.AddRange (EmailClearFlags);
            EmailMarkFlagDones = McPending.QueryByOperation (dataSource.Account.Id, McPending.Operations.EmailMarkFlagDone);
            candidateList.AddRange (EmailMarkFlagDones);
            CalCreates = McPending.QueryByOperation (dataSource.Account.Id, McPending.Operations.CalCreate);
            candidateList.AddRange (CalCreates);
            // Check to see if any of the pending require serial mode. We end up with:
            // List of one: just a serial-only pending.
            // List has everything up to but not including the serial-only pending.
            // List has everything - we didn't see a serial-only.
            foreach (var pending in candidateList) {
                if (pending.DeferredSerialIssueOnly) {
                    // If the serial-only pending is 1st, execute it. Otherwise it waits.
                    if (0 == PendingList.Count) {
                        PendingList.Add (pending);
                    }
                    break;
                }
                PendingList.Add (pending);
            }
            PareListsToPendingList ();
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            XNamespace emailNs = Xml.Email.Ns;
            XNamespace tasksNs = Xml.Tasks.Ns;

            // Get the folders needed sync
            var folders = FoldersNeedingSync (BEContext.Account.Id);
            // This becomes the folders in the xml <Collections>
            var collections = new XElement (m_ns + Xml.AirSync.Collections);
            // Save the list for later; so we can eliminiate redundant sync requests
            FoldersInRequest = new List<McFolder> ();
            foreach (var folder in folders) {
                FoldersInRequest.Add (folder);
                var collection = new XElement (m_ns + Xml.AirSync.Collection,
                                     new XElement (m_ns + Xml.AirSync.SyncKey, folder.AsSyncKey),
                                     new XElement (m_ns + Xml.AirSync.CollectionId, folder.ServerId));
                // Add <GetChanges/> if we've done a sync before
                if (McFolder.AsSyncKey_Initial != folder.AsSyncKey) {
                    collection.Add (new XElement (m_ns + Xml.AirSync.GetChanges));
                    // Set flags when syncing email
                    var classCode = Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (folder.Type);
                    var options = new XElement (m_ns + Xml.AirSync.Options);
                    if (Xml.AirSync.ClassCode.Email.Equals (classCode)) {
                        options.Add (new XElement (m_ns + Xml.AirSync.MimeSupport, (uint)Xml.AirSync.MimeSupportCode.AllMime_2));
                        options.Add (new XElement (m_ns + Xml.AirSync.FilterType, "5"));
                        options.Add (new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                            new XElement (m_baseNs + Xml.AirSync.Type, (uint)Xml.AirSync.TypeCode.Mime_4),
                            new XElement (m_baseNs + Xml.AirSync.TruncationSize, "100000000")));
                    }
                    if (Xml.AirSync.ClassCode.Calendar.Equals (classCode)) {
                        options.Add (new XElement (m_ns + Xml.AirSync.MimeSupport, (uint)Xml.AirSync.MimeSupportCode.AllMime_2));
                        options.Add (new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                            new XElement (m_baseNs + Xml.AirSync.Type, (uint)Xml.AirSync.TypeCode.Mime_4),
                            new XElement (m_baseNs + Xml.AirSync.TruncationSize, "100000000")));
                    }
                    if (Xml.AirSync.ClassCode.Contacts.Equals (classCode)) {
                        options.Add (new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                            new XElement (m_baseNs + Xml.AirSync.Type, (uint)Xml.AirSync.TypeCode.PlainText_1),
                            new XElement (m_baseNs + Xml.AirSync.TruncationSize, "100000000")));
                    }
                    if (options.HasElements) {
                        collection.Add (options);
                    }
                    // If there are email deletes, then push them up to the server.
                    XElement commands = null;
                    if (0 != PendingList.Count) {
                        commands = new XElement (m_ns + Xml.AirSync.Commands);
                    }

                    foreach (var pending in EmailDeletes) {
                        commands.Add (new XElement (m_ns + Xml.AirSync.Delete,
                            new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId)));
                        pending.MarkDispached ();
                    }
                    // If there are make-reads, then push them to the server.
                    foreach (var pending in EmailMarkReads) {
                        commands.Add (new XElement (m_ns + Xml.AirSync.Change,
                            new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                            new XElement (m_ns + Xml.AirSync.ApplicationData,
                                new XElement (emailNs + Xml.Email.Read, "1"))));
                        pending.MarkDispached ();
                    }
                    // If there are set/clear/mark-dones, then push them to the server.
                    foreach (var pending in EmailSetFlags) {
                        commands.Add (new XElement (m_ns + Xml.AirSync.Change,
                            new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                            new XElement (m_ns + Xml.AirSync.ApplicationData,
                                new XElement (emailNs + Xml.Email.Flag,
                                    new XElement (emailNs + Xml.Email.Status, (uint)Xml.Email.FlagStatusCode.Set_2),
                                    new XElement (emailNs + Xml.Email.FlagType, pending.FlagType),
                                    new XElement (tasksNs + Xml.Tasks.StartDate, pending.Start.ToLocalTime ().ToAsUtcString ()),
                                    new XElement (tasksNs + Xml.Tasks.UtcStartDate, pending.UtcStart.ToAsUtcString ()),
                                    new XElement (tasksNs + Xml.Tasks.DueDate, pending.Due.ToLocalTime ().ToAsUtcString ()),
                                    new XElement (tasksNs + Xml.Tasks.UtcDueDate, pending.UtcDue.ToAsUtcString ())))));
                        pending.MarkDispached ();
                    }

                    foreach (var pending in EmailClearFlags) {
                        commands.Add (new XElement (m_ns + Xml.AirSync.Change,
                            new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                            new XElement (m_ns + Xml.AirSync.ApplicationData,
                                new XElement (emailNs + Xml.Email.Flag))));
                        pending.MarkDispached ();
                    }

                    foreach (var pending in EmailMarkFlagDones) {
                        commands.Add (new XElement (m_ns + Xml.AirSync.Change,
                            new XElement (m_ns + Xml.AirSync.ServerId, pending.ServerId),
                            new XElement (m_ns + Xml.AirSync.ApplicationData,
                                new XElement (emailNs + Xml.Email.Flag,
                                    new XElement (emailNs + Xml.Email.Status, (uint)Xml.Email.FlagStatusCode.MarkDone_1),
                                    new XElement (emailNs + Xml.Email.CompleteTime, pending.CompleteTime.ToAsUtcString ()),
                                    new XElement (tasksNs + Xml.Tasks.DateCompleted, pending.DateCompleted.ToAsUtcString ())))));
                        pending.MarkDispached ();
                    }

                    foreach (var pending in CalCreates) {
                        var cal = McObject.QueryById<McCalendar> (pending.CalId);
                        if (null != cal) {
                            cal.ReadAncillaryData ();
                            commands.Add (new XElement (m_ns + Xml.AirSync.Add,
                                new XElement (m_ns + Xml.AirSync.ClientId, pending.ClientId),
                                // FIXME: need the line below if not in a Calendar folder.
                                // new XElement (m_ns + Xml.AirSync.Class, Xml.AirSync.ClassCode.Calendar),
                                AsHelpers.ToXmlApplicationData (cal)));
                            // FIXME - what do we need to say if the item is missing from the DB?
                            pending.MarkDispached ();
                        }
                    }

                    if (null != commands) {
                        collection.Add (commands);
                    }
                }
                collections.Add (collection);
            }
            var sync = new XElement (m_ns + Xml.AirSync.Sync, collections);
            sync.Add (new XElement (m_ns + Xml.AirSync.WindowSize, "25"));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (sync);
            Log.Info (Log.LOG_SYNC, "AsSyncCommand:\n{0}", doc);
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

            case Xml.AirSync.StatusCode.SyncKeyInvalid_3:
                // FIXME - need resolution logic to deal with _Initial-level re-sync of a folder.
                foreach (var folder in FoldersInRequest) {
                    folder.AsSyncKey = McFolder.AsSyncKey_Initial;
                    folder.AsSyncRequired = true;
                    folder.Update ();
                }
                foreach (var pending in PendingList) {
                    pending.ResolveAsDeferredForce ();
                }
                PendingList.Clear ();
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "ASYNCTOPFOOF");

            case Xml.AirSync.StatusCode.ProtocolError_4:
                var result = NcResult.Error (NcResult.SubKindEnum.Error_ProtocolError);
                if (1 == PendingList.Count) {
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
                    folder.AsSyncRequired = true;
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
                    folder.AsSyncRequired = true;
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
                    folder.AsSyncRequired = true;
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
            Log.Info (Log.LOG_SYNC, "AsSyncCommand response:\n{0}", doc);
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
                    folder.AsSyncRequired = (McFolder.AsSyncKey_Initial == oldSyncKey) || (null != xmlMoreAvailable);
                } else {
                    Log.Warn (Log.LOG_SYNC, "SyncKey missing from XML.");
                }
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
                    folder.AsSyncRequired = true;
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
                    folder.AsSyncRequired = true;
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
            } else if (FoldersNeedingSync (BEContext.Account.Id).Any ()) {
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "SYNCRESYNC0");
            } else {
                return Event.Create ((uint)SmEvt.E.Success, "SYNCSUCCESS0");
            }
        }
        // Called when we get an empty Sync response body.
        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response)
        {
            foreach (var folder in FoldersInRequest) {
                folder.AsSyncRequired = false;
                folder.Update ();
            }
            foreach (var pending in PendingList) {
                pending.ResolveAsSuccess (BEContext.ProtoControl);
            }
            PendingList.Clear ();

            if (FoldersNeedingSync (BEContext.Account.Id).Any ()) {
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

        public static List<McFolder> FoldersNeedingSync (int accountId)
        {
            // Make sure any folders with pending are marked as needing Sync.
            var pendings = BackEnd.Instance.Db.Table<McPending> ().Where (x => 
				x.AccountId == accountId).ToList ();

            foreach (var pending in pendings) {
                switch (pending.Operation) {
                // Only Pendings that are resolved by Sync count for us here.
                case McPending.Operations.EmailDelete:
                case McPending.Operations.EmailMarkRead:
                case McPending.Operations.EmailSetFlag:
                case McPending.Operations.EmailClearFlag:
                case McPending.Operations.EmailMarkFlagDone:
                case McPending.Operations.CalCreate:
                    break;

                default:
                    continue;
                }

                // FIXME - we need a clear rule on who is responsible for setting AsSyncRequired.
                if (null != pending.FolderServerId && string.Empty != pending.FolderServerId) {
                    var folder = BackEnd.Instance.Db.Table<McFolder> ().SingleOrDefault (x => 
                    x.ServerId == pending.FolderServerId);
                    if (false == folder.IsClientOwned && !folder.AsSyncRequired) {
                        folder.AsSyncRequired = true;
                        folder.Update ();
                    }
                }
            }

            // Ping, et al, decide what needs to be checked.  We sync what needs sync'ing.
            // If we don't sync the flagged folders, then the ping command starts right back up.
            return BackEnd.Instance.Db.Table<McFolder> ().Where (x => x.AccountId == accountId &&
            true == x.AsSyncRequired && false == x.IsClientOwned).ToList ();
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
            PendingList.Add (firstPending);
            PareListsToPendingList ();
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
                        if (null != emailMessage && (uint)Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == folder.Type &&
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
                        folder.AsSyncRequired = true;
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
    }
}
