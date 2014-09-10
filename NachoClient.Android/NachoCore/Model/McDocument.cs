using SQLite;
using System;
using System.IO;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class McDocument : McAbstrFileDesc, IFilesViewItem
    {
        protected static object syncRoot = new Object ();

        protected static volatile McDocument instance;

        public static McDocument Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new McDocument ();
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
            return "documents";
        }

        public override bool IsReferenced ()
        {
            // FIXME.
            return false;
        }

        public string SourceApplication { get; set; }

        public McDocument InsertSaveStart (int accountId)
        {
            var document = new McDocument () {
                AccountId = accountId,
            };
            return (McDocument)CompleteInsertSaveStart (document);
        }
    }
}
