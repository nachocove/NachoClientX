using SQLite;
using System;
using System.IO;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class McDocument : McAbstrFileDesc, IFilesViewItem
    {
        public override string GetFilePathSegment ()
        {
            return "documents";
        }

        public string SourceApplication { get; set; }

        public static McDocument InsertSaveStart (int accountId)
        {
            var document = new McDocument () {
                AccountId = accountId,
            };
            document.CompleteInsertSaveStart ();
            return document;
        }
    }
}
