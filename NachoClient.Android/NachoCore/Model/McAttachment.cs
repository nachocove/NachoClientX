using SQLite;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McAttachment : McAbstrFileDesc, IFilesViewItem
    {
        public override string GetFilePathSegment ()
        {
            return "attachments";
        }

        public static McAttachment InsertSaveStart (int accountId)
        {
            var att = new McAttachment () {
                AccountId = accountId,
            };
            att.CompleteInsertSaveStart ();
            return att;
        }

        /// <summary>
        /// Create a new McAttachment. The contents are filled in by passing a FileStream for the McAttachment's file to a delegate.
        /// </summary>
        /// <returns>A new McAttachment object that has been added to the database</returns>
        public static McAttachment InsertFile (int accountId, WriteFileDelegate writer)
        {
            var att = new McAttachment () {
                AccountId = accountId,
            };
            att.CompleteInsertFile (writer);
            return att;
        }

        public static McAttachment InsertError (int accountId)
        {
            var att = new McAttachment () {
                AccountId = accountId,
                FilePresence = FilePresenceEnum.Error,
            };
            att.CompleteInsertSaveStart ();
            return att;
        }

        /// <summary>
        /// DEPRECATED - DO NOT USE.
        /// </summary>
        public int ItemId { get; set; }

        /// <summary>
        /// DEPRECATED - DO NOT USE.
        /// </summary>
        public McAbstrFolderEntry.ClassCodeEnum ClassCode { get; set; }

        [Indexed]
        public string FileReference { get; set; }

        public uint Method { get; set; }

        public string ContentId { get; set; }

        public string ContentLocation { get; set; }

        public bool IsInline { get; set; }

        public uint VoiceSeconds { get; set; }

        public int VoiceOrder { get; set; }

        public string ContentType { get; set; }

        public override bool IsImageFile ()
        {
            if (base.IsImageFile ()) {
                return true;
            }
            if (String.IsNullOrEmpty (ContentType)) {
                return false;
            }
            var mimeInfo = ContentType.Split (new char[] { '/' });
            if (2 != mimeInfo.Length) {
                return false;
            }
            if (!String.Equals ("image", mimeInfo [0], StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            string[] subtype = {
                "tiff",
                "jpeg",
                "jpg",
                "gif",
                "png",
            };
            foreach (var s in subtype) {
                if (String.Equals (s, mimeInfo [1], StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        public NcResult Link (McAbstrItem item, bool includedInBody = false)
        {
            return Link (item.Id, item.AccountId, item.GetClassCode ());
        }

        public NcResult Link (int itemId, int accountId, McAbstrFolderEntry.ClassCodeEnum classCode, bool includedInBody = false)
        {
            NcAssert.False (0 == Id);
            NcAssert.False (0 == itemId);

            switch (classCode) {
            case McAbstrFolderEntry.ClassCodeEnum.Calendar:
            case McAbstrFolderEntry.ClassCodeEnum.Email:
            case McAbstrFolderEntry.ClassCodeEnum.Chat:
                // FIXME - can we get rid of never-in-folder?
            case McAbstrFolderEntry.ClassCodeEnum.NeverInFolder:
                break;
            default:
                NcAssert.CaseError (string.Format ("{0}", classCode));
                break;
            }
            // NOTICE - currently we allow attaching to cross the account barrier!
            // The Map is in the item's account.
            NcResult result = NcResult.OK ();
            NcModel.Instance.RunInTransaction (() => {
                var existing = McMapAttachmentItem.QueryByAttachmentIdItemIdClassCode (accountId, Id, itemId, classCode);
                if (null != existing) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_AlreadyAttached);
                    return;
                }
                var map = new McMapAttachmentItem (accountId) {
                    AttachmentId = Id,
                    ItemId = itemId,
                    ClassCode = classCode,
                    IncludedInBody = includedInBody,
                };
                map.Insert ();
            });
            return result;
        }

        public NcResult Unlink (McAbstrItem item)
        {
            return Unlink (item.Id, item.AccountId, item.GetClassCode ());
        }

        public NcResult Unlink (int itemId, int accountId, McAbstrFolderEntry.ClassCodeEnum classCode)
        {
            var existing = McMapAttachmentItem.QueryByAttachmentIdItemIdClassCode (accountId, Id, itemId, classCode);
            if (null == existing) {
                return NcResult.Error (NcResult.SubKindEnum.Error_NotAttached);
            }
            existing.Delete ();
            return NcResult.OK ();
        }

        /// <summary>
        /// Queries the by item id.
        /// </summary>
        /// <returns>The by item.</returns>
        /// <param name="accountId">Account identifier. MUST match that of the item for a useful result.</param>
        /// <param name="itemId">Item identifier.</param>
        /// <param name="classCode">Class code.</param>
        public static List<McAttachment> QueryByItemId (int accountId, int itemId, McAbstrFolderEntry.ClassCodeEnum classCode)
        {
            if (McAbstrFolderEntry.ClassCodeEnum.Email == classCode || McAbstrFolderEntry.ClassCodeEnum.Calendar == classCode || McAbstrFolderEntry.ClassCodeEnum.Chat == classCode) {
                // Only e-mail messages and calendar items can own attachments.
                // TODO We think that exceptions can own attachments, but that hasn't been confirmed.
                return NcModel.Instance.Db.Query<McAttachment> (
                    "SELECT a.* FROM McAttachment AS a " +
                    " JOIN McMapAttachmentItem AS m ON a.Id = m.AttachmentId " +
                    " WHERE " +
                    " likelihood (m.AccountId = ?, 1.0) AND " +
                    " likelihood (m.ItemId = ?, 0.01) AND " +
                    " likelihood (m.ClassCode = ?, 0.5) ",
                    accountId, itemId, (int)classCode);
            } else {
                // For other kinds of items, don't even bother looking in the database.
                return new List<McAttachment> ();
            }
        }

        public static List<McAttachment> QueryByItem (McAbstrFolderEntry item)
        {
            return QueryByItemId (item.AccountId, item.Id, item.GetClassCode ());
        }

        // accountId must match that of the McAttachment.
        public static IEnumerable<McAttachment> QueryNeedsFetch (int accountId, int limit, double minScore, int maxSize)
        {
            return NcModel.Instance.Db.Query<McAttachment> (
                "SELECT a.* FROM McAttachment AS a " +
                " JOIN McMapAttachmentItem AS m ON a.Id = m.AttachmentId " +
                " JOIN McEmailMessage AS e ON e.Id = m.ItemId " +
                " WHERE " +
                " likelihood (a.AccountId = ?, 1.0) AND " +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.Score >= ?, 0.1) AND " +
                " a.FileSize <= ? AND " +
                " a.FilePresence != ? AND " +
                " a.FilePresence != ? AND " +
                " a.FilePresence != ? " +
                " ORDER BY e.Score DESC, e.DateReceived DESC LIMIT ?",
                accountId, minScore, maxSize,
                (int)FilePresenceEnum.Complete, (int)FilePresenceEnum.Partial, (int)FilePresenceEnum.Error,
                limit);
        }

        /// <summary>
        /// Queries the items. NOTE: this may return items from ANY account.
        /// </summary>
        /// <returns>The items.</returns>
        /// <param name="attachmentId">Attachment identifier.</param>
        public static List<McAbstrItem> QueryItems (int attachmentId)
        {
            var retval = new List<McAbstrItem> ();
            var emails = NcModel.Instance.Db.Query<McEmailMessage> (
                             "SELECT e.* FROM McEmailMessage AS e " +
                             " JOIN McMapAttachmentItem AS m ON e.Id = m.ItemId " +
                             " JOIN McAttachment AS a ON a.Id = m.AttachmentId " +
                             " WHERE " +
                             " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                             " likelihood (a.Id = ?, 0.01) AND " +
                             " likelihood (m.ClassCode = ?, 0.5) ",
                             attachmentId, (int)McAbstrFolderEntry.ClassCodeEnum.Email);
            var cals = NcModel.Instance.Db.Query<McCalendar> (
                           "SELECT c.* FROM McCalendar AS c " +
                           " JOIN McMapAttachmentItem AS m ON c.Id = m.ItemId " +
                           " JOIN McAttachment AS a ON a.Id = m.AttachmentId " +
                           " WHERE " +
                           " likelihood (c.IsAwaitingDelete = 0, 1.0) AND " +
                           " likelihood (a.Id = ?, 0.01) AND " +
                           " likelihood (m.ClassCode = ?, 0.5) ",
                           attachmentId, (int)McAbstrFolderEntry.ClassCodeEnum.Calendar);
            retval.AddRange (emails);
            retval.AddRange (cals);
            return retval;
        }

        /// <summary>
        /// Queries the items.
        /// </summary>
        /// <returns>The items.</returns>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="attachmentId">Attachment identifier.</param>
        public static List<McAbstrItem> QueryItems (int accountId, int attachmentId)
        {
            var retval = new List<McAbstrItem> ();
            var emails = NcModel.Instance.Db.Query<McEmailMessage> (
                             "SELECT e.* FROM McEmailMessage AS e " +
                             " JOIN McMapAttachmentItem AS m ON e.Id = m.ItemId " +
                             " JOIN McAttachment AS a ON a.Id = m.AttachmentId " +
                             " WHERE " +
                             " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                             " likelihood (e.AccountId = ?, 0.5) AND " +
                             " likelihood (a.Id = ?, 0.01) ",
                             accountId, attachmentId);
            var cals = NcModel.Instance.Db.Query<McCalendar> (
                           "SELECT c.* FROM McCalendar AS c " +
                           " JOIN McMapAttachmentItem AS m ON c.Id = m.ItemId " +
                           " JOIN McAttachment AS a ON a.Id = m.AttachmentId " +
                           " WHERE " +
                           " likelihood (c.IsAwaitingDelete = 0, 1.0) AND " +
                           " likelihood (c.AccountId = ?, 0.5) AND " +
                           " likelihood (a.Id = ?, 0.01) ",
                           accountId, attachmentId);
            retval.AddRange (emails);
            retval.AddRange (cals);
            return retval;
        }
    }
}
