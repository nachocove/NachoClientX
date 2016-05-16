//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoPlatform;
using System.Collections.Generic;
using NachoCore.Model;
using System.Linq;

namespace NachoCore
{
    public class NcStrategy
    {
        protected IBEContext BEContext;
        protected int AccountId { get; set; }

        public NcStrategy (IBEContext beContext)
        {
            BEContext = beContext;
            AccountId = BEContext.Account.Id;
        }

        public bool PowerPermitsSpeculation ()
        {
            return (Power.Instance.PowerState != PowerStateEnum.Unknown && Power.Instance.BatteryLevel > 0.7) ||
                (Power.Instance.PowerStateIsPlugged () && Power.Instance.BatteryLevel > 0.2);
        }

        public virtual bool ANarrowFolderHasToClientExpected ()
        {
            NcAssert.True (false);
            return true;
        }

        protected List<McEmailMessage> FetchBodyHintList (int count)
        {
            var EmailList = new List<McEmailMessage> ();
            if (null != NcApplication.Instance.Account &&
                AccountId == NcApplication.Instance.Account.Id &&
                NcApplication.Instance.ExecutionContext == NcApplication.ExecutionContextEnum.Foreground) {
                var hints = BackEnd.Instance.BodyFetchHints.GetHints (AccountId, count);
                var pendings = McPending.QueryByOperation (AccountId, McPending.Operations.EmailBodyDownload);
                foreach (var id in hints) {
                    var email = McEmailMessage.QueryById<McEmailMessage> (id);
                    if (null != email) {
                        var existing = pendings.FirstOrDefault (x => x.ServerId == email.ServerId);
                        if (null != existing) {
                            Log.Info (Log.LOG_BACKEND, "ignoring hint due to existence of pending");
                            // there's already a pending for this.
                            BackEnd.Instance.BodyFetchHints.RemoveHint (AccountId, id);
                            // TODO: Do we need to do anything to the existing pending?
                            continue;
                        }

                        if (0 == email.BodyId) {
                            EmailList.Add (email);
                        } else {
                            var body = McBody.QueryById<McBody> (email.BodyId);
                            if (body.FilePresence != McAbstrFileDesc.FilePresenceEnum.Complete) {
                                EmailList.Add (email);
                            }
                        }
                    }
                }
            }
            return EmailList;
        }

        protected long MaxAttachmentSize ()
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
            return maxAttachmentSize;
        }
    }
}
