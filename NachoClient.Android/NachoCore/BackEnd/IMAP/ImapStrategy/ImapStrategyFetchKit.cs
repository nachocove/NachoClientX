//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Generic;
using NachoPlatform;
using System.Linq;
using NachoCore.ActiveSync;

namespace NachoCore.IMAP
{
    public partial class ImapStrategy : NcStrategy
    {
        public const int KBaseFetchSize = 5;

        // Returns null if nothing to do.
        public FetchKit GenFetchKit ()
        {
            var remaining = KBaseFetchSize;
            var maxAttachmentSize = MaxAttachmentSize ();
            var fetchBodies = FetchBodiesFromEmailList (FetchBodyHintList (remaining), maxAttachmentSize);
            remaining -= fetchBodies.Count;

            fetchBodies.AddRange (FetchBodiesFromEmailList (McEmailMessage.QueryNeedsFetch (AccountId, remaining, McEmailMessage.minHotScore).ToList (), maxAttachmentSize));
            remaining -= fetchBodies.Count;

            List<McAttachment> fetchAtts = new List<McAttachment> ();
            if (0 < remaining) {
                fetchAtts = McAttachment.QueryNeedsFetch (AccountId, remaining, 0.9, (int)maxAttachmentSize).ToList ();
            }
            if (fetchBodies.Any () || fetchAtts.Any ()) {
                Log.Info (Log.LOG_IMAP, "GenFetchKit: {0} emails, {1} attachments.", fetchBodies.Count, fetchAtts.Count);
                return new FetchKit () {
                    FetchBodies = fetchBodies,
                    FetchAttachments = fetchAtts,
                    Pendings = new List<FetchKit.FetchPending> (),
                };
            }
            Log.Info (Log.LOG_IMAP, "GenFetchKit: nothing to do.");
            return null;
        }

        public FetchKit GenFetchKitHints ()
        {
            var fetchBodies = FetchBodiesFromEmailList (FetchBodyHintList (KBaseFetchSize), MaxAttachmentSize ());
            if (fetchBodies.Any ()) {
                Log.Info (Log.LOG_IMAP, "GenFetchKitHints: {0} emails", fetchBodies.Count);
                return new FetchKit () {
                    FetchBodies = fetchBodies,
                    FetchAttachments = new List<McAttachment> (),
                    Pendings = new List<FetchKit.FetchPending> (),
                };
            } else {
                return null;
            }
        }

        private List<FetchKit.FetchBody> FetchBodiesFromEmailList (List<McEmailMessage> emails, long maxAttachmentSize)
        {
            var fetchBodies = new List<FetchKit.FetchBody> ();
            foreach (var email in emails) {
                // TODO: all this can be one SQL JOIN.
                var folders = McFolder.QueryByFolderEntryId<McEmailMessage> (AccountId, email.Id);
                if (0 == folders.Count) {
                    // This can happen - we score a message, and then it gets moved to a client-owned folder.
                    continue;
                }
                fetchBodies.Add (new FetchKit.FetchBody () {
                    ServerId = email.ServerId,
                    ParentId = folders [0].ServerId,
                    BodyPref = BodyPref (email, maxAttachmentSize),
                });
            }
            return fetchBodies;
        }

        private Xml.AirSync.TypeCode BodyPref (McEmailMessage message, long maxAttachmentSize)
        {
            return Xml.AirSync.TypeCode.Mime_4;
        }
    }
}

