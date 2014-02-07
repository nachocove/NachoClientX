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
            var folders = FoldersNeedingSync ();
            // This becomes the folders in the xml <Collections>
            var collections = new XElement (m_ns + Xml.AirSync.Collections);
            // Save the list for later; so we can eliminiate redundant sync requests
            FoldersInRequest = new List<McFolder> ();
            foreach (var folder in folders) {
                FoldersInRequest.Add (folder);
                // E.g.
                // <Collection>
                //  <SyncKey>0</SyncKey>
                //  <CollectionId>Contact:DEFAULT</CollectionId>
                // </Collection>
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

                    if (0 != setFs.Count () || 0 != clearFs.Count () || 0 != markFs.Count ()) {
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
                                    new XElement (emailNs + Xml.Email.FlagType, setF.Message),
                                    new XElement (tasksNs + Xml.Tasks.StartDate, setF.UtcStart.ToLocalTime ().ToAsUtcString ()),
                                    new XElement (tasksNs + Xml.Tasks.UtcStartDate, setF.UtcStart.ToAsUtcString ()),
                                    new XElement (tasksNs + Xml.Tasks.DueDate, setF.UtcDue.ToLocalTime ().ToAsUtcString ()),
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
                                    new XElement (emailNs + Xml.Email.CompleteTime, DateTime.UtcNow.ToLocalTime ().ToAsUtcString ()),
                                    new XElement (tasksNs + Xml.Tasks.DateCompleted, DateTime.UtcNow.ToLocalTime ().ToAsUtcString ())))));
                        markF.IsDispatched = true;
                        markF.Update ();
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

            var collections = doc.Root.Element (m_ns + Xml.AirSync.Collections).Elements (m_ns + Xml.AirSync.Collection);
            foreach (var collection in collections) {
                var serverId = collection.Element (m_ns + Xml.AirSync.CollectionId).Value;
                var folder = BackEnd.Instance.Db.Table<McFolder> ().Single (rec => rec.AccountId == DataSource.Account.Id &&
                             rec.ServerId == serverId);
                var xmlSyncKey = collection.Element (m_ns + Xml.AirSync.SyncKey);
                var oldSyncKey = folder.AsSyncKey;
                if (null != xmlSyncKey) {
                    // The protocol requires SyncKey, but GOOG does not obey in the StatusCode.NotFound case.
                    folder.AsSyncKey = collection.Element (m_ns + Xml.AirSync.SyncKey).Value;
                    folder.AsSyncRequired = (Xml.AirSync.SyncKey_Initial == oldSyncKey) ||
                    (null != collection.Element (m_ns + Xml.AirSync.MoreAvailable));
                } else {
                    Log.Warn (Log.LOG_SYNC, "SyncKey missing from XML.");
                }
                Log.Info (Log.LOG_SYNC, "MoreAvailable presence {0}", (null != collection.Element (m_ns + Xml.AirSync.MoreAvailable)));
                Log.Info (Log.LOG_SYNC, "Folder:{0}, Old SyncKey:{1}, New SyncKey:{2}", folder.ServerId, oldSyncKey, folder.AsSyncKey);
                var status = collection.Element (m_ns + Xml.AirSync.Status);
                var statusCode = (Xml.AirSync.StatusCode)uint.Parse (status.Value);
                switch (statusCode) {
                case Xml.AirSync.StatusCode.Success:
                    // Clear any deletes dispached in the request.
                    var deles = BackEnd.Instance.Db.Table<McPending> ()
                        .Where (x => x.AccountId == DataSource.Account.Id &&
                                x.FolderServerId == folder.ServerId &&
                                x.Operation == McPending.Operations.EmailDelete &&
                                x.IsDispatched == true);
                    if (0 != deles.Count ()) {
                        foreach (var change in deles) {
                            change.Delete ();
                        }
                    }
                    // Clear any mark-reads dispatched in the request.
                    var mRs = BackEnd.Instance.Db.Table<McPending> ()
                        .Where (x => x.AccountId == DataSource.Account.Id &&
                              x.FolderServerId == folder.ServerId &&
                              x.Operation == McPending.Operations.EmailMarkRead &&
                              x.IsDispatched == true);
                    if (0 != mRs.Count ()) {
                        foreach (var markRead in mRs) {
                            markRead.Delete ();
                        }
                    }
                    // Clear any set/clear/mark-dones dispatched in the request.
                    var setFs = BackEnd.Instance.Db.Table<McPending> ()
                        .Where (x => x.AccountId == DataSource.Account.Id &&
                                x.FolderServerId == folder.ServerId &&
                                x.Operation == McPending.Operations.EmailSetFlag &&
                                x.IsDispatched == true);
                    foreach (var setF in setFs) {
                        setF.Delete ();
                    }

                    var clearFs = BackEnd.Instance.Db.Table<McPending> ()
                        .Where (x => x.AccountId == DataSource.Account.Id &&
                                  x.FolderServerId == folder.ServerId &&
                                  x.Operation == McPending.Operations.EmailClearFlag &&
                                  x.IsDispatched == true);
                    foreach (var clearF in clearFs) {
                        clearF.Delete ();
                    }

                    var markFs = BackEnd.Instance.Db.Table<McPending> ()
                        .Where (x => x.AccountId == DataSource.Account.Id &&
                                 x.FolderServerId == folder.ServerId &&
                                 x.Operation == McPending.Operations.EmailMarkFlagDone &&
                                 x.IsDispatched == true);
                    foreach (var markF in markFs) {
                        markF.Delete ();
                    }
                    // FIXME - this is a DUMB way to cleanup dispatched!

                    // Perform all commands.
                    XElement xmlClass;
                    string classCode;
                    var commandsNode = collection.Element (m_ns + Xml.AirSync.Commands);
                    if (null != commandsNode) {
                        var commands = commandsNode.Elements ();
                        foreach (var command in commands) {
                            switch (command.Name.LocalName) {
                            case Xml.AirSync.Add:
                                // If the Class element is present, respect it. Otherwise key off
                                // the type of the folder.
                                // FIXME - 12.1-isms.
                                xmlClass = command.Element (m_ns + Xml.AirSync.Class);
                                if (null != xmlClass) {
                                    classCode = xmlClass.Value;
                                } else {
                                    classCode = Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (folder.Type);
                                }
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
                                    Log.Error ("AsSyncCommand ProcessResponse UNHANDLED class " + classCode);
                                    break;
                                }
                                break;
                            case Xml.AirSync.Change:
                                // TODO: Merge with Add.
                                xmlClass = command.Element (m_ns + Xml.AirSync.Class);
                                if (null != xmlClass) {
                                    classCode = xmlClass.Value;
                                } else {
                                    classCode = Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (folder.Type);
                                }
                                switch (classCode) {
                                case Xml.AirSync.ClassCode.Email:
                                    ServerSaysChangeEmailItem (command, folder);
                                    break;
                                case Xml.AirSync.ClassCode.Calendar:
                                    ServerSaysChangeCalendarItem (command, folder);
                                    break;
                                case Xml.AirSync.ClassCode.Contacts:
                                    ServerSaysChangeContact (command, folder);
                                    break;
                                default:
                                    Log.Error ("AsSyncCommand ProcessResponse UNHANDLED class " + classCode);
                                    break;
                                }
                                break;

                            case Xml.AirSync.Delete:
                                xmlClass = command.Element (m_ns + Xml.AirSync.Class);
                                if (null != xmlClass) {
                                    classCode = xmlClass.Value;
                                } else {
                                    classCode = Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (folder.Type);
                                }
                                switch (classCode) {
                                // FIXME: support Calendar & Contacts too.
                                case Xml.AirSync.ClassCode.Email:
                                    HadEmailMessageChanges = true;
                                    var delServerId = command.Element (m_ns + Xml.AirSync.ServerId).Value;
                                    var emailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ().SingleOrDefault (x => x.ServerId == delServerId);
                                    if (null != emailMessage) {
                                        emailMessage.DeleteBody (BackEnd.Instance.Db);
                                        emailMessage.Delete ();
                                    }
                                    break;
                                case Xml.AirSync.ClassCode.Calendar:
                                    HadCalendarChanges = true;
                                    // FIXME - do the delete.
                                    break;
                                case Xml.AirSync.ClassCode.Contacts:
                                    HadContactChanges = true;
                                    // FIXME - do the delete.
                                    break;
                                }
                                break;

                            default:
                                Log.Error ("AsSyncCommand ProcessResponse UNHANDLED command " + command.Name.LocalName);
                                break;
                            }
                        }
                    }
                    break;

                case Xml.AirSync.StatusCode.SyncKeyInvalid:
                case Xml.AirSync.StatusCode.NotFound:
                    Log.Info ("Need to ReSync because of status {0}.", statusCode);
                    // NotFound as seen so far (GOOG) isn't making sense. 
                    folder.AsSyncKey = Xml.AirSync.SyncKey_Initial;
                    folder.AsSyncRequired = true;
                    break;

                default:
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
            if (FoldersNeedingSync ().Any ()) {
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

            if (FoldersNeedingSync ().Any ()) {
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

        private SQLite.TableQuery<McFolder> FoldersNeedingSync ()
        {
            // Make sure any folders with pending are marked as needing Sync.
            var pendings = BackEnd.Instance.Db.Table<McPending> ().Where (x => 
                x.AccountId == DataSource.Account.Id).ToList ();

            foreach (var pending in pendings) {
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
            return BackEnd.Instance.Db.Table<McFolder> ().Where (x => x.AccountId == DataSource.Account.Id &&
            true == x.AsSyncRequired && false == x.IsClientOwned);
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
    }
}
