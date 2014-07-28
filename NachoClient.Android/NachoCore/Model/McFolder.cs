using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using NachoCore.Utils;
using NachoCore.ActiveSync;

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

        [Indexed]
        public bool IsAwaitingCreate { get; set; }

        public const string AsSyncKey_Initial = "0";
        public const string AsRootServerId = "0";

        public string AsSyncKey { get; set; }

        public uint AsFolderSyncEpoch { get; set; }
        // AsSyncMeta ONLY to be manipulated by sync strategy class.
        // AsSyncMetaToClientExpected true when we have a reason to believe that we're not synced up.
        public bool AsSyncMetaToClientExpected { get; set; }
        // AsSyncMetaDoGetChanges true when strategy decides we should GetChanges for this folder.
        public bool AsSyncMetaDoGetChanges { get; set; }
        // When we GetChanges, AsSyncMetaFilterCode tells the date-range.
        public Xml.Provision.MaxAgeFilterCode AsSyncMetaFilterCode { get; set; }
        // When we GetChanges, AsSyncMetaWindowSize tells the number-of-messages-window.
        public uint AsSyncMetaWindowSize { get; set; }
        // Updated when a Sync works on this folder. When we hit MaxFolders limit, this decides who goes next.
        public DateTime AsSyncLastPing { get; set; }

        [Indexed]
        public string DisplayName { get; set; }

        public int DisplayColor { get; set; }

        public Xml.FolderHierarchy.TypeCode Type { get; set; }
        // Client-owned distinguised folders.
        public const string ClientOwned_Outbox = "Outbox2";
        public const string ClientOwned_Drafts = "Drafts2";
        public const string ClientOwned_GalCache = "GAL";
        public const string ClientOwned_Gleaned = "GLEANED";
        public const string ClientOwned_LostAndFound = "LAF";
        public const string ClientOwned_DeviceContacts = "DEVCONTACTS";

        public override string ToString ()
        {
            return "NcFolder: sid=" + ServerId + " pid=" + ParentId + " skey=" + AsSyncKey + " dn=" + DisplayName + " type=" + Type.ToString ();
        }
        // "factory" to create folders.
        public static McFolder Create (int accountId, 
                                       bool isClientOwned,
                                       bool isHidden,
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
                ParentId = parentId,
                ServerId = serverId,
                DisplayName = displayName,
                Type = folderType,
            };
            return folder;
        }

        public override int Update ()
        {
            if (IsHidden) {
                NcAssert.True (IsClientOwned, "Cannot update synced folders to be hidden");
            }

            int retval = base.Update ();
            return retval;
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
            var account = McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Device).Single ();
            return McFolder.GetClientOwnedFolder (account.Id, ClientOwned_DeviceContacts);
        }

        public static McFolder GetOutboxFolder (int accountId)
        {
            return McFolder.GetClientOwnedFolder (accountId, ClientOwned_Outbox);
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

        public static McFolder GetUserFolder (int accountId, Xml.FolderHierarchy.TypeCode typeCode, int parentId, string name)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND " +
                          " f.IsAwaitingDelete = 0 AND " +
                          " f.Type = ? AND " +
                          " f.ParentId = ? AND " +
                          " f.DisplayName = ?",
                              accountId, (uint)typeCode, parentId, name);
            if (0 == folders.Count) {
                return null;
            }
            NcAssert.True (1 == folders.Count);
            return folders.First ();
        }

        private static McFolder GetDistinguishedFolder (int accountId, Xml.FolderHierarchy.TypeCode typeCode)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND " +
                          " f.IsAwaitingDelete = 0 AND " +
                          " f.Type = ? ",
                              accountId, (uint)typeCode);
            if (0 == folders.Count) {
                return null;
            }
            NcAssert.True (1 == folders.Count);
            return folders.First ();
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

        public static List<McFolder> QueryByParentId (int accountId, string parentId)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND " +
                          " f.IsAwaitingDelete = 0 AND " +
                          " f.ParentId = ? ",
                              accountId, parentId);
            return folders.ToList ();
        }

        public static List<McFolder> ServerEndQueryByParentId (int accountId, string parentId)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND " +
                          " f.IsAwaitingCreate = 0 AND " +
                          " f.ParentId = ? ",
                              accountId, parentId);
            return folders.ToList ();
        }

        public static List<McFolder> QueryByFolderEntryId<T> (int accountId, int folderEntryId) where T : McAbstrFolderEntry
        {
            var getClassCode = typeof(T).GetMethod ("GetClassCode");
            NcAssert.True (null != getClassCode);
            ClassCodeEnum classCode = (ClassCodeEnum)getClassCode.Invoke (null, new object[]{ });
            return NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f JOIN McMapFolderFolderEntry AS m ON f.Id = m.FolderId WHERE " +
            " m.AccountId = ? AND " +
            " m.FolderEntryId = ? AND " +
            " f.IsAwaitingDelete = 0 AND " +
            " m.ClassCode = ? ",
                accountId, folderEntryId, (uint)classCode).ToList ();
        }

        public static List<McFolder> QueryClientOwned (int accountId, bool isClientOwned)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND " +
                          " f.IsAwaitingDelete = 0 AND " +
                          " f.IsClientOwned = ? ",
                              accountId, isClientOwned);
            return folders.ToList ();
        }
        // ONLY TO BE USED BY SERVER-END CODE.
        // ServerEndQueryXxx differs from QueryXxx in that it includes IsAwatingDelete folders and excludes
        // IsAwaitingCreate folders.

        public static McFolder ServerEndQueryByServerId (int accountId, string serverId)
        {
            return NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
            " f.AccountId = ? AND " +
            " f.IsAwaitingCreate = 0 AND " +
            " f.ServerId = ? ", 
                accountId, serverId).SingleOrDefault ();
        }

        public static List<McFolder> ServerEndQueryAll (int accountId)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND " +
                          " f.IsClientOwned = 0 AND " +
                          " f.IsAwaitingCreate = 0 ",
                              accountId);
            return folders.ToList ();
        }

        public static McFolder ServerEndQueryById (int folderId)
        {
            return NcModel.Instance.Db.Query<McFolder> ( "SELECT f.* FROM McFolder AS f WHERE " +
                    " f.Id = ? AND " +
                    " f.IsAwaitingCreate = 0 ", folderId).SingleOrDefault ();
        }

        public static void ServerEndMoveToClientOwned (int accountId, string serverId, string destParentId)
        {
            var destFolder = GetClientOwnedFolder (accountId, destParentId);
            NcAssert.NotNull (destFolder, "Destination folder should exist");

            var potentialFolder = ServerEndQueryByServerId (accountId, serverId);
            NcAssert.NotNull (potentialFolder, "Server to move should exist");

            NcAssert.True (potentialFolder.IsClientOwned == false, "Folder to be moved should be synced");
            potentialFolder.IsClientOwned = true;
            potentialFolder.ParentId = destParentId;
            potentialFolder.Update ();

            RecursivelyChangeFlags (accountId, potentialFolder.ServerId);
        }

        // change all isClientOwned flags for folders in a directory to true;
        private static void RecursivelyChangeFlags (int accountId, string parentServerId)
        {
            var children = McFolder.ServerEndQueryByParentId (accountId, parentServerId);
            foreach (McFolder child in children) {
                child.IsClientOwned = true;
                child.Update ();
                RecursivelyChangeFlags (accountId, child.ServerId);
            }
        }

        public override int Delete ()
        {
            // Delete anything in the folder and any sub-folders/map entries (recursively).
            var contentMaps = McMapFolderFolderEntry.QueryByFolderId (AccountId, Id);
            foreach (var map in contentMaps) {
                map.Delete ();
                switch (map.ClassCode) {
                case McAbstrItem.ClassCodeEnum.Email:
                    var emailMessage = McAbstrFolderEntry.QueryById<McEmailMessage> (map.FolderEntryId);
                    emailMessage.Delete ();
                    break;

                case McAbstrItem.ClassCodeEnum.Calendar:
                    var cal = McAbstrFolderEntry.QueryById<McCalendar> (map.FolderEntryId);
                    cal.Delete ();
                    break;

                case McAbstrItem.ClassCodeEnum.Contact:
                    var contact = McAbstrFolderEntry.QueryById<McContact> (map.FolderEntryId);
                    contact.Delete ();
                    break;

                case McAbstrItem.ClassCodeEnum.Tasks:
                    var task = McAbstrFolderEntry.QueryById<McTask> (map.FolderEntryId);
                    task.Delete ();
                    break;

                case McAbstrItem.ClassCodeEnum.Folder:
                    NcAssert.True (false);
                    break;

                default:
                    NcAssert.True (false);
                    break;
                }
            }
            // Delete any sub-folders.
            var subs = McFolder.QueryByParentId (AccountId, ServerId);
            foreach (var sub in subs) {
                // Recusion.
                sub.Delete ();
            }
            return base.Delete ();
        }

        private static ClassCodeEnum ClassCodeEnumFromObj (McAbstrFolderEntry obj)
        {
            var getClassCode = obj.GetType ().GetMethod ("GetClassCode");
            NcAssert.True (null != getClassCode);
            return (ClassCodeEnum)getClassCode.Invoke (null, new object[]{ });
        }

        public NcResult Link (McAbstrItem obj)
        {
            ClassCodeEnum classCode = ClassCodeEnumFromObj (obj);
            NcAssert.True (classCode != ClassCodeEnum.Folder, "Linking folders is not currently supported");
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
            };
            map.Insert ();
            return NcResult.OK ();
        }

        public static NcResult UnlinkAll (McAbstrItem obj)
        {
            ClassCodeEnum classCode = ClassCodeEnumFromObj (obj);
            var maps = McMapFolderFolderEntry.QueryByFolderEntryIdClassCode (obj.AccountId, obj.Id, classCode);
            foreach (var map in maps) {
                map.Delete ();
            }
            return NcResult.OK ();
        }

        public NcResult Unlink (McAbstrItem obj)
        {
            ClassCodeEnum classCode = ClassCodeEnumFromObj (obj);
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
        public static void AsSetExpected (int accountId)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND f.IsClientOwned = 0",
                              accountId);
            foreach (var folder in folders) {
                folder.AsSyncMetaToClientExpected = true;
                folder.Update ();
            }
        }

        public static void AsResetState (int accountId)
        {
            var folders = NcModel.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? ",
                              accountId);
            foreach (var folder in folders) {
                folder.AsSyncKey = AsSyncKey_Initial;
                folder.AsSyncMetaToClientExpected = true;
                folder.Update ();
            }
        }

        public static ClassCodeEnum GetClassCode ()
        {
            return McAbstrFolderEntry.ClassCodeEnum.Folder;
        }

        public static List<McFolder> SearchFolders(int accountId, string searchFor)
        {
            if (String.IsNullOrEmpty (searchFor)) {
                return new List<McFolder> ();
            } 

            searchFor = "%" + searchFor + "%";

            return NcModel.Instance.Db.Query<McFolder> (
                "SELECT f.* FROM McFolder AS f" +
                "WHERE " +
                "f.IsAwaitingDelete = 0 AND " +
                "f.AccountId = ? AND " +
                "f.DisplayName LIKE ? " +
                "ORDER BY f.DisplayName DESC",
                accountId, searchFor);
        }
    }
}

