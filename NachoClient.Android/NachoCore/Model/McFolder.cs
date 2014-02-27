using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McFolder : McObjectPerAccount
    {
        [Indexed]
        public bool IsClientOwned { get; set; }

        [Indexed]
        public bool IsHidden { get; set; }

        [Indexed]
        public string ServerId { get; set; }

        [Indexed]
        public string ParentId { get; set; }

        [Indexed]
        public bool IsAwatingCreate { get; set; }

        public string AsSyncKey { get; set; }

        public bool AsSyncRequired { get; set; }

        [Indexed]
        public string DisplayName { get; set; }
        // FIXME: Need enumeration
        public uint Type { get; set; }

        public override string ToString ()
        {
            return "NcFolder: sid=" + ServerId + " pid=" + ParentId + " skey=" + AsSyncKey + " dn=" + DisplayName + " type=" + Type.ToString ();
        }
        // "factory" to create client-owned folders.
        public static McFolder Create (int accountId, 
                                 bool isClientOwned,
                                 bool isHidden,
                                 string parentId,
                                 string serverId,
                                 string displayName,
                                 uint folderType)
        {
            var folder = new McFolder () {
                AsSyncKey = "0",
                AsSyncRequired = false,
                AccountId = accountId,
                IsClientOwned = isClientOwned,
                IsHidden = isHidden,
                ParentId = parentId,
                ServerId = serverId,
                DisplayName = displayName,
                Type = folderType,
            };
            folder.Insert ();
            return folder;
        }

        public static McFolder QueryByServerId (int accountId, string serverId)
        {
            return BackEnd.Instance.Db.Table<McFolder> ().Single (fld => 
                fld.AccountId == accountId &&
            fld.ServerId == serverId);
        }

        public static McFolder QueryById (int id)
        {
            return BackEnd.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
            " f.Id = ? ",
                id).SingleOrDefault ();
        }

        public static List<McFolder> QueryByItemId (int accountId, int itemId)
        {
            return BackEnd.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f JOIN McMapFolderItem AS m ON f.Id = m.FolderId WHERE " +
            " m.AccountId = ? AND " +
            " m.ItemId = ? ",
                accountId, itemId);
        }

        public override int Delete ()
        {
            // Delete anything in the folder and any map entries (recursively).
            // FIXME - query needs to find non-email items and sub-dirs.
            var contents = McEmailMessage.QueryByFolderId (AccountId, Id);
            foreach (var item in contents) {
                var map = McMapFolderItem.QueryByFolderIdItemIdClassCode (AccountId, Id, item.Id,
                    (uint)McItem.ClassCodeEnum.Email);
                // FIXME capture result of ALL delete ops.
                map.Delete ();
                item.Delete ();
            }
            return base.Delete ();
        }
    }
}

