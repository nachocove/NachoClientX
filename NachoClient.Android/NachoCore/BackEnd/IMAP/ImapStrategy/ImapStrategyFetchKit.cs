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
        public FetchKit GenFetchKit (int AccountId)
        {
            // The maximum attachment size to fetch depends on the quality of the network connection.
            long maxAttachmentSize = 0;
            switch (NcCommStatus.Instance.Speed) {
            case NetStatusSpeedEnum.WiFi_0:
                maxAttachmentSize = 1024 * 1024;
                break;
            case NetStatusSpeedEnum.CellFast_1:
                maxAttachmentSize = 200 * 1024;
                break;
            case NetStatusSpeedEnum.CellSlow_2:
                maxAttachmentSize = 50 * 1024;
                break;
            }
            var remaining = KBaseFetchSize;
            var fetchBodies = new List<FetchKit.FetchBody> ();
            var emails = McEmailMessage.QueryNeedsFetch (AccountId, remaining, McEmailMessage.minHotScore).ToList ();
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
            remaining -= fetchBodies.Count;
            List<McAttachment> fetchAtts = new List<McAttachment> ();
            if (0 < remaining) {
                fetchAtts = McAttachment.QueryNeedsFetch (AccountId, remaining, 0.9, (int)maxAttachmentSize).ToList ();
            }
            if (0 < fetchBodies.Count || 0 < fetchAtts.Count) {
                Log.Info (Log.LOG_AS, "GenFetchKit: {0} emails, {1} attachments.", fetchBodies.Count, fetchAtts.Count);
                return new FetchKit () {
                    FetchBodies = fetchBodies,
                    FetchAttachments = fetchAtts,
                    Pendings = new List<FetchKit.FetchPending> (),
                };
            }
            Log.Info (Log.LOG_AS, "GenFetchKit: nothing to do.");
            return null;
        }

        private Xml.AirSync.TypeCode BodyPref (McEmailMessage message, long maxAttachmentSize)
        {
            return Xml.AirSync.TypeCode.Mime_4;
        }
    }
}

