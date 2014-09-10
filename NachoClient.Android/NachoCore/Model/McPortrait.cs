using System;
using System.IO;
using System.Linq;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McPortrait : McAbstrFileDesc
    {
        protected static object syncRoot = new Object ();

        protected static volatile McPortrait instance;

        public static McPortrait Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new McPortrait ();
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
            return "portraits";
        }

        public override bool IsReferenced ()
        {
            NcAssert.True (!IsReferenced ());
            return (0 != McContact.QueryByPortraitIdIncAwaitDel (AccountId, Id).Count ());
        }

        public McPortrait InsertSaveStart (int accountId)
        {
            var portrait = new McPortrait () {
                AccountId = accountId,
            };
            return (McPortrait)CompleteInsertSaveStart (portrait);
        }

        public McPortrait InsertFile (int accountId, byte[] content)
        {
            var portrait = new McPortrait () {
                AccountId = accountId,
            };
            return (McPortrait)CompleteInsertFile (portrait, content);
        }

        public byte[] GetContentsByteArray (int portraitId)
        {
            var portrait = QueryById<McPortrait> (portraitId);
            return CompleteGetContentsByteArray (portrait);
        }
    }
}
