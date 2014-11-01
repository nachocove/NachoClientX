using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
    // If SQLite.Net would tolerate an abstract class, we'd be one.
    public class McAbstrItem : McAbstrFolderEntry
    {
        // The ClientId is generated by the client, and is constant for the life of the Item.
        // It is meaningless for server-originated messages.
        [Indexed]
        public string ClientId { get; set; }

        [Indexed]
        // The "owner" can record a value here that idicates the conditions under which the entry
        // was created. The owner can then know that later enhancements weren't available when the the
        // entry was created, and take actions. For example, adding street address to device contacts.
        public int OwnerEpoch { get; set; }

        [Indexed]
        public bool HasBeenGleaned { get; set; }

        /// Index of Body container
        public int BodyId { get; set; }

        public string BodyPreview { get; set; }

        [Indexed]
        public uint PendingRefCount { get; set; }

        // Platform-specific code sets this when a user-notification is sent.
        public bool HasBeenNotified { get; set; }

        /// The parser blew up when parsing this item, so we know it is missing some fields.
        public bool IsIncomplete { get; set; }

        public McAbstrItem ()
        {
            // TODO - only really need to init ClientId for from-client creations.
            ClientId = Guid.NewGuid ().ToString ("N");
        }

        public enum ItemSource
        {
            Unknown,
            ActiveSync,
            Device,
            User,
            Internal,
        };

        public virtual void DeleteAncillary ()
        {
            // Sub-class overrides and adds post-delete ancillary data cleanup.
            // We'd prefer to make this abstract, but SQLite.Net can't tolerate it.
            NcAssert.True (NcModel.Instance.IsInTransaction ());
        }

        private IEnumerable<McAbstrItem> ExistingItems ()
        {
            IEnumerable<McAbstrItem> existingItems = null;
            switch (GetClassCode ()) {
            case ClassCodeEnum.Calendar:
                existingItems = McCalendar.QueryByServerIdMult<McCalendar> (AccountId, ServerId);
                break;
            case ClassCodeEnum.Contact:
                existingItems = McCalendar.QueryByServerIdMult<McContact> (AccountId, ServerId);
                break;
            case ClassCodeEnum.Email:
                existingItems = McCalendar.QueryByServerIdMult<McEmailMessage> (AccountId, ServerId);
                break;
            }
            return (null == existingItems) ? new List<McAbstrItem> () : existingItems;
        }

        public override int Insert ()
        {
            var existingItems = ExistingItems ();
            foreach (var existingItem in existingItems) {
                NcAssert.True (false, string.Format ("Existing item {0} has ServerId {1}", existingItem.Id, ServerId));
            }
            return base.Insert ();
        }

        public override int Update ()
        {
            var existingItems = ExistingItems ();
            foreach (var existingItem in existingItems) {
                NcAssert.Equals (Id, existingItem.Id);
            }
            return base.Update ();
        }

        public override int Delete ()
        {
            NcAssert.True (100000 > PendingRefCount);
            var returnVal = -1;

            NcModel.Instance.RunInTransaction (() => {
                McFolder.UnlinkAll (this);
                if (0 == PendingRefCount) {
                    var result = base.Delete ();
                    if (0 < BodyId) {
                        var body = McBody.QueryById<McBody> (BodyId);
                        if (null != body) {
                            body.Delete ();
                        }
                    }
                    DeleteAncillary ();
                    returnVal = result;
                } else {
                    IsAwaitingDelete = true;
                    Update ();
                    returnVal = 0;
                }
            });

            return returnVal;
        }

        public static IEnumerable<T> QueryByBodyIdIncAwaitDel<T> (int accountId, int bodyId) where T : McAbstrItem, new()
        {
            return NcModel.Instance.Db.Query<T> (
                string.Format ("SELECT f.* FROM {0} AS f WHERE " +
                " f.AccountId = ? AND " +
                " f.BodyId = ? ",
                    typeof(T).Name), 
                accountId, bodyId);
        }

        public static T QueryByClientId<T> (int accountId, string clientId) where T : McAbstrItem, new()
        {
            return NcModel.Instance.Db.Query<T> (
                string.Format ("SELECT f.* FROM {0} AS f WHERE " +
                " f.AccountId = ? AND " +
                " f.IsAwaitingDelete = 0 AND " +
                " f.ClientId = ? ", 
                    typeof(T).Name), 
                accountId, clientId).SingleOrDefault ();
        }

        public static List<T> QueryByFolderId<T> (int accountId, int folderId) where T : McAbstrItem, new()
        {
            return NcModel.Instance.Db.Query<T> (
                string.Format (
                    "SELECT e.* FROM {0} AS e JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId WHERE " +
                    " e.AccountId = ? AND " +
                    " m.AccountId = ? AND " +
                    " e.IsAwaitingDelete = 0 AND " +
                    " m.FolderId = ? ",
                    typeof(T).Name),
                accountId, accountId, folderId);
        }

        public McBody GetBody ()
        {
            if (0 == BodyId) {
                return null;
            }
            return McBody.QueryById<McBody> (BodyId);
        }

        public string GetBodyPreviewOrEmpty ()
        {
            if (null == BodyPreview) {
                return String.Empty;
            } else {
                return BodyPreview;
            }
        }

        public override ClassCodeEnum GetClassCode ()
        {
            // Sub-class needs to override if it is to be folder link-able.
            return ClassCodeEnum.NeverInFolder;
        }

    }
}

