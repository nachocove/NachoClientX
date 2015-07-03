//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{

    public class BodyDownloader
    {
        McAbstrItem item;

        string downloadToken;

        public event EventHandler<string> Finished;

        public BodyDownloader ()
        {
        }

        public void Start (McAbstrItem item)
        {
            this.item = item;
            StartDownload ();
        }



        void StartDownload ()
        {
            // Download the body.
            NcResult nr;
            if (item is McEmailMessage) {
                nr = BackEnd.Instance.DnldEmailBodyCmd (item.AccountId, item.Id, true);
            } else if (item is McAbstrCalendarRoot) {
                nr = BackEnd.Instance.DnldCalBodyCmd (item.AccountId, item.Id);
            } else {
                throw new NcAssert.NachoDefaultCaseFailure (string.Format ("Unhandled abstract item type {0}", item.GetType ().Name));
            }
            downloadToken = nr.GetValue<string> ();

            if (null == downloadToken) {
                item = McAbstrItem.RefreshItem (item);
                var body = McBody.QueryById<McBody> (item.BodyId);
                if (McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                    ReturnSuccess ();
                } else {
                    Log.Warn (Log.LOG_UI, "Failed to start body download for message {0} in account {1}", item.Id, item.AccountId);
                    ReturnErrorMessage (nr);
                }
                return;
            }
            McPending.Prioritize (item.AccountId, downloadToken);
        }

        void ReturnSuccess ()
        {
            Console.WriteLine ("BodyDownloaded: ReturnSuccess");

            if (null != Finished) {
                Finished (this, null);
            }
        }

        void ReturnErrorMessage (NcResult nr)
        {
            Console.WriteLine ("BodyDownloaded: ReturnErrorMessage");
            string message;
            if (!ErrorHelper.ExtractErrorString (nr, out message)) {
                message = "Download failed.";
            }
            if (null != Finished) {
                Finished (this, message);
            }
        }

        public bool HandleStatusEvent (StatusIndEventArgs statusEvent)
        {
            if (null == statusEvent.Tokens) {
                return false;
            }
            if (statusEvent.Tokens.FirstOrDefault () != downloadToken) {
                return false;
            }

            Console.WriteLine ("BodyDownloader HandleStatusEvent {0} {1}", item.Id, statusEvent.Status.SubKind);

            switch (statusEvent.Status.SubKind) {
            case NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded:
            case NcResult.SubKindEnum.Info_CalendarBodyDownloadSucceeded:
                ReturnSuccess ();
                break;
            case NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed:
            case NcResult.SubKindEnum.Error_CalendarBodyDownloadFailed:
                // The McPending isn't needed any more.
                var localAccountId = item.AccountId;
                var localDownloadToken = downloadToken;
                NcTask.Run (delegate {
                    foreach (var request in McPending.QueryByToken (localAccountId, localDownloadToken)) {
                        if (McPending.StateEnum.Failed == request.State) {
                            request.Delete ();
                        }
                    }
                }, "DelFailedMcPendingBodyDnld");
                ReturnErrorMessage (NcResult.Error (statusEvent.Status.SubKind));
                break;
            }
            return true;
        }
    }

}

