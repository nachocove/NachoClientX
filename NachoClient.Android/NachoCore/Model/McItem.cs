using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
    // If SQLite.Net would tolerate an abstract class, we'd be one.
    public class McItem : McFolderEntry
    {
        // The ClientId is generated by the client, and is constant for the life of the Item.
        // It is meaningless for server-originated messages.
        [Indexed]
        public string ClientId { get; set; }

        [Indexed]
        public bool HasBeenGleaned { get; set; }

        /// Index of Body container
        public int BodyId { get; set; }

        /// Type of the transferred body
        public int BodyType { get; set; }

        [Indexed]
        public uint PendingRefCount { get; set; }

        // Platform-specific code sets this when a user-notification is sent.
        public bool HasBeenNotified { get; set; }

        public McItem ()
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

        public override int Delete ()
        {
            NcAssert.True (100000 > PendingRefCount);
            var returnVal = -1;
            try {
                NcModel.Instance.RunInTransaction (() => {
                    McFolder.UnlinkAll (this);
                    if (0 == PendingRefCount) {
                        var result = base.Delete ();
                        DeleteAncillary ();
                        returnVal = result;
                    } else {
                        IsAwaitingDelete = true;
                        Update ();
                        returnVal = 0;
                    }
                });
            } catch (SQLiteException ex) {
                Log.Error (Log.LOG_EMAIL, "Deleting the email failed: {0} No changes were made to the DB.", ex.Message);
                return -1;
            }
            return returnVal;
        }

        public static T QueryByClientId<T> (int accountId, string clientId) where T : McItem, new()
        {
            return NcModel.Instance.Db.Query<T> (
                string.Format ("SELECT f.* FROM {0} AS f WHERE " +
                " f.AccountId = ? AND " +
                " f.IsAwaitingDelete = 0 AND " +
                " f.ClientId = ? ", 
                    typeof(T).Name), 
                accountId, clientId).SingleOrDefault ();
        }

        public static List<T> QueryByFolderId<T> (int accountId, int folderId) where T : McItem, new()
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

        public string GetBody ()
        {
            return McBody.Get (BodyId);
        }

        public McBody GetBodyDescr ()
        {
            return McBody.GetDescr (BodyId);
        }

        public string GetBodyPath ()
        {
            return McBody.GetBodyPath (BodyId);
        }

        public int GetBodyType ()
        {
            return BodyType;
        }

        protected void DeleteBody ()
        {
            McBody.Delete (BodyId);
        }
    }
}

