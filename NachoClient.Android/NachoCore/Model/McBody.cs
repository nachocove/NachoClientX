using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McBody : McAbstrFileDesc
    {
        public override string GetFilePathSegment ()
        {
            return "bodies";
        }

        /// <summary>
        /// Create a new McBody with the given string as the contents
        /// </summary>
        /// <returns>A new McBody object that has been added to the database</returns>
        public static McBody InsertFile (int accountId, McAbstrFileDesc.BodyTypeEnum bodyType, string content)
        {
            var body = new McBody () {
                AccountId = accountId,
                BodyType = bodyType,
            };
            body.CompleteInsertFile (content);
            return body;
        }

        /// <summary>
        /// Create a new McBody with the given byte array as the contents
        /// </summary>
        /// <returns>A new McBody object that has been added to the database</returns>
        public static McBody InsertFile (int accountId, McAbstrFileDesc.BodyTypeEnum bodyType, byte[] content)
        {
            var body = new McBody () {
                AccountId = accountId,
                BodyType = bodyType,
            };
            body.CompleteInsertFile (content);
            return body;
        }

        /// <summary>
        /// Create a new McBody. The contents are filled in by passing a FileStream for the McBody's file to a delegate.
        /// </summary>
        /// <returns>A new McBody object that has been added to the database</returns>
        public static McBody InsertFile (int accountId, McAbstrFileDesc.BodyTypeEnum bodyType, WriteFileDelegate writer)
        {
            var body = new McBody () {
                AccountId = accountId,
                BodyType = bodyType,
            };
            body.CompleteInsertFile (writer);
            return body;
        }

        public static McBody InsertDuplicate (int accountId, int srcBodyId)
        {
            var srcBody = QueryById<McBody> (srcBodyId);
            var dstBody = new McBody () {
                AccountId = accountId,
                BodyType = srcBody.BodyType,
            };
            dstBody.CompleteInsertDuplicate (srcBody);
            return dstBody;
        }

        public static McBody InsertError (int accountId)
        {
            var body = new McBody () {
                AccountId = accountId,
                FilePresence = FilePresenceEnum.Error,
            };
            body.CompleteInsertSaveStart ();
            return body;
        }

        public static McBody InsertPlaceholder (int accountId)
        {
            var body = new McBody () {
                AccountId = accountId,
                FilePresence = FilePresenceEnum.None,
            };
            body.CompleteInsertSaveStart ();
            return body;
        }

        public static string GetContentsString (int bodyId)
        {
            var body = QueryById<McBody> (bodyId);
            if (null == body) {
                return null;
            }
            return body.GetContentsString ();
        }

        public void Touch()
        {
            NcAssert.True (NcModel.Instance.IsInTransaction ());
            LastModified = DateTime.UtcNow;
            NcModel.Instance.Db.Query<McBody> ("UPDATE McBody SET LastModified = ?", LastModified);
        }

        public override int Delete ()
        {
            DeleteFileStoredBundle ();
            return base.Delete ();
        }

        void DeleteFileStoredBundle ()
        {
            var path = NcEmailMessageBundle.FileStoragePathForBodyId (AccountId, Id);
            if (Directory.Exists (path)) {
                try {
                    Directory.Delete (path, true);
                } catch (Exception ex) {
                    Log.Error (Log.LOG_DB, "McBody: Exception trying to delete bundle: {0}", ex);
                }
            }
        }

    }
}
