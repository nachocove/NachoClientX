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
            return "NcFolder: sid=" + ServerId + " pid=" + ParentId + " skey=" + AsSyncKey + " dn=" + DisplayName + " type=" + Type.ToString();
        }

        // "factory" to create client-owned folders.
        public static McFolder CreateClientOwned (int accountId)
        {
            var folder = new McFolder () {
                IsClientOwned = true,
                AsSyncKey = "0",
                AsSyncRequired = false,
                AccountId = accountId,
            };
            return folder;
        }

        public static McFolder QueryByServerId (int accountId, string serverId)
        {
            return BackEnd.Instance.Db.Table<McFolder> ().Single (fld => 
                fld.AccountId == accountId &&
            fld.ServerId == serverId);
        }

        public static List<McFolder> QueryByItemId (int accountId, int itemId)
        {
            return BackEnd.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f JOIN McMapFolderItem AS m ON f.Id = m.FolderId WHERE " +
                " m.AccountId = ? AND " +
                " m.ItemId = ? ",
                accountId, itemId);
        }
    }
}

