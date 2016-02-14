using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using SQLite;
using NachoCore.Utils;
using NachoCore.ActiveSync;
using Portable.Text;
using System.Security.Cryptography;

namespace NachoCore.Model
{
    public class McFolder : McAbstrFolderEntry
    {
        [Indexed]
        public bool IsClientOwned { get; set; }

        [Indexed]
        public bool IsHidden { get; set; }

        [Indexed]
        public string ParentId { get; set; }

        public const string AsSyncKey_Initial = "0";
        public const string AsRootServerId = "0";

        public string AsSyncKey { get; set; }

        // Keep track of certian back-to-back Sync failures. If N happen in a row, we will reset the SyncKey.
        public int AsSyncFailRun { get; set; }

        // For keeping old folders around through a forced re-FolderSync from 0.
        public uint AsFolderSyncEpoch { get; set; }
        // For keeping old items around through a forced re-Sync from 0.
        public int AsSyncEpoch { get; set; }

        public bool AsSyncEpochScrubNeeded { get; set; }
        // AsSyncMetaToClientExpected true when we have a reason to believe that we're not synced up.
        public bool AsSyncMetaToClientExpected { get; set; }
        // Updated when a Sync works on this folder. When we hit MaxFolders limit, this decides who goes next.
        public DateTime AsSyncLastPing { get; set; }
        // True after we see our first command from the server (gotta be an Add).
        public bool HasSeenServerCommand { get; set; }
        // Number of times we've attempted to Sync this folder and seen a response.
        public int SyncAttemptCount { get; set; }
        // Updated when a Sync response contains this folder.
        public DateTime LastSyncAttempt { get; set; }

        #region IMAP Folder metadata

        // Nacho Mail Code requires the McEmailMessage.ServerId to be unique across all folders. In IMAP,
        // the UID is unique only within a folder (otehr folders may have messages with the same UID).
        // In order to make the ServerId for a message unique, we create this ImapGuid, and prepend it to
        // the UID to create the McEmailMessgae.ServerId. See ImapProtoControl.ImapMessageFolderGuid(),
        // ImapProtoControl.ImapMessageUid() and ImapProtoControl.MessageServerId() for functions that
        // Convert to and from the McEmailMessage.ServerId to the various pieces of information.
        //
        // The ImapGuid will not change if the folder is moved or renamed.
        //
        // Technically this isn't part of the actual IMAP metadata (that the Imap Server provides),
        // but we'll treat it as such, since it really never should be modified by any code (other than
        // the McFolder initializer).
        public string ImapGuid { get; set; }

        // Whether the IMAP folder had the \NoSelect flag set. This means it can not be opened and will not have messages.
        public bool ImapNoSelect { get; set; }
        // The folder's UIDVALIDITY value
        public uint ImapUidValidity { get; set; }
        // the folders UIDNEXT value
        public uint ImapUidNext { get; set; }

        // the folders EXISTS value, i.e. total number of messages in the folder
        public int ImapExists { get; set; }

        #endregion

        #region IMAP Sync helper variables

        /// <summary>
        /// DateTime we last examined the folder.
        /// </summary>
        /// <value>DateTime we last examined the folder.</value>
        public DateTime ImapLastExamine { get; set; }

        /// <summary>
        /// Tells us that we need to start at the top and work our way down again. This usually gets set
        /// if we detect a new message or a change in HighestModSeq.
        /// </summary>
        /// <value><c>true</c> if imap need full sync; otherwise, <c>false</c>.</value>
        public bool ImapNeedFullSync { get; set; }

        /// <summary>
        /// The lowest UID we've synced in the current round of syncing
        /// </summary>
        /// <value>The lowest uid synced.</value>
        public uint ImapUidLowestUidSynced { get; set; }

        /// <summary>
        /// The highest UID we've synced in the current round of syncing
        /// </summary>
        /// <value>The highest uid synced.</value>
        public uint ImapUidHighestUidSynced { get; set; }

        /// <summary>
        /// The current sync-point in the current round of syncing
        /// </summary>
        /// <value>The last uid synced.</value>
        public uint ImapLastUidSynced { get; set; }

        /// <summary>
        /// The set of UID's we need to process as a string (UniqueIdSet.ToString(). Parse with TryParseUidSet())
        /// </summary>
        /// <value>The imap uid set.</value>
        public string ImapUidSet { get; set; }

        #endregion

        [Indexed]
        public string DisplayName { get; set; }

        [Indexed]
        public DateTime LastAccessed { get; set; }

        public int DisplayColor { get; set; }

        public bool IsDistinguished { get; set; }

        public Xml.FolderHierarchy.TypeCode Type { get; set; }

        // Client-owned distinguised folders.
        public const string ClientOwned_Outbox = "f6a01521-a763-4522-8a13-3df0545f4bdb";
        public const string ClientOwned_EmailDrafts = "568e8b03-3e40-468a-ab65-7a5c20e360c1";
        public const string ClientOwned_CalDrafts = "910bfa36-c005-4091-8053-39811ca43b03";
        public const string ClientOwned_GalCache = "246568ef-9747-4248-829f-577a03d27585";
        public const string ClientOwned_Gleaned = "ff34dbdb-07d2-4410-81e3-982af89ada6d";
        public const string ClientOwned_LostAndFound = "02b061f1-c074-425b-aa7e-d9b491d52a35";
        public const string ClientOwned_DeviceContacts = "161b18ba-c9fe-4421-930d-6f90655c21c6";
        public const string ClientOwned_DeviceCalendars = "a40af78a-583e-4b25-9e49-a341df6b3b4d";

        // Old names for the folders
        public const string ClientOwned_Outbox_Deprecated = "Outbox2";
        public const string ClientOwned_EmailDrafts_Deprecated = "EmailDrafts2";
        public const string ClientOwned_CalDrafts_Deprecated = "CalDrafts2";
        public const string ClientOwned_GalCache_Deprecated = "GAL";
        public const string ClientOwned_Gleaned_Deprecated = "GLEANED";
        public const string ClientOwned_LostAndFound_Deprecated = "LAF";
        public const string ClientOwned_DeviceContacts_Deprecated = "DEVCONTACTS";
        public const string ClientOwned_DeviceCalendars_Deprecated = "DEVCALENDARS";

        public const string GMail_All_ServerId = "Mail:^all";

        //Used for display name when creating the folder (if it doesn't already exist)"
        public const string DRAFTS_DISPLAY_NAME = "Drafts";
        public const string ARCHIVE_DISPLAY_NAME = "Archive";

        private static ConcurrentDictionary<int, string> JunkFolderIds = new ConcurrentDictionary<int, string> ();

        // A dictionary mapping account id to the RIC folder id of the account. (-1 if there is none locally)
        private static ConcurrentDictionary<int, int> RicFolderIds = new ConcurrentDictionary<int, int> ();

        public McFolder ()
        {
            ImapUidLowestUidSynced = uint.MaxValue;
            ImapLastUidSynced = uint.MinValue;
            ImapUidHighestUidSynced = uint.MinValue;
            ImapGuid = Guid.NewGuid ().ToString ("N");
        }

        public override string ToString ()
        {
            return "NcFolder: sid=" + ServerId + " pid=" + ParentId + " skey=" + AsSyncKey + " dn=" + DisplayName + " type=" + Type.ToString ();
        }

        public string ImapFolderNameRedacted ()
        {
            bool obfuscate = IsDistinguished || ServerId.StartsWith ("[Gmail]/") ? false : true;
            return string.Format ("{0}<{1}>", ImapGuid, !obfuscate ? ServerId : "User Folder");
        }

        public string ServerIdHashString ()
        {
            byte[] bytes = Encoding.UTF8.GetBytes (ServerId);
            SHA256Managed hashstring = new SHA256Managed ();
            byte[] hash = hashstring.ComputeHash (bytes);
            string hex = BitConverter.ToString (hash);
            return hex.Replace ("-", "");
        }

        // "factory" to create folders.
        public static McFolder Create (int accountId, 
                                       bool isClientOwned,
                                       bool isHidden,
                                       bool isDistinguished,
                                       string parentId,
                                       string serverId,
                                       string displayName,
                                       Xml.FolderHierarchy.TypeCode folderType)
        {
            // client-owned folder can't be created inside synced folder
            if (parentId != "0") {
                var parentFolder = McFolder.QueryByServerId<McFolder> (accountId, parentId);
                NcAssert.NotNull (parentFolder, "ParentId does not correspond to an existing folder");
                NcAssert.True (parentFolder.IsClientOwned == isClientOwned, "Child folder's isClientOwned field must match parent's field");
                NcAssert.True (parentFolder.AccountId == accountId, "Child folder's AccountId must match parent's AccountId");
            }

            if (isHidden) {
                NcAssert.True (isClientOwned, "Synced folders cannot be hidden");
            }

            var folder = new McFolder () {
                AsSyncKey = AsSyncKey_Initial,
                AsSyncMetaToClientExpected = false,
                AccountId = accountId,
                IsClientOwned = isClientOwned,
                IsHidden = isHidden,
                IsDistinguished = isDistinguished,
                ParentId = parentId,
                ServerId = serverId,
                DisplayName = displayName,
                Type = folderType,
            };
            return folder;
        }

        public override T UpdateWithOCApply<T> (Mutator mutator, out int count, int tries = 100)
        {
            if (IsHidden) {
                NcAssert.True (IsClientOwned, "Cannot update synced folders to be hidden");
            }
            return base.UpdateWithOCApply<T> (mutator, out count, tries);
        }

        public override T UpdateWithOCApply<T> (Mutator mutator, int tries = 100)
        {
            if (IsHidden) {
                NcAssert.True (IsClientOwned, "Cannot update synced folders to be hidden");
            }
            return base.UpdateWithOCApply<T> (mutator, tries);
        }

        public override int Update ()
        {
            NcAssert.True (false, "Must use UpdateWithOCApply.");
            return 0;
        }

        public static List<McFolder> GetClientOwnedFolders (string serverId)
        {
            return NcModel.Instance.Db.Table<McFolder> ().Where (x => 
                serverId == x.ServerId &&
            true == x.IsClientOwned).ToList ();
        }

        public static McFolder GetClientOwnedFolder (int accountId, string serverId)
        {
            return NcModel.Instance.Db.Table<McFolder> ().Where (x => 
                accountId == x.AccountId &&
            serverId == x.ServerId &&
            true == x.IsClientOwned).SingleOrDefault ();
        }

        /*
         * SYNCED FOLDERS:
         * Folder Get...Folder functions for distinguished folders that aren't going to 
         * Be destroyed after creation. Server-end and App-end code can BOTH use them.
         * Othere than these, Server-end code is restricted to using ServerEndQuery... or
         * ServerEndGet... functions, and App-end code is prohibited from using them: XOR!
         * CLIENT-OWNED FOLDERS:
         * Any code can use them.
         */
        public static McFolder GetDeviceContactsFolder ()
        {
            return McFolder.GetClientOwnedFolder (McAccount.GetDeviceAccount ().Id, ClientOwned_DeviceContacts);
        }

        public static McFolder GetDeviceCalendarsFolder ()
        {
            return McFolder.GetClientOwnedFolder (McAccount.GetDeviceAccount ().Id, ClientOwned_DeviceCalendars);
        }

        public static McFolder GetClientOwnedOutboxFolder (int accountId)
        {
            return McFolder.GetClientOwnedFolder (accountId, ClientOwned_Outbox);
        }

        public static McFolder GetClientOwnedDraftsFolder (int accountId)
        {
            return McFolder.GetClientOwnedFolder (accountId, ClientOwned_EmailDrafts);
        }

        public static List<McFolder> GetClientOwnedDraftsFolders ()
        {
            return McFolder.GetClientOwnedFolders (ClientOwned_EmailDrafts);
        }

        public static McFolder GetCalDraftsFolder (int accountId)
        {
            return McFolder.GetClientOwnedFolder (accountId, ClientOwned_CalDrafts);
        }

        public static McFolder GetGalCacheFolder (int accountId)
        {
            return McFolder.GetClientOwnedFolder (accountId, ClientOwned_GalCache);
        }

        public static McFolder GetGleanedFolder (int accountId)
        {
            return McFolder.GetClientOwnedFolder (accountId, ClientOwned_Gleaned);
        }

        public static McFolder GetLostAndFoundFolder (int accountId)
        {
            return McFolder.GetClientOwnedFolder (accountId, ClientOwned_LostAndFound);
        }

        public bool IsClientOwnedDraftsFolder ()
        {
            if (NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12 == this.Type) {
                if (ClientOwned_EmailDrafts == this.ServerId) {
                    return true;
                }
            }
            return false;
        }

        public bool IsClientOwnedOutboxFolder ()
        {
            if (NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12 == this.Type) {
                if (ClientOwned_Outbox == this.ServerId) {
                    return true;
                }
            }
            return false;
        }

        public bool IsJunkFolder ()
        {
            return JunkFolderIds.ContainsKey (this.Id);
        }

        public static bool IsJunkFolder (int folderId)
        {
            return JunkFolderIds.ContainsKey (folderId);
        }

        public static List<McFolder> GetUserFolders (int accountId, Xml.FolderHierarchy.TypeCode typeCode, string parentId, string name)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " likelihood (f.AccountId = ?, 1.0) AND " +
                          " likelihood (f.IsAwaitingDelete = 0, 1.0) AND " +
                          " likelihood (f.Type = ?, 0.2) AND " +
                          " likelihood (f.ParentId = ?, 0.05) AND " +
                          " likelihood (f.DisplayName = ?, 0.05) ",
                              accountId, (uint)typeCode, parentId, name);
            return folders.ToList ();
        }

        public static McFolder GetDistinguishedFolder (int accountId, Xml.FolderHierarchy.TypeCode typeCode)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " likelihood (f.AccountId = ?, 1.0) AND " +
                          " likelihood (f.IsAwaitingDelete = 0, 1.0) AND " +
                          " likelihood (f.IsClientOwned = 0, 1.0) AND " +
                          " likelihood (f.Type = ?, 0.05) ",
                              accountId, (uint)typeCode);
            if (0 == folders.Count) {
                return null;
            }
            NcAssert.True (1 == folders.Count);
            return folders.First ();
        }

        public int CountOfAllItems (McAbstrFolderEntry.ClassCodeEnum classCode)
        {
            return CountOfAllItems (AccountId, Id, classCode);
        }

        public static int CountOfAllItems (int accountId, int folderId, McAbstrFolderEntry.ClassCodeEnum classCode)
        {
            return NcModel.Instance.Db.ExecuteScalar<int> (
                "SELECT COUNT(*) FROM McEmailMessage AS e " +
                "JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                "WHERE " +
                " likelihood (e.AccountId = ?, 1.0)  AND " +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (m.AccountId = ?, 1.0) AND " +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (m.FolderId = ?, 0.05)",
                accountId, accountId, classCode, folderId);
        }

        public static McFolder GetDefaultDeletedFolder (int accountId)
        {
            return GetDistinguishedFolder (accountId, Xml.FolderHierarchy.TypeCode.DefaultDeleted_4);
        }

        public static McFolder GetRicContactFolder (int accountId)
        {
            return GetDistinguishedFolder (accountId, Xml.FolderHierarchy.TypeCode.Ric_19);
        }

        public static McFolder GetDefaultInboxFolder (int accountId)
        {
            return GetDistinguishedFolder (accountId, Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
        }

        public static McFolder GetDefaultCalendarFolder (int accountId)
        {
            return GetDistinguishedFolder (accountId, Xml.FolderHierarchy.TypeCode.DefaultCal_8);
        }

        public static McFolder GetDefaultContactFolder (int accountId)
        {
            return GetDistinguishedFolder (accountId, Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
        }

        public static McFolder GetDefaultTaskFolder (int accountId)
        {
            return GetDistinguishedFolder (accountId, Xml.FolderHierarchy.TypeCode.DefaultTasks_7);
        }

        public static McFolder GetDefaultSentFolder (int accountId)
        {
            return GetDistinguishedFolder (accountId, Xml.FolderHierarchy.TypeCode.DefaultSent_5);
        }

        public static McFolder GetOrCreateArchiveFolder (int accountId)
        {
            McFolder archiveFolder = McFolder.GetUserFolders (accountId, Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, "0", ARCHIVE_DISPLAY_NAME).FirstOrDefault ();
            if (null == archiveFolder) {
                archiveFolder = McFolder.GetUserFolders (accountId, Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1, "0", ARCHIVE_DISPLAY_NAME).FirstOrDefault ();
            }
            if (null == archiveFolder) {
                BackEnd.Instance.CreateFolderCmd (accountId, ARCHIVE_DISPLAY_NAME, Xml.FolderHierarchy.TypeCode.UserCreatedMail_12);
                archiveFolder = McFolder.GetUserFolders (accountId, Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, "0", ARCHIVE_DISPLAY_NAME).FirstOrDefault ();
            }
            NcAssert.NotNull (archiveFolder);
            return archiveFolder;
        }

        public static int GetRicFolderId (int accountId)
        {
            int folderId;
            if (!RicFolderIds.TryGetValue (accountId, out folderId)) {
                var ricFolder = GetRicContactFolder (accountId);
                if (null == ricFolder) {
                    return -1;
                }
                folderId = ricFolder.Id;
                RicFolderIds.TryAdd (accountId, folderId);
            }
            return folderId;
        }

        public static List<McFolder> QueryByParentId (int accountId, string parentId)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " likelihood (f.AccountId = ?, 1.0) AND " +
                          " likelihood (f.IsAwaitingDelete = 0, 1.0) AND " +
                          " likelihood (f.ParentId = ?, 0.05) ",
                              accountId, parentId);
            return folders.ToList ();
        }

        public static List<McFolder> QueryByMostRecentlyAccessedVisibleFolders (int accountId)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f " +
                          "WHERE f.AccountId = ? AND f.LastAccessed > ? AND f.IsHidden = 0 " +
                          "ORDER BY f.LastAccessed DESC", accountId, DateTime.UtcNow.AddYears (-1));
            return folders.ToList ();
        }

        public static List<McFolder> QueryNonHiddenFoldersOfType (int accountId, Xml.FolderHierarchy.TypeCode[] types)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f " +
                          " WHERE f.AccountId = ? AND " +
                          " f.IsAwaitingDelete = 0 AND " +
                          " f.Type IN " + Folder_Helpers.TypesToCommaDelimitedString (types) + " AND " +
                          " f.IsHidden = 0 " +
                          " ORDER BY f.DisplayName ", 
                              accountId);
            return folders.ToList ();
        }

        public static McFolder QueryByServerId (int accountId, string serverId)
        {
            var f = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                    " likelihood (f.AccountId = ?, 1.0) AND " +
                    " likelihood (f.IsAwaitingDelete = 0, 1.0) AND " +
                    " likelihood (f.IsHidden = 0, 0.9) AND " +
                    " likelihood (f.ServerId = ?, 0.5) ",
                        accountId, serverId).ToList ();
            NcAssert.True (2 > f.Count ());
            return f.SingleOrDefault ();
        }

        public static List<McFolder> QueryVisibleChildrenOfParentId (int accountId, string parentId)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND " +
                          " f.IsHidden = 0 AND " +
                          " f.IsAwaitingDelete = 0 AND " +
                          " f.ParentId = ? ",
                              accountId, parentId);
            return folders.ToList ();
        }

        public static List<McFolder> ServerEndQueryByParentId (int accountId, string parentId)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " likelihood (f.AccountId = ?, 1.0) AND " +
                          " likelihood (f.IsAwaitingCreate = 0, 1.0) AND " +
                          " likelihood (f.ParentId = ?, 0.05) ",
                              accountId, parentId);
            return folders.ToList ();
        }

        public static List<McFolder> QueryByFolderEntryId<T> (int accountId, int folderEntryId) where T : McAbstrFolderEntry, new()
        {
            var classCode = new T ().GetClassCode ();
            return NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f JOIN McMapFolderFolderEntry AS m ON f.Id = m.FolderId WHERE " +
            " likelihood (m.AccountId = ?, 1.0) AND " +
            " likelihood (m.FolderEntryId = ?, 0.001) AND " +
            " likelihood (f.IsAwaitingDelete = 0, 1.0) AND " +
            " likelihood (m.ClassCode = ?, 0.2) ",
                accountId, folderEntryId, (uint)classCode).ToList ();
        }

        public static List<McFolder> QueryByIsClientOwned (int accountId, bool isClientOwned)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " likelihood (f.AccountId = ?, 1.0) AND " +
                          " likelihood (f.IsAwaitingDelete = 0, 1.0) AND " +
                          " likelihood (f.IsClientOwned = ?, 0.2) ",
                              accountId, isClientOwned);
            return folders.ToList ();
        }

        // ONLY TO BE USED BY SERVER-END CODE.
        // ServerEndQueryXxx differs from QueryXxx in that it includes IsAwatingDelete folders and excludes
        // IsAwaitingCreate folders.

        public static McFolder ServerEndQueryByServerId (int accountId, string serverId)
        {
            return NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
            " likelihood (f.AccountId = ?, 1.0) AND " +
            " likelihood (f.IsAwaitingCreate = 0, 1.0) AND " +
            " likelihood (f.ServerId = ?, 0.05) ", 
                accountId, serverId).SingleOrDefault ();
        }

        public static List<McFolder> ServerEndQueryAll (int accountId)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " likelihood (f.AccountId = ?, 1.0) AND " +
                          " likelihood (f.IsClientOwned = 0, 0.8) AND " +
                          " likelihood (f.IsAwaitingCreate = 0, 1.0) ",
                              accountId);
            return folders.ToList ();
        }

        public static McFolder ServerEndQueryById (int folderId)
        {
            return NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
            " likelihood (f.Id = ?, 0.05) AND " +
            " likelihood (f.IsAwaitingCreate = 0, 1.0) ", folderId).SingleOrDefault ();
        }

        public static void ServerEndMoveToClientOwned (int accountId, string serverId, string destParentId)
        {
            var destFolder = GetClientOwnedFolder (accountId, destParentId);
            NcAssert.NotNull (destFolder, "Destination folder should exist");

            var potentialFolder = ServerEndQueryByServerId (accountId, serverId);
            NcAssert.NotNull (potentialFolder, "Server to move should exist");

            NcAssert.False (potentialFolder.IsClientOwned, "Folder to be moved should be synced");
            NcAssert.False (potentialFolder.IsDistinguished, "Folder to be moved must not be distinguished");

            // TODO: we should also give new ServerId values to everything. It should not matter.
            // But a bug that makes it matter would be hard to fix!
            potentialFolder = potentialFolder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.IsClientOwned = true;
                target.ParentId = destParentId;
                return true;
            });
            
            RecursivelyChangeFlags (accountId, potentialFolder.ServerId);
        }

        // change all isClientOwned flags for folders in a directory to true;
        private static void RecursivelyChangeFlags (int accountId, string parentServerId)
        {
            var children = McFolder.ServerEndQueryByParentId (accountId, parentServerId);
            foreach (McFolder iterChild in children) {
                var child = iterChild;
                child = child.UpdateSet_IsClientOwned (true);
                RecursivelyChangeFlags (accountId, child.ServerId);
            }
        }

        public void PerformSyncEpochScrub (bool testRunSync = false)
        {
            const int perIter = 100;
            Action action = () => {
                Log.Info (Log.LOG_AS, "PerformSyncEpochScrub {0}", Id);
                while (true) {
                    var orphanedEmails = McEmailMessage.QueryOldEpochByFolderId<McEmailMessage> (AccountId, Id, AsSyncEpoch, perIter);
                    var orphanedCals = McCalendar.QueryOldEpochByFolderId<McCalendar> (AccountId, Id, AsSyncEpoch, perIter);
                    var orphanedContacts = McContact.QueryOldEpochByFolderId<McContact> (AccountId, Id, AsSyncEpoch, perIter);
                    var orphanedTasks = McTask.QueryOldEpochByFolderId<McTask> (AccountId, Id, AsSyncEpoch, perIter);
                    var whackem = new List<McAbstrItem> ();
                    whackem.AddRange (orphanedEmails);
                    whackem.AddRange (orphanedCals);
                    whackem.AddRange (orphanedContacts);
                    whackem.AddRange (orphanedTasks);
                    if (0 == whackem.Count) {
                        break;
                    }
                    foreach (var item in whackem) {
                        item.Delete ();
                    }
                }
                UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.AsSyncEpochScrubNeeded = false;
                    return true;
                });
                // after UpdateWithOCApply, "this" can be a stale version of the folder!
            };
            if (testRunSync) {
                action ();
            } else {
                NcTask.Run (action, "PerformSyncEpochScrub");
            }
        }

        public void DeleteItems ()
        {
            var contentMaps = McMapFolderFolderEntry.QueryByFolderId (AccountId, Id);
            foreach (var map in contentMaps) {
                map.Delete ();
                switch (map.ClassCode) {
                case McAbstrItem.ClassCodeEnum.Email:
                    var emailMessage = McAbstrFolderEntry.QueryById<McEmailMessage> (map.FolderEntryId);
                    if (null != emailMessage) {
                        emailMessage.Delete ();
                    }
                    break;

                case McAbstrItem.ClassCodeEnum.Calendar:
                    var cal = McAbstrFolderEntry.QueryById<McCalendar> (map.FolderEntryId);
                    if (null != cal) {
                        cal.Delete ();
                    }
                    break;

                case McAbstrItem.ClassCodeEnum.Contact:
                    var contact = McAbstrFolderEntry.QueryById<McContact> (map.FolderEntryId);
                    if (null != contact) {
                        contact.Delete ();
                    }
                    break;

                case McAbstrItem.ClassCodeEnum.Tasks:
                    var task = McAbstrFolderEntry.QueryById<McTask> (map.FolderEntryId);
                    if (null != task) {
                        task.Delete ();
                    }
                    break;

                case McAbstrItem.ClassCodeEnum.Folder:
                    NcAssert.True (false);
                    break;

                default:
                    NcAssert.True (false);
                    break;
                }
            }
        }

        public override int Insert ()
        {
            using (var capture = CaptureWithStart ("Insert")) {
                int result = 0;
                NcModel.Instance.RunInTransaction (() => {
                    // If this is a calendar folder, give it a unique index that can be used to give it a color.
                    // This doesn't seem like the right place for this code.  McFolder.Insert() shouldn't have
                    // code that is specific to calendar folders.  But on the other hand, McFolder.Insert() is
                    // the only place that the code can go that guarantees that DisplayColor is set and that its
                    // value is unique.
                    if (NachoFolders.FilterForCalendars.Contains (this.Type) && 0 == DisplayColor) {
                        // This code will work even if the app UI allows the user to select the color for a folder,
                        // which could result in a gap in the index numbers.  The next folder to be created will
                        // start filling in the gap.  That is why we don't just look for the largest existing index.
                        int nextColor = 1;
                        var calFolders = NcModel.Instance.Db.Query<McFolder> (
                                             "SELECT f.* FROM McFolder AS f " +
                                             " WHERE f.Type IN " + Folder_Helpers.TypesToCommaDelimitedString (NachoFolders.FilterForCalendars) +
                                             " ORDER BY f.DisplayColor ");
                        foreach (var folder in calFolders) {
                            if (nextColor == folder.DisplayColor) {
                                ++nextColor;
                            } else if (nextColor != folder.DisplayColor + 1) {
                                break;
                            }
                        }
                        this.DisplayColor = nextColor;
                    }
                    // Make sure there's no other folder with the same ServerId in this account.
                    if (McFolder.QueryByServerIdMult<McFolder> (AccountId, ServerId).Any ()) {
                        throw new ArgumentException ("Trying to insert duplicate serverid");
                    }
                    result = base.Insert ();
                    if (MaybeJunkFolder (DisplayName)) {
                        JunkFolderIds.TryAdd (Id, DisplayName);
                    }
                });
                return result;
            }
        }

        public override int Delete ()
        {
            using (var capture = CaptureWithStart ("Delete")) {
                // Delete anything in the folder and any sub-folders/map entries (recursively).
                DeleteItems ();

                // Delete any sub-folders.
                var subs = McFolder.QueryByParentId (AccountId, ServerId);
                foreach (var sub in subs) {
                    // Recusion.
                    sub.Delete ();
                }
                int rows = base.Delete ();

                string dummy;
                JunkFolderIds.TryRemove (Id, out dummy);
                if (Xml.FolderHierarchy.TypeCode.Ric_19 == Type) {
                    int folderId;
                    RicFolderIds.TryRemove (AccountId, out folderId);
                }

                return rows;
            }
        }

        public NcResult UpdateLink (McAbstrItem obj)
        {
            var classCode = obj.GetClassCode ();
            NcAssert.True (classCode != ClassCodeEnum.Folder, "Linking folders is not currently supported");
            NcAssert.True (classCode != ClassCodeEnum.NeverInFolder);
            NcAssert.True (AccountId == obj.AccountId, "Folder's AccountId should match FolderEntry's AccountId");
            var existing = McMapFolderFolderEntry.QueryByFolderIdFolderEntryIdClassCode 
                (AccountId, Id, obj.Id, classCode);
            if (null == existing) {
                return NcResult.Error (NcResult.SubKindEnum.Error_NotInFolder);
            }
            if (existing.AsSyncEpoch != AsSyncEpoch) {
                existing.AsSyncEpoch = AsSyncEpoch;
                existing.Update ();
            }
            return NcResult.OK ();
        }

        public NcResult Link (McAbstrItem obj)
        {
            var classCode = obj.GetClassCode ();
            NcAssert.True (classCode != ClassCodeEnum.Folder, "Linking folders is not currently supported");
            NcAssert.True (classCode != ClassCodeEnum.NeverInFolder);
            NcAssert.True (AccountId == obj.AccountId, "Folder's AccountId should match FolderEntry's AccountId");
            var existing = McMapFolderFolderEntry.QueryByFolderIdFolderEntryIdClassCode 
                (AccountId, Id, obj.Id, classCode);
            if (null != existing) {
                return NcResult.Error (NcResult.SubKindEnum.Error_AlreadyInFolder);
            }
            var map = new McMapFolderFolderEntry (AccountId) {
                FolderId = Id,
                FolderEntryId = obj.Id,
                ClassCode = classCode,
                AsSyncEpoch = AsSyncEpoch,
            };
            NcModel.Instance.RunInTransaction (() => {
                map.Insert ();

                // if it is a contact, re-evaluate the eclipsing status
                if (obj is McContact) {
                    var contact = (McContact)obj;
                    contact.Update ();
                }
            });
            return NcResult.OK ();
        }

        public static NcResult UnlinkAll (McAbstrItem obj)
        {
            var classCode = obj.GetClassCode ();
            if (ClassCodeEnum.NeverInFolder == classCode) {
                return NcResult.OK ();
            }
            var maps = McMapFolderFolderEntry.QueryByFolderEntryIdClassCode (obj.AccountId, obj.Id, classCode);
            foreach (var map in maps) {
                map.Delete ();
            }
            return NcResult.OK ();
        }

        public NcResult Unlink (McAbstrItem obj)
        {
            var classCode = obj.GetClassCode ();
            return Unlink (obj.Id, classCode);
        }

        public NcResult Unlink (int feId, ClassCodeEnum classCode)
        {
            var existing = McMapFolderFolderEntry.QueryByFolderIdFolderEntryIdClassCode 
                (AccountId, Id, feId, classCode);
            if (null == existing) {
                return NcResult.Error (NcResult.SubKindEnum.Error_NotInFolder);
            }
            existing.Delete ();
            return NcResult.OK ();
        }

        public static void UpdateSet_AsSyncMetaToClientExpected (int accountId, bool toClientExpected)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND f.IsClientOwned = 0",
                              accountId);
            foreach (var folder in folders) {
                folder.UpdateSet_AsSyncMetaToClientExpected (toClientExpected);
            }
        }

        public McFolder UpdateReset_AsSyncFailRun ()
        {
            var folder = UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.AsSyncFailRun = 0;
                return true;
            });
            return folder;
        }

        public McFolder UpdateIncrement_AsSyncFailRunToClientExpected (bool toClientExpected)
        {
            var folder = UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.AsSyncFailRun++;
                target.AsSyncMetaToClientExpected = toClientExpected;
                return true;
            });
            return folder;
        }

        public McFolder UpdateSet_AsSyncMetaToClientExpected (bool toClientExpected)
        {
            var folder = UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.AsSyncMetaToClientExpected = toClientExpected;
                return true;
            });
            return folder;
        }

        public McFolder UpdateSet_IsAwaitingDelete (bool isAwaitingDelete)
        {
            var folder = UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.IsAwaitingDelete = isAwaitingDelete;
                return true;
            });
            return folder;
        }

        public McFolder UpdateSet_IsAwaitingCreate (bool isAwaitingCreate)
        {
            var folder = UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.IsAwaitingCreate = isAwaitingCreate;
                return true;
            });
            return folder;
        }

        public McFolder UpdateSet_IsClientOwned (bool isClientOwned)
        {
            var folder = UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                if (!isClientOwned) {
                    NcAssert.True (!target.IsHidden, "Cannot update synced folders to be hidden");
                }
                target.IsClientOwned = isClientOwned;
                return true;
            });
            return folder;
        }

        public McFolder UpdateSet_IsHidden (bool isHidden)
        {
            var folder = UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                if (isHidden) {
                    NcAssert.True (target.IsClientOwned, "Cannot update synced folders to be hidden");
                }
                target.IsHidden = isHidden;
                return true;
            });
            return folder;
        }

        public McFolder UpdateSet_ParentId (string parentId)
        {
            var folder = UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.ParentId = parentId;
                return true;
            });
            return folder;
        }

        public McFolder UpdateSet_ServerId (string serverId)
        {
            var folder = UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.ServerId = serverId;
                return true;
            });
            return folder;
        }

        public McFolder UpdateSet_DisplayName (string displayName)
        {
            var folder = UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.DisplayName = displayName;
                return true;
            });
            return folder;
        }

        public McFolder UpdateSet_AsSyncLastPing (DateTime asSyncLastPing)
        {
            var folder = UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.AsSyncLastPing = asSyncLastPing;
                return true;
            });
            return folder;
        }

        public McFolder UpdateSet_LastAccessed (DateTime lastAccessed)
        {
            var folder = UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.LastAccessed = lastAccessed;
                return true;
            });
            return folder;
        }

        public void ResetSyncState ()
        {
            AsSyncKey = AsSyncKey_Initial;
            AsSyncMetaToClientExpected = true;
        }

        // TODO - would be nice to let the user see old folder contents (read-only) until next sync comes in.
        public McFolder UpdateResetSyncState ()
        {
            var folder = UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.ResetSyncState ();
                target.AsSyncEpoch++;
                target.AsSyncEpochScrubNeeded = true;
                return true;
            });
            return folder;
        }

        public static void UpdateResetSyncState (int accountId)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? ",
                              accountId);
            foreach (var iterFolder in folders) {
                iterFolder.UpdateResetSyncState ();
            }
        }

        public override ClassCodeEnum GetClassCode ()
        {
            return McAbstrFolderEntry.ClassCodeEnum.Folder;
        }

        public static List<McFolder> SearchFolders (int accountId, string searchFor)
        {
            if (String.IsNullOrEmpty (searchFor)) {
                return new List<McFolder> ();
            } 

            searchFor = "%" + searchFor + "%";
            // FIXME - double-check this query.
            return NcModel.Instance.Db.Query<McFolder> (
                "SELECT f.* FROM McFolder AS f" +
                "WHERE " +
                "f.IsAwaitingDelete = 0 AND " +
                "f.AccountId = ? AND " +
                "f.DisplayName LIKE ? " +
                "ORDER BY f.DisplayName DESC",
                accountId, searchFor);
        }

        public static List<McFolder> SearchFolders (string searchFor)
        {
            if (String.IsNullOrEmpty (searchFor)) {
                return new List<McFolder> ();
            } 

            searchFor = "%" + searchFor + "%";

            return NcModel.Instance.Db.Query<McFolder> (
                "SELECT f.* FROM McFolder AS f" +
                "WHERE " +
                "f.IsAwaitingDelete = 0 AND " +
                "f.DisplayName LIKE ? " +
                "ORDER BY f.DisplayName DESC",
                searchFor);
        }

        public static bool MaybeJunkFolder (string folderName)
        {
            // TODO - This is pretty hokey. But there is no TypeCode for junk folder.
            List<string> tags = new List<string> ();
            tags.Add ("junk");
            tags.Add ("spam");
            tags.Add ("bulk mail"); // Yahoo uses this as junk mail folder
            return FolderMatchesStringList (folderName, tags);
        }

        public static bool MaybeSentFolder (McAccount.AccountServiceEnum serviceType, string folderName)
        {
            List<string> tags = new List<string> ();
            switch (serviceType) {
            case McAccount.AccountServiceEnum.iCloud:
                //* LIST (\HasNoChildren) "/" "Sent Messages"
                // No special Use flag.
                tags.Add ("Sent Messages");
                break;

            case McAccount.AccountServiceEnum.Yahoo:
                //* LIST (\HasNoChildren) "/" "Sent"
                // No special Use flag.
                tags.Add ("Sent");
                break;

            case McAccount.AccountServiceEnum.GoogleDefault:
                //* LIST (\HasNoChildren \Sent) "/" "[Gmail]/Sent Mail"
                // Chances are we won't get here if the caller pays proper
                // attention to the special use flags (i.e. \Sent).
                tags.Add ("[Gmail]/Sent Mail");
                break;

            case McAccount.AccountServiceEnum.HotmailDefault:
            case McAccount.AccountServiceEnum.Aol:
                //* LIST (\HasNoChildren \Sent) "/" "Sent"
                // Chances are we won't get here if the caller pays proper
                // attention to the special use flags (i.e. \Sent).
                tags.Add ("Sent");
                break;

            default:
                tags.Add ("Sent");
                break;
            }
            return FolderMatchesStringList (folderName, tags);
        }

        public static bool MaybeNotesFolder (McAccount.AccountServiceEnum serviceType, string folderName)
        {
            List<string> tags = new List<string> ();
            tags.Add ("Notes");
            return FolderMatchesStringList (folderName, tags);
        }

        public static bool MaybeTrashFolder (McAccount.AccountServiceEnum serviceType, string folderName)
        {
            List<string> tags = new List<string> ();
            switch (serviceType) {
            case McAccount.AccountServiceEnum.iCloud:
                //* LIST (\HasNoChildren) "/" "Deleted Messages"
                tags.Add ("Deleted Messages");
                break;

            case McAccount.AccountServiceEnum.GoogleDefault:
                //* LIST (\HasNoChildren \Trash) "/" "[Gmail]/Trash"
                tags.Add ("[Gmail]/Trash");
                break;

            default:
                tags.Add ("Trash");
                break;
            }
            return FolderMatchesStringList (folderName, tags);
        }

        public static bool MaybeDraftFolder (McAccount.AccountServiceEnum serviceType, string folderName)
        {
            List<string> tags = new List<string> ();
            switch (serviceType) {
            case McAccount.AccountServiceEnum.iCloud:
                //* LIST (\HasNoChildren) "/" Drafts
                // No special Use flag.
                tags.Add ("Drafts");
                break;

            case McAccount.AccountServiceEnum.Yahoo:
                //* LIST (\HasNoChildren) "/" "Draft"
                // No special Use flag.
                tags.Add ("Draft");
                break;

            case McAccount.AccountServiceEnum.GoogleDefault:
                //* LIST (\Drafts \HasNoChildren) "/" "[Gmail]/Drafts"
                // Chances are we won't get here if the caller pays proper
                // attention to the special use flags (i.e. \Sent).
                tags.Add ("[Gmail]/Drafts");
                break;

            case McAccount.AccountServiceEnum.HotmailDefault:
            case McAccount.AccountServiceEnum.Aol:
                //* LIST (\HasNoChildren \Drafts) "/" Drafts
                //* LIST (\HasNoChildren \Drafts) "/" "Drafts"
                // Chances are we won't get here if the caller pays proper
                // attention to the special use flags (i.e. \Sent).
                tags.Add ("Drafts");
                break;

            default:
                tags.Add ("Drafts");
                break;
            }
            return FolderMatchesStringList (folderName, tags);
        }


        private static bool FolderMatchesStringList (string folderName, List<string> tags)
        {
            NcAssert.True (tags.Any ());
            var folderLower = folderName.ToLowerInvariant ();
            foreach (var tag in tags) {
                if (folderLower.Contains (tag.ToLowerInvariant ())) {
                    return true;
                }
            }
            return false;
        }

        public static void InitializeJunkFolders ()
        {
            ConcurrentDictionary<int, string> junkFolderIds = new ConcurrentDictionary<int, string> ();
            foreach (var folder in NcModel.Instance.Db.Table<McFolder> ()) {
                if (NcTask.Cts.Token.IsCancellationRequested) {
                    JunkFolderIds = junkFolderIds; // keep what we have so far
                    NcTask.Cts.Token.ThrowIfCancellationRequested ();
                }
                if (MaybeJunkFolder (folder.DisplayName)) {
                    junkFolderIds.TryAdd (folder.Id, folder.DisplayName);
                }
            }
            JunkFolderIds = junkFolderIds;
        }

        public static string JunkFolderListSqlString ()
        {
            if (0 == JunkFolderIds.Count) {
                return null;
            }
            return "(" + string.Join (",", JunkFolderIds.Keys) + ")";
        }

        public static string GleaningExemptedFolderListSqlString ()
        {
            var folderList = new List<int> ();
            if (0 < JunkFolderIds.Count) {
                folderList.AddRange (JunkFolderIds.Keys);
            }
            var draftFolders = GetClientOwnedDraftsFolders ();
            if (0 < draftFolders.Count) {
                folderList.AddRange (draftFolders.Select (x => x.Id));
            }
            if (0 == folderList.Count) {
                return null;
            }
            return "(" + string.Join (",", folderList) + ")";
        }

        public const int HOT_FAKE_FOLDER_ID = -1;
        public const int LTR_FAKE_FOLDER_ID = -2;
        public const int DEFERRED_FAKE_FOLDER_ID = -3;
        public const int DEADLINE_FAKE_FOLDER_ID = -4;
        public const int INBOX_FAKE_FOLDER_ID = -5;

        public static McFolder GetHotFakeFolder ()
        {
            return  new McFolder () {
                Id = HOT_FAKE_FOLDER_ID,
                DisplayName = "Hot List",
            };
        }

        public static McFolder GetLtrFakeFolder ()
        {
            return new McFolder () {
                Id = LTR_FAKE_FOLDER_ID,
                DisplayName = "Focused",
            };
        }

        public static McFolder GetDeferredFakeFolder ()
        {
            return new McFolder () {
                Id = DEFERRED_FAKE_FOLDER_ID,
                DisplayName = "Deferred",
            };
        }

        public static McFolder GetDeadlineFakeFolder ()
        {
            return new McFolder () {
                Id = DEADLINE_FAKE_FOLDER_ID,
                DisplayName = "Deadline",
            };
        }

        public static McFolder GetInboxFakeFolder ()
        {
            return new McFolder () {
                Id = INBOX_FAKE_FOLDER_ID,
                DisplayName = "Inbox",
            };
        }
    }
}

