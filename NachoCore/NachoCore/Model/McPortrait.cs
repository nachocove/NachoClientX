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

        public static bool CompareData (McPortrait a, McPortrait b)
        {
            if ((null == a) || (null == b)) {
                return false;
            }

            // Q - Need to stream?
            var dataA = a.GetContentsByteArray ();
            var dataB = b.GetContentsByteArray ();
            if (dataA.Length != dataB.Length) {
                return false;
            }
            for (int n = 0; n < dataA.Length; n++) {
                if (dataA [n] != dataB [n]) {
                    return false;
                }
            }
            return true;
        }
    }
}
