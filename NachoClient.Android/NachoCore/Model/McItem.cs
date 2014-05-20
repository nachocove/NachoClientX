using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;

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

        ///  AirSync.TypeCode; also NativeBodyType
        public int BodyType { get; set; }

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

        public static T QueryByClientId<T> (int accountId, string clientId) where T : McItem, new()
        {
            return NcModel.Instance.Db.Query<T> (
                string.Format ("SELECT f.* FROM {0} AS f WHERE " +
                " f.AccountId = ? AND " +
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

        protected void DeleteBody ()
        {
            McBody.Delete (BodyId);
        }
    }
}

