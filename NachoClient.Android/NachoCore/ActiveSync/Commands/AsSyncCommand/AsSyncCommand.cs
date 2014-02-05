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
                            BackEnd.Instance.Db.Update (dele);
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
                            BackEnd.Instance.Db.Update (change);
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
                            BackEnd.Instance.Db.Delete (change);
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
                            BackEnd.Instance.Db.Delete (markRead);
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
                                    var emailMessage = AddEmail (command, folder);
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
                                // FIXME: Impact on emails?
                                xmlClass = command.Element (m_ns + Xml.AirSync.Class);
                                if (null != xmlClass) {
                                    classCode = xmlClass.Value;
                                } else {
                                    classCode = Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (folder.Type);
                                }
                                switch (classCode) {
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
                                    var delServerId = command.Element (m_ns + Xml.AirSync.ServerId).Value;
                                    var emailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ().SingleOrDefault (x => x.ServerId == delServerId);
                                    if (null != emailMessage) {
                                        emailMessage.DeleteBody (BackEnd.Instance.Db);
                                        BackEnd.Instance.Db.Delete (emailMessage);
                                    }
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

                BackEnd.Instance.Db.Update (folder);
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
                BackEnd.Instance.Db.Update (folder);
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

        private McEmailMessage AddEmail (XElement command, McFolder folder)
        {
            XNamespace email2Ns = Xml.Email2.Ns;

            var serverId = command.Element (m_ns + Xml.AirSync.ServerId).Value;
            // MoveItems should make the server send an Add/Delete that we want to ignore.
            // If we see an Add with a pre-existing ServerId in the DB, then we ignore the Add Op.
            // In 0.2, be more precise about dropping these Add/Deletes.
            if (BackEnd.Instance.Db.Table<McEmailMessage> ().Any (x =>
                x.AccountId == DataSource.Account.Id &&
                x.ServerId == serverId)) {
                return null;
            }

            IEnumerable<XElement> xmlAttachments = null;
            var emailMessage = new McEmailMessage {
                AccountId = DataSource.Account.Id,
                ServerId = serverId,
            };

            var appData = command.Element (m_ns + Xml.AirSync.ApplicationData);
            foreach (var child in appData.Elements()) {
                switch (child.Name.LocalName) {
                case Xml.AirSyncBase.Attachments:
                    xmlAttachments = child.Elements (m_baseNs + Xml.AirSyncBase.Attachment);
                    break;
                case Xml.AirSyncBase.Body:
                    emailMessage.BodyType = child.Element (m_baseNs + Xml.AirSyncBase.Type).Value;
                    var bodyElement = child.Element (m_baseNs + Xml.AirSyncBase.Data);
                    // NOTE: We have seen EstimatedDataSize of 0 and no Truncate here.
                    if (null != bodyElement) {
                        var saveAttr = bodyElement.Attributes ().SingleOrDefault (x => x.Name == "nacho-body-id");
                        if (null != saveAttr) {
                            emailMessage.BodyId = int.Parse (saveAttr.Value);
                        } else {
                            var body = new McBody ();
                            body.Body = bodyElement.Value; 
                            BackEnd.Instance.Db.Insert (body);
                            emailMessage.BodyId = body.Id;
                        }
                    } else {
                        emailMessage.BodyId = 0;
                        Console.WriteLine ("Truncated message from server.");
                    }
                    break;
                case Xml.Email.To:
                    // TODO: Append
                    emailMessage.To = child.Value;
                    break;
                case Xml.Email.Cc:
                    // TODO: Append
                    emailMessage.Cc = child.Value;
                    break;
                case Xml.Email.From:
                    emailMessage.From = child.Value;
                    break;
                case Xml.Email.ReplyTo:
                    emailMessage.ReplyTo = child.Value;
                    break;
                case Xml.Email.Subject:
                    emailMessage.Subject = child.Value;
                    break;
                case Xml.Email.DateReceived:
                    try {
                        emailMessage.DateReceived = DateTime.Parse (child.Value);
                    } catch {
                        // FIXME - just log it.
                    }
                    break;
                case Xml.Email.DisplayTo:
                    emailMessage.DisplayTo = child.Value;
                    break;
                case Xml.Email.Importance:
                    try {
                        emailMessage.Importance = UInt32.Parse (child.Value);
                    } catch {
                        // FIXME - just log it.
                    }
                    break;
                case Xml.Email.Read:
                    if ("1" == child.Value) {
                        emailMessage.IsRead = true;
                    } else {
                        emailMessage.IsRead = false;
                    }
                    break;
                case Xml.Email.MessageClass:
                    emailMessage.MessageClass = child.Value;
                    break;
                case Xml.Email.ThreadTopic:
                    emailMessage.ThreadTopic = child.Value;
                    break;
                case Xml.Email.Sender:
                    emailMessage.Sender = child.Value;
                    break;
                case Xml.Email2.ReceivedAsBcc:
                    if ("1" == child.Value) {
                        emailMessage.ReceivedAsBcc = true;
                    } else {
                        emailMessage.ReceivedAsBcc = false;
                    }
                    break;
                case Xml.Email2.ConversationId:
                    // FIXME: Will be base64 string soon
//                    emailMessage.ConversationId = child.Value;
                    break;
                }
            }

            // Need to handle the illegal case (GOOG "All" folder) where the ServerId is used twice
            // on the 2nd insert of the same message.
            var existingEmailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ()
                .SingleOrDefault (x => 
                    x.AccountId == emailMessage.AccountId &&
                                       x.ServerId == emailMessage.ServerId);
            int emailMessageId;
            if (null == existingEmailMessage) {
                BackEnd.Instance.Db.Insert (emailMessage);
                emailMessageId = emailMessage.Id;
            } else {
                emailMessageId = existingEmailMessage.Id;
            }
            var map = new McMapFolderItem (DataSource.Account.Id) {
                ItemId = emailMessageId,
                FolderId = folder.Id,
                ClassCode = (uint)McItem.ClassCodeEnum.Email,
            };
            BackEnd.Instance.Db.Insert (map);
            if (null != existingEmailMessage) {
                return null;
            }
            if (null != xmlAttachments) {
                foreach (XElement xmlAttachment in xmlAttachments) {
                    // Create & save the attachment record.
                    var attachment = new McAttachment {
                        AccountId = emailMessage.AccountId,
                        EmailMessageId = emailMessage.Id,
                        IsDownloaded = false,
                        PercentDownloaded = 0,
                        IsInline = false,
                        EstimatedDataSize = uint.Parse (xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.EstimatedDataSize).Value),
                        FileReference = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.FileReference).Value,
                        Method = uint.Parse (xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.Method).Value),
                    };
                    var displayName = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.DisplayName);
                    if (null != displayName) {
                        attachment.DisplayName = displayName.Value;
                    }
                    var contentLocation = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.ContentLocation);
                    if (null != contentLocation) {
                        attachment.ContentLocation = contentLocation.Value;
                    }
                    var contentId = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.ContentId);
                    if (null != contentId) {
                        attachment.ContentId = contentId.Value;
                    }
                    var isInline = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.IsInline);
                    if (null != isInline) {
                        attachment.IsInline = ParseXmlBoolean (isInline);
                    }
                    var xmlUmAttDuration = xmlAttachment.Element (email2Ns + Xml.Email2.UmAttDuration);
                    if (null != xmlUmAttDuration) {
                        attachment.VoiceSeconds = uint.Parse (xmlUmAttDuration.Value);
                    }
                    var xmlUmAttOrder = xmlAttachment.Element (email2Ns + Xml.Email2.UmAttOrder);
                    if (null != xmlUmAttOrder) {
                        attachment.VoiceOrder = int.Parse (xmlUmAttOrder.Value);
                    }
                    BackEnd.Instance.Db.Insert (attachment);
                }
            }
            return emailMessage;
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
