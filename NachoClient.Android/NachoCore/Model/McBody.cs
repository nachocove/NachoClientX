﻿using System;
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

        public static string GetContentsString (int bodyId)
        {
            var body = QueryById<McBody> (bodyId);
            if (null == body) {
                return null;
            }
            return body.GetContentsString ();
        }
    }
}
