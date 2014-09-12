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

        public static string GetFilePath (int bodyId)
        {
            var body = QueryById<McBody> (bodyId);
            if (null == body) {
                return null;
            }
            return body.GetFilePath ();
        }

        public static McBody InsertSaveStart (int accountId)
        {
            var body = new McBody () {
                AccountId = accountId,
            };
            body.CompleteInsertSaveStart ();
            return body;
        }

        public static McBody InsertFile (int accountId, string content)
        {
            var body = new McBody () {
                AccountId = accountId,
            };
            body.CompleteInsertFile (content);
            return body;
        }

        public static McBody InsertDuplicate (int accountId, int srcBodyId)
        {
            var dstBody = new McBody () {
                AccountId = accountId,
            };
            var srcBody = QueryById<McBody> (srcBodyId);
            dstBody.CompleteInsertDuplicate (srcBody);
            return dstBody;
        }

        public static string GetContentsString (int bodyId)
        {
            var body = QueryById<McBody> (bodyId);
            if (null == body) {
                return null;
            }
            return body.GetContentsString ();
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
