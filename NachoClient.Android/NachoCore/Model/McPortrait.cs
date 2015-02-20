using System;
using System.IO;
using System.Linq;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McPortrait : McAbstrFileDesc
    {
        public override string GetFilePathSegment ()
        {
            return "portraits";
        }

        public static McPortrait InsertFile (int accountId, byte[] content)
        {
            var portrait = new McPortrait () {
                AccountId = accountId,
            };
            portrait.CompleteInsertFile (content);
            return portrait;
        }

        public static byte[] GetContentsByteArray (int portraitId)
        {
            var portrait = QueryById<McPortrait> (portraitId);
            if (null == portrait) {
                return null;
            }
            return portrait.GetContentsByteArray ();
        }
    }
}
