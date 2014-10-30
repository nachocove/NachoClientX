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
        public static McBody InsertFile (int accountId, int bodyType, string content)
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
        public static McBody InsertFile (int accountId, int bodyType, WriteFileDelegate writer)
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
