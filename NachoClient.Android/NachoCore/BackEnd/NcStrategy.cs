//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoPlatform;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore
{
    public class NcStrategy
    {
        protected IBEContext BEContext;

        protected int AccountId { get; set; }

        protected Random CoinToss;

        public NcStrategy (IBEContext beContext)
        {
            CoinToss = new Random ();
            BEContext = beContext;
            AccountId = BEContext.Account.Id;
            NcApplication.Instance.StatusIndEvent += StatusIndEventHandler;
        }

        public void StatusIndEventHandler (Object sender, EventArgs ea)
        {
            var siea = (StatusIndEventArgs)ea;
            if (null == siea.Account || siea.Account.Id != AccountId) {
                return;
            }
            switch (siea.Status.SubKind) {
            case NcResult.SubKindEnum.Info_ExecutionContextChanged:
                switch ((NcApplication.ExecutionContextEnum)siea.Status.Value) {
                case NcApplication.ExecutionContextEnum.Background:
                    EnteredBG = DateTime.UtcNow;
                    break;

                default:
                    EnteredBG = DateTime.MinValue;
                    break;
                }
                break;
            }
        }

        DateTime EnteredBG = DateTime.MinValue;
        protected readonly TimeSpan BGActiveTime = new TimeSpan (0, 20, 0);

        /// <summary>
        /// See if we're past the time in BG where we should start shutting up. If we're plugged in, we allow it.
        /// </summary>
        /// <remarks>
        /// Note that we also override the result with a coin toss, i.e. some percent of the time
        /// we will allow a sync anyway.
        /// </remarks>
        /// <returns><c>true</c>, if time permits sync was backgrounded, <c>false</c> otherwise.</returns>
        protected bool BGTimePermitsSync ()
        {
            return (Power.Instance.PowerStateIsPlugged () || 
                CoinToss.NextDouble () > 0.85 || (EnteredBG != DateTime.MinValue && (DateTime.UtcNow - EnteredBG) < BGActiveTime));
        }

        protected bool SyncPermitted ()
        {
            return PowerPermitsSyncs () &&
                (NcApplication.Instance.ExecutionContext == NcApplication.ExecutionContextEnum.Foreground || BGTimePermitsSync ());
        }

        protected double FetchMailThreshold {
            get {
                switch (NcApplication.Instance.ExecutionContext) {
                case NcApplication.ExecutionContextEnum.Foreground:
                    return 0.7;

                case NcApplication.ExecutionContextEnum.Background:
                    if (EnteredBG == DateTime.MinValue || (DateTime.UtcNow - EnteredBG) < BGActiveTime) {
                        // EnteredBG == DateTime.MinValue just means the status ind hasn't triggered yet, so let's
                        // assume we're still in the < 3 minute window.
                        return 0.7;
                    } else {
                        return 0.9;
                    }

                default:
                    return 1.0;
                }
            }
        }

        public bool PowerPermitsSpeculation ()
        {
            return (Power.Instance.PowerState != PowerStateEnum.Unknown && Power.Instance.BatteryLevel > 0.7) ||
            (Power.Instance.PowerStateIsPlugged () && Power.Instance.BatteryLevel > 0.2);
        }

        public bool PowerPermitsSyncs ()
        {
            return (Power.Instance.PowerState != PowerStateEnum.Unknown && Power.Instance.BatteryLevel > 0.4) ||
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
                foreach (var id in hints) {
                    var email = McEmailMessage.QueryById<McEmailMessage> (id);
                    if (null != email) {
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
