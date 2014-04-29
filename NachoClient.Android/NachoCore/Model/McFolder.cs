using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using NachoCore.Utils;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public class McFolder : McFolderEntry
    {
        [Indexed]
        public bool IsClientOwned { get; set; }

        [Indexed]
        public bool IsHidden { get; set; }

        [Indexed]
        public string ParentId { get; set; }

        [Indexed]
        public bool IsAwaitingCreate { get; set; }

        [Indexed]
        public bool IsAwaitingDelete { get; set; }

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

        [Indexed]
        public string DisplayName { get; set; }

        public int DisplayColor { get; set; }

        public Xml.FolderHierarchy.TypeCode Type { get; set; }
        // Client-owned distinguised folders.
        public const string ClientOwned_Outbox = "Outbox2";
        public const string ClientOwned_GalCache = "GAL";
        public const string ClientOwned_Gleaned = "GLEANED";
        public const string ClientOwned_LostAndFound = "LAF";

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

        public static McFolder GetClientOwnedFolder (int accountId, string serverId)
        {
            return BackEnd.Instance.Db.Table<McFolder> ().SingleOrDefault (x => 
                accountId == x.AccountId &&
            serverId == x.ServerId &&
            true == x.IsClientOwned);
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
            var folders = BackEnd.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND " +
                          " f.Type = ? AND " +
                          " f.ParentId = ? AND " +
                          " f.DisplayName = ?",
                              accountId, (uint)typeCode, parentId, name);
            if (0 == folders.Count) {
                return null;
            }
            NachoAssert.True (1 == folders.Count);
            return folders.First ();
        }

        private static McFolder GetDistinguishedFolder (int accountId, Xml.FolderHierarchy.TypeCode typeCode)
        {
            var folders = BackEnd.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND " +
                          " f.Type = ? ",
                              accountId, (uint)typeCode);
            if (0 == folders.Count) {
                return null;
            }
            NachoAssert.True (1 == folders.Count);
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
            var folders = BackEnd.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND " +
                          " f.ParentId = ? ",
                              accountId, parentId);
            return folders.ToList ();
        }

        public static List<McFolder> QueryByFolderEntryId<T> (int accountId, int folderEntryId) where T : McFolderEntry
        {
            var getClassCode = typeof(T).GetMethod ("GetClassCode");
            NachoAssert.True (null != getClassCode);
            ClassCodeEnum classCode = (ClassCodeEnum)getClassCode.Invoke (null, new object[]{ });
            return BackEnd.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f JOIN McMapFolderFolderEntry AS m ON f.Id = m.FolderId WHERE " +
            " m.AccountId = ? AND " +
            " m.FolderEntryId = ? AND " +
            " f.IsAwaitingDelete = 0 AND " +
            " m.ClassCode = ? ",
                accountId, folderEntryId, (uint)classCode).ToList ();
        }

        public static List<McFolder> QueryClientOwned (int accountId, bool isClientOwned)
        {
            var folders = BackEnd.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND " +
                          " f.IsAwaitingDelete = 0 AND " +
                          " f.IsClientOwned = ? ",
                              accountId, isClientOwned);
            return folders.ToList ();
        }
        // ONLY TO BE USED BY SERVER-END CODE.
        // ServerEndQueryXxx differs from QueryXxx in that it includes IsAwatingDelete folders and excludes
        // IsAwaitingCreate folders.
        public static List<McFolder> ServerEndQueryAll (int accountId)
        {
            var folders = BackEnd.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          " f.AccountId = ? AND " +
                          " f.IsClientOwned = 0 AND " +
                          " f.IsAwaitingCreate = 0 ",
                              accountId);
            return folders.ToList ();
        }

        public override int Delete ()
        {
            // Delete anything in the folder and any map entries (recursively).
            var contentMaps = McMapFolderFolderEntry.QueryByFolderId (AccountId, Id);
            foreach (var map in contentMaps) {
                map.Delete ();
                switch (map.ClassCode) {
                case McItem.ClassCodeEnum.Email:
                    var emailMessage = McFolderEntry.QueryById<McEmailMessage> (map.FolderEntryId);
                    emailMessage.Delete ();
                    break;

                case McItem.ClassCodeEnum.Calendar:
                    var cal = McFolderEntry.QueryById<McCalendar> (map.FolderEntryId);
                    cal.Delete ();
                    break;

                case McItem.ClassCodeEnum.Contact:
                    var contact = McFolderEntry.QueryById<McCalendar> (map.FolderEntryId);
                    contact.Delete ();
                    break;

                case McItem.ClassCodeEnum.Tasks:
                    var task = McFolderEntry.QueryById<McTask> (map.FolderEntryId);
                    task.Delete ();
                    break;

                case McItem.ClassCodeEnum.Folder:
                    var folder = McFolderEntry.QueryById<McFolder> (map.FolderEntryId);
                    // recursion.
                    folder.Delete ();
                    break;

                default:
                    NachoAssert.True (false);
                    break;
                }
            }
            return base.Delete ();
        }

        public NcResult Link (McFolderEntry obj)
        {
            var getClassCode = obj.GetType ().GetMethod ("GetClassCode");
            NachoAssert.True (null != getClassCode);
            ClassCodeEnum classCode = (ClassCodeEnum)getClassCode.Invoke (null, new object[]{ });
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

        public NcResult Unlink (McFolderEntry obj)
        {
            var getClassCode = obj.GetType ().GetMethod ("GetClassCode");
            NachoAssert.True (null != getClassCode);
            ClassCodeEnum classCode = (ClassCodeEnum)getClassCode.Invoke (null, new object[]{ });
            var existing = McMapFolderFolderEntry.QueryByFolderIdFolderEntryIdClassCode 
                (AccountId, Id, obj.Id, classCode);
            if (null == existing) {
                return NcResult.Error (NcResult.SubKindEnum.Error_NotInFolder);
            }
            existing.Delete ();
            return NcResult.OK ();
        }

        public static void AsResetState (int accountId)
        {
            // TODO: USE SQL UPDATE.
            var folders = BackEnd.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          "f.AccountId = ? ",
                              accountId);
            foreach (var folder in folders) {
                folder.AsSyncKey = AsSyncKey_Initial;
                folder.AsSyncMetaToClientExpected = true;
                folder.Update ();
            }
        }

        public static ClassCodeEnum GetClassCode ()
        {
            return McFolderEntry.ClassCodeEnum.Folder;
        }
    }
}

