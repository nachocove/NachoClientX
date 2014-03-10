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

        public AsSyncCommand (IAsDataSource dataSource) : base (Xml.AirSync.Sync, Xml.AirSync.Ns, dataSource)
        {
            Timeout = new TimeSpan (0, 0, 20);
            SuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_SyncSucceeded);
            FailureInd = NcResult.Error (NcResult.SubKindEnum.Error_SyncFailed);
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            XNamespace emailNs = Xml.Email.Ns;
            XNamespace tasksNs = Xml.Tasks.Ns;

            // Get the folders needed sync
            var folders = FoldersNeedingSync (DataSource.Account.Id);
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
                if (Xml.AirSync.SyncKey_Initial != folder.AsSyncKey) {
                    collection.Add (new XElement (m_ns + Xml.AirSync.GetChanges));
                    // Set flags when syncing email
                    var classCode = Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (folder.Type);
                    var options = new XElement (m_ns + Xml.AirSync.Options);
                    if (Xml.AirSync.ClassCode.Email.Equals (classCode)) {
                        options.Add (new XElement (m_ns + Xml.AirSync.MimeSupport, (uint)Xml.AirSync.MimeSupportCode.AllMime));
                        options.Add (new XElement (m_ns + Xml.AirSync.FilterType, "5"));
                        options.Add (new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                            new XElement (m_baseNs + Xml.AirSync.Type, (uint)Xml.AirSync.TypeCode.Mime),
                            new XElement (m_baseNs + Xml.AirSync.TruncationSize, "100000000")));
                    }
                    // Expect that we will have more complex code that may add to options, and that
                    // we should only send options if not empty.
                    if (options.HasElements) {
                        collection.Add (options);
                    }
                    // If there are email deletes, then push them up to the server.
                    XElement commands = null;
                    var deles = BackEnd.Instance.Db.Table<McPending> ()
                        .Where (x => x.AccountId == DataSource.Account.Id &&
                                x.FolderServerId == folder.ServerId &&
                                x.Operation == McPending.Operations.EmailDelete);
                    if (0 != deles.Count ()) {
                        if (null == commands) {
                            commands = new XElement (m_ns + Xml.AirSync.Commands);
                        }
                        foreach (var dele in deles) {
                            commands.Add (new XElement (m_ns + Xml.AirSync.Delete,
                                new XElement (m_ns + Xml.AirSync.ServerId, dele.ServerId)));
                            dele.IsDispatched = true;
                            dele.Update ();
                        }
                    }
                    // If there are make-reads, then push them to the server.
                    var mRs = BackEnd.Instance.Db.Table<McPending> ()
                        .Where (x => x.AccountId == DataSource.Account.Id &&
                              x.FolderServerId == folder.ServerId &&
                              x.Operation == McPending.Operations.EmailMarkRead);
                    if (0 != mRs.Count ()) {
                        if (null == commands) {
                            commands = new XElement (m_ns + Xml.AirSync.Commands);
                        }
                        foreach (var change in mRs) {
                            commands.Add (new XElement (m_ns + Xml.AirSync.Change,
                                new XElement (m_ns + Xml.AirSync.ServerId, change.ServerId),
                                new XElement (m_ns + Xml.AirSync.ApplicationData,
                                    new XElement (emailNs + Xml.Email.Read, "1"))));
                            change.IsDispatched = true;
                            change.Update ();
                        }
                    }
                    // If there are set/clear/mark-dones, then push them to the server.
                    var setFs = BackEnd.Instance.Db.Table<McPending> ()
                        .Where (x => x.AccountId == DataSource.Account.Id &&
                                x.FolderServerId == folder.ServerId &&
                                x.Operation == McPending.Operations.EmailSetFlag);

                    var clearFs = BackEnd.Instance.Db.Table<McPending> ()
                        .Where (x => x.AccountId == DataSource.Account.Id &&
                                  x.FolderServerId == folder.ServerId &&
                                  x.Operation == McPending.Operations.EmailClearFlag);

                    var markFs = BackEnd.Instance.Db.Table<McPending> ()
                        .Where (x => x.AccountId == DataSource.Account.Id &&
                                 x.FolderServerId == folder.ServerId &&
                                 x.Operation == McPending.Operations.EmailMarkFlagDone);

                    var calCres = BackEnd.Instance.Db.Table<McPending> ()
                        .Where (x => x.AccountId == DataSource.Account.Id &&
                                  x.FolderServerId == folder.ServerId &&
                                  x.Operation == McPending.Operations.CalCreate);

                    if (0 != setFs.Count () || 0 != clearFs.Count () || 0 != markFs.Count () ||
                        0 != calCres.Count ()) {
                        if (null == commands) {
                            commands = new XElement (m_ns + Xml.AirSync.Commands);
                        }
                    }

                    foreach (var setF in setFs) {
                        commands.Add (new XElement (m_ns + Xml.AirSync.Change,
                            new XElement (m_ns + Xml.AirSync.ServerId, setF.ServerId),
                            new XElement (m_ns + Xml.AirSync.ApplicationData,
                                new XElement (emailNs + Xml.Email.Flag,
                                    new XElement (emailNs + Xml.Email.Status, (uint)Xml.Email.FlagStatusCode.Set),
                                    new XElement (emailNs + Xml.Email.FlagType, setF.FlagType),
                                    new XElement (tasksNs + Xml.Tasks.StartDate, setF.Start.ToLocalTime ().ToAsUtcString ()),
                                    new XElement (tasksNs + Xml.Tasks.UtcStartDate, setF.UtcStart.ToAsUtcString ()),
                                    new XElement (tasksNs + Xml.Tasks.DueDate, setF.Due.ToLocalTime ().ToAsUtcString ()),
                                    new XElement (tasksNs + Xml.Tasks.UtcDueDate, setF.UtcDue.ToAsUtcString ())))));
                        setF.IsDispatched = true;
                        setF.Update ();
                    }

                    foreach (var clearF in clearFs) {
                        commands.Add (new XElement (m_ns + Xml.AirSync.Change,
                            new XElement (m_ns + Xml.AirSync.ServerId, clearF.ServerId),
                            new XElement (m_ns + Xml.AirSync.ApplicationData,
                                new XElement (emailNs + Xml.Email.Flag))));
                        clearF.IsDispatched = true;
                        clearF.Update ();
                    }

                    foreach (var markF in markFs) {
                        commands.Add (new XElement (m_ns + Xml.AirSync.Change,
                            new XElement (m_ns + Xml.AirSync.ServerId, markF.ServerId),
                            new XElement (m_ns + Xml.AirSync.ApplicationData,
                                new XElement (emailNs + Xml.Email.Flag,
                                    new XElement (emailNs + Xml.Email.Status, (uint)Xml.Email.FlagStatusCode.MarkDone),
                                    new XElement (emailNs + Xml.Email.CompleteTime, markF.CompleteTime.ToAsUtcString ()),
                                    new XElement (tasksNs + Xml.Tasks.DateCompleted, markF.DateCompleted.ToAsUtcString ())))));
                        markF.IsDispatched = true;
                        markF.Update ();
                    }

                    foreach (var calCre in calCres) {
                        var cal = McObject.QueryById<McCalendar> (calCre.CalId);
                        if (null != cal) {
                            commands.Add (new XElement (m_ns + Xml.AirSync.Add,
                                new XElement (m_ns + Xml.AirSync.ClientId, calCre.ClientId),
                                // TODO: need the line below if not in a Calendar folder.
                                // new XElement (m_ns + Xml.AirSync.Class, Xml.AirSync.ClassCode.Calendar),
                                AsHelpers.ToXmlApplicationData (cal)));
                            // FIXME - what do we need to say if the item is missing from the DB?
                            calCre.IsDispatched = true;
                            calCre.Update ();
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
            // FIXME - deal with Limit element.
            var globEvent = base.ProcessTopLevelStatus (sender, status);
            if (null != globEvent) {
                return globEvent;
            }
            switch ((Xml.AirSync.StatusCode)status) {
            case Xml.AirSync.StatusCode.Success:
                return null;
            case Xml.AirSync.StatusCode.ProtocolError:
                return Event.Create ((uint)SmEvt.E.HardFail, "ASYNCTOPPE");
            case Xml.AirSync.StatusCode.SyncKeyInvalid:
                DataSource.ProtocolState.AsSyncKey = Xml.AirSync.SyncKey_Initial;
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "ASYNCKEYINV");
            default:
                Log.Error ("AsSyncCommand ProcessResponse UNHANDLED Top Level status: {0}", status);
                return null;
            }
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            Log.Info (Log.LOG_SYNC, "AsSyncCommand response:\n{0}", doc);

            // ProcessTopLevelStatus will handle Status and Limit elements, if  included.
            // If we get here, we know any TL Status is okay.
            var xmlCollections = doc.Root.Element (m_ns + Xml.AirSync.Collections);
            if (null == xmlCollections) {
                return Event.Create ((uint)SmEvt.E.Success, "SYNCSUCCODD");
            }
            var collections = xmlCollections.Elements (m_ns + Xml.AirSync.Collection);
            // Note: we may get back zero Collection items.
            foreach (var collection in collections) {
                // Note: CollectionId, Status and SyncKey are required to be present.
                var serverId = collection.Element (m_ns + Xml.AirSync.CollectionId).Value;
                var folder = McFolderEntry.QueryByServerId<McFolder> (DataSource.Account.Id, serverId);
                var oldSyncKey = folder.AsSyncKey;
                var xmlSyncKey = collection.Element (m_ns + Xml.AirSync.SyncKey);
                var xmlMoreAvailable = collection.Element (m_ns + Xml.AirSync.MoreAvailable);
                var xmlStatus = collection.Element (m_ns + Xml.AirSync.Status);

                if (null != xmlSyncKey) {
                    // The protocol requires SyncKey, but GOOG does not obey in the StatusCode.NotFound case.
                    folder.AsSyncKey = xmlSyncKey.Value;
                    folder.AsSyncRequired = (Xml.AirSync.SyncKey_Initial == oldSyncKey) || (null != xmlMoreAvailable);
                } else {
                    Log.Warn (Log.LOG_SYNC, "SyncKey missing from XML.");
                }
                Log.Info (Log.LOG_SYNC, "MoreAvailable presence {0}", (null != xmlMoreAvailable));
                Log.Info (Log.LOG_SYNC, "Folder:{0}, Old SyncKey:{1}, New SyncKey:{2}", folder.ServerId, oldSyncKey, folder.AsSyncKey);
                var status = (Xml.AirSync.StatusCode)uint.Parse (xmlStatus.Value);
                switch (status) {
                case Xml.AirSync.StatusCode.Success:
                    var xmlCommands = collection.Element (m_ns + Xml.AirSync.Commands);
                    ProcessCollectionCommands (folder, xmlCommands);

                    var xmlResponses = collection.Element (m_ns + Xml.AirSync.Responses);
                    ProcessCollectionResponses (folder, xmlResponses);

                    McPending.DeleteDispatchedByFolderServerId (DataSource.Account.Id, folder.ServerId);
                    break;

                case Xml.AirSync.StatusCode.SyncKeyInvalid:
                case Xml.AirSync.StatusCode.NotFound:
                    Log.Info ("Need to ReSync because of status {0}.", status);
                    // NotFound as seen so far (GOOG) isn't making sense. 
                    folder.AsSyncKey = Xml.AirSync.SyncKey_Initial;
                    folder.AsSyncRequired = true;
                    break;

                default:
                    // FIXME - on hard fail, whack dependent McPending.
                    Log.Error ("AsSyncCommand ProcessResponse UNHANDLED Collection status: {0}", status);
                    break;
                }
                folder.Update ();
            }
            if (HadEmailMessageChanges) {
                DataSource.Control.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
            }
            if (HadNewUnreadEmailMessageInInbox) {
                DataSource.Control.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox));
            }
            if (HadContactChanges) {
                DataSource.Control.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            }
            if (HadCalendarChanges) {
                DataSource.Control.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            }
            if (FoldersNeedingSync (DataSource.Account.Id).Any ()) {
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

            DeleteAllDispatchedPending ();

            if (FoldersNeedingSync (DataSource.Account.Id).Any ()) {
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "SYNCRESYNC1");
            } else {
                return Event.Create ((uint)SmEvt.E.Success, "SYNCSUCCESS1");
            }
        }

        private void DeleteAllDispatchedPending ()
        {
            var pendings = BackEnd.Instance.Db.Table<McPending> ()
                .Where (x => x.AccountId == DataSource.Account.Id && x.IsDispatched == true);
            foreach (var pending in pendings) {
                pending.Delete ();
            }
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
        // FIXME - make this a generic extension.
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
                        if (null != emailMessage && (uint)Xml.FolderHierarchy.TypeCode.DefaultInbox == folder.Type &&
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
                        var emailMessage = McFolderEntry.QueryByServerId<McEmailMessage> (DataSource.Account.Id, delServerId);
                        if (null != emailMessage) {
                            emailMessage.Delete ();
                        }
                        break;
                    case Xml.AirSync.ClassCode.Calendar:
                        HadCalendarChanges = true;
                        var cal = McFolderEntry.QueryByServerId<McCalendar> (DataSource.Account.Id, delServerId);
                        if (null != cal) {
                            cal.Delete ();
                        }
                        break;
                    case Xml.AirSync.ClassCode.Contacts:
                        HadContactChanges = true;
                        var contact = McFolderEntry.QueryByServerId<McContact> (DataSource.Account.Id, delServerId);
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
                McItem item;
                switch (response.Name.LocalName) {
                case Xml.AirSync.Add:
                    // Status and ClientId are required to be present.
                    var xmlStatus = response.Element (m_ns + Xml.AirSync.Status);
                    var status = (Xml.AirSync.StatusCode)uint.Parse (xmlStatus.Value);
                    switch (status) {
                    case Xml.AirSync.StatusCode.Success:
                    case Xml.AirSync.StatusCode.LimitReWait:
                        // Note: we don't wait using Sync, we use Ping.
                        break;
                    case Xml.AirSync.StatusCode.ProtocolError:
                    case Xml.AirSync.StatusCode.ClientError:
                    case Xml.AirSync.StatusCode.ResendFull:
                    case Xml.AirSync.StatusCode.ServerWins:
                    case Xml.AirSync.StatusCode.NoSpace:
                        // Note: we don't send partial Sync requests.
                        // The Add operation is permanently screwed.
                        // FIXME - do a StatusInd to say so.
                        continue;
                    }

                    var xmlClientId = response.Element (m_ns + Xml.AirSync.ClientId);
                    var clientId = xmlClientId.Value;
                    switch (classCode) {
                    case Xml.AirSync.ClassCode.Email:
                        Log.Warn ("AsSyncCommand ProcessCollectionResponses:Add - should not see Email.");
                        continue;
                    case Xml.AirSync.ClassCode.Contacts:
                        item = McItem.QueryByClientId<McContact> (DataSource.Account.Id, clientId);
                        break;
                    case Xml.AirSync.ClassCode.Calendar:
                        item = McItem.QueryByClientId<McCalendar> (DataSource.Account.Id, clientId);
                        break;
                    default:
                        Log.Error ("AsSyncCommand ProcessCollectionResponses UNHANDLED class " + classCode);
                        continue;
                    }
                    var xmlServerId = response.Element (m_ns + Xml.AirSync.ServerId);
                    var serverId = xmlServerId.Value;
                    if (null != serverId) {
                        item.ServerId = serverId;
                    }
                    item.Update ();
                    break;

                case Xml.AirSync.Change:
                    // Note: we are only supposed to see Change here if it failed.
                    switch (classCode) {
                    case Xml.AirSync.ClassCode.Email:
                        Log.Warn ("AsSyncCommand ProcessCollectionResponses:Change - should not see Email.");
                        continue;
                    case Xml.AirSync.ClassCode.Contacts:
                    case Xml.AirSync.ClassCode.Calendar:
                        // FIXME - report error Status values via StatusInd.
                        continue;
                    default:
                        Log.Error ("AsSyncCommand ProcessCollectionResponses UNHANDLED class " + classCode);
                        continue;
                    }
                        #pragma warning disable 162
                    break;
                        #pragma warning restore 162

                case Xml.AirSync.Fetch:
                    // Note: we are only supposed to see Fetch here if it succeeded.
                    switch (classCode) {
                    case Xml.AirSync.ClassCode.Contacts:
                    case Xml.AirSync.ClassCode.Email:
                    case Xml.AirSync.ClassCode.Calendar:
                        // FIXME. We don't use Fetch (yet). In a fetch, we need to save the complete item to the DB.
                        continue;
                    default:
                        Log.Error ("AsSyncCommand ProcessCollectionResponses UNHANDLED class " + classCode);
                        continue;
                    }
                    break;

                default:
                    Log.Error ("AsSyncCommand ProcessCollectionResponses UNHANDLED response " + response.Name.LocalName);
                    break;
                }
            }
        }
    }
}
