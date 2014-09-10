using System;
using System.IO;
using System.Linq;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McBody : McAbstrFileDesc
    {
        protected static object syncRoot = new Object ();

        protected static volatile McBody instance;

        public static McBody Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new McBody ();
                        }
                    }
                }
                return instance; 
            }
        }

        protected override bool IsInstance ()
        {
            return this == instance;
        }

        public override string GetFilePathSegment ()
        {
            return "bodies";
        }

        public override bool IsReferenced ()
        {
            NcAssert.True (!IsInstance ());
            // TODO: find a clean way to iterate over all derived classes of McAbstrItem.
            return (0 != McEmailMessage.QueryByBodyIdIncAwaitDel<McEmailMessage> (AccountId, Id).Count () ||
                0 != McCalendar.QueryByBodyIdIncAwaitDel<McCalendar> (AccountId, Id).Count () ||
                0 != McContact.QueryByBodyIdIncAwaitDel<McContact> (AccountId, Id).Count () ||
                0 != McTask.QueryByBodyIdIncAwaitDel<McTask> (AccountId, Id).Count ());
        }

        public string GetFilePath (int bodyId)
        {
            var body = QueryById<McBody> (bodyId);
            return CompleteGetFilePath (body);
        }

        public McBody InsertSaveStart (int accountId)
        {
            var body = new McBody () {
                AccountId = accountId,
            };
            return (McBody)CompleteInsertSaveStart (body);
        }

        public McBody InsertFile (int accountId, string content)
        {
            var body = new McBody () {
                AccountId = accountId,
            };
            return (McBody)CompleteInsertFile (body, content);
        }

        public McBody InsertDuplicate (int accountId)
        {
            var body = new McBody () {
                AccountId = accountId,
            };
            return (McBody)CompleteInsertDuplicate (body);
        }

        public McBody InsertDuplicate (int accountId, int srcBodyId)
        {
            var dstBody = new McBody () {
                AccountId = accountId,
            };
            var srcBody = QueryById<McBody> (srcBodyId);
            return (McBody)CompleteInsertDuplicate (dstBody, srcBody);
        }

        public string GetContentsString (int bodyId)
        {
            var body = QueryById<McBody> (bodyId);
            return CompleteGetContentsString (body);
        }

        /// Body type is stored in McItem, along with the item's index to McBody.
        /// This circumvents reading db just to get body type. Most references to
        /// bodies are to get its path, which is computed with the body id, without
        /// reading from the database.
        /// 
        /// TODO: covernt to using AirSync TypeCode so enum is defined in one place.
        /// AirSync.TypeCode PlainText_1, Html_2, Rtf_3, Mime_4
        public const int PlainText = 1;
        public const int HTML = 2;
        public const int RTF = 3;
        public const int MIME = 4;
    }
}
