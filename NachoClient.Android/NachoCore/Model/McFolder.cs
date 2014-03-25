using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using NachoCore.Utils;

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
        public bool IsAwatingCreate { get; set; }

        public const string AsSyncKey_Initial = "0";

        public string AsSyncKey { get; set; }

        public bool AsSyncRequired { get; set; }

        [Indexed]
        public string DisplayName { get; set; }

        public int DisplayColor { get; set; }

        /// FIXME: Need enumeration
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
                AsSyncKey = AsSyncKey_Initial,
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

        public static List<McFolder> QueryByItemId<T> (int accountId, int itemId)
        {
            uint classCode;
            switch (typeof(T).FullName) {
            case "NachoCore.Model.McEmailMessage":
                classCode = (uint)McItem.ClassCodeEnum.Email;
                break;

            case "NachoCore.Model.McCalendar":
                classCode = (uint)McItem.ClassCodeEnum.Calendar;
                break;

            case "NachoCore.Model.McContact":
                classCode = (uint)McItem.ClassCodeEnum.Contact;
                break;

            default:
                throw new Exception ("Usupported Item class.");
            }

            return BackEnd.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f JOIN McMapFolderItem AS m ON f.Id = m.FolderId WHERE " +
            " m.AccountId = ? AND " +
            " m.ItemId = ? AND " +
            " m.ClassCode = ? ",
                accountId, itemId, classCode);
        }

        public override int Delete ()
        {
            // Delete anything in the folder and any map entries (recursively).
            // FIXME - query needs to find non-email items and sub-dirs.
            var contents = McItem.QueryByFolderId<McEmailMessage> (AccountId, Id);
            foreach (var item in contents) {
                var map = McMapFolderItem.QueryByFolderIdItemIdClassCode (AccountId, Id, item.Id,
                              (uint)McItem.ClassCodeEnum.Email);
                // FIXME capture result of ALL delete ops.
                map.Delete ();
                item.Delete ();
            }
            return base.Delete ();
        }

        public static void AsResetState (int accountId)
        {
            // TODO: SQL UPDATE.
            var folders = BackEnd.Instance.Db.Query<McFolder> ("SELECT f.* FROM McFolder AS f WHERE " +
                          "f.AccountId = ? ",
                              accountId);
            foreach (var folder in folders) {
                folder.AsSyncKey = AsSyncKey_Initial;
                folder.AsSyncRequired = true;
                folder.Update ();
            }
        }
    }
}

