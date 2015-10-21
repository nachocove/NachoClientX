//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Views;
using NachoCore.Model;
using System.Collections.Generic;
using Android.Widget;
using NachoCore.Utils;
using NachoCore;
using System.Linq;

namespace NachoClient.AndroidClient
{
    public class NcAttachmentView
    {
        View view;
        McAttachment attachment;

        string downloadToken;
        bool statusIndicatorIsRegistered;

        public delegate void AttachmentSelectedCallback (McAttachment attachment);

        public delegate void AttachmentErrorCallback (McAttachment attachment, NcResult nr);

        AttachmentErrorCallback OnAttachmentError;
        AttachmentSelectedCallback OnAttachmentSelected;

        public NcAttachmentView (McAttachment attachment, View view, AttachmentSelectedCallback onAttachmentSelected, AttachmentErrorCallback onAttachmentError)
        {
            this.attachment = attachment;
            this.view = view;
            this.OnAttachmentError = onAttachmentError;
            this.OnAttachmentSelected = onAttachmentSelected;

            Bind.BindAttachmentView (attachment, view);

            view.Click += View_Click;
        }

        void View_Click (object sender, EventArgs e)
        {
            switch (attachment.FilePresence) {
            case McAbstrFileDesc.FilePresenceEnum.None:
            case McAbstrFileDesc.FilePresenceEnum.Error:
                StartDownload ();
                break;
            case McAbstrFileDesc.FilePresenceEnum.Partial:
                break;
            case McAbstrFileDesc.FilePresenceEnum.Complete:
                if (null != OnAttachmentSelected) {
                    OnAttachmentSelected (attachment);
                }
                break;
            default:
                NachoCore.Utils.NcAssert.CaseError ();
                break;
            }
        }

        private void StartDownload ()
        {
            MaybeRegisterStatusInd ();
            var nr = DownloadAttachment (attachment);
            downloadToken = nr.GetValue<String> ();
            if (null == downloadToken) {
                if (null != OnAttachmentError) {
                    OnAttachmentError (attachment, nr);
                }
                RefreshStatus ();
                return;
            }
            StartAnimation ();
        }

        void StartAnimation ()
        {
            var downloadImageView = view.FindViewById<ImageView> (Resource.Id.attachment_download);
            var spinnerView = view.FindViewById<ProgressBar> (Resource.Id.attachment_spinner);
            downloadImageView.Visibility = ViewStates.Gone;
            spinnerView.Visibility = ViewStates.Visible;
        }

        void StopAnimation (bool showDownloadArrow)
        {
            var spinnerView = view.FindViewById<ProgressBar> (Resource.Id.attachment_spinner);
            spinnerView.Visibility = ViewStates.Gone;
            var downloadImageView = view.FindViewById<ImageView> (Resource.Id.attachment_download);
            downloadImageView.Visibility = (showDownloadArrow ? ViewStates.Visible : ViewStates.Gone);
        }

        private void ShowErrorMessage ()
        {
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var statusEvent = (StatusIndEventArgs)e;
            if (null != downloadToken && null != statusEvent.Tokens && downloadToken == statusEvent.Tokens.FirstOrDefault ()) {
                if (NcResult.SubKindEnum.Error_AttDownloadFailed == statusEvent.Status.SubKind) {
                    var localAccountId = attachment.AccountId;
                    var localDownloadToken = downloadToken;
                    NcTask.Run (delegate {
                        foreach (var request in McPending.QueryByToken(localAccountId, localDownloadToken)) {
                            if (McPending.StateEnum.Failed == request.State) {
                                request.Delete ();
                            }
                        }
                    }, "DelFailedMcPendingAttachmentDnld");
                    if (null != OnAttachmentError) {
                        OnAttachmentError (attachment, statusEvent.Status);
                    }
                }
                RefreshStatus ();
                downloadToken = null;
            }
        }

        public void RefreshStatus ()
        {
            if (null == attachment) {
                return;
            }
            attachment = McAttachment.QueryById<McAttachment> (attachment.Id);
            if (null == attachment) {
                return;
            }
            switch (attachment.FilePresence) {
            case McAbstrFileDesc.FilePresenceEnum.None:
                break;
            case McAbstrFileDesc.FilePresenceEnum.Partial:
                break;
            case McAbstrFileDesc.FilePresenceEnum.Error:
                StopAnimation (true);
                MaybeUnregisterStatusInd ();
                break;
            case McAbstrFileDesc.FilePresenceEnum.Complete:
                StopAnimation (false);
                MaybeUnregisterStatusInd ();
                break;
            default:
                NachoCore.Utils.NcAssert.CaseError ();
                break;
            }
            Bind.BindAttachmentView (attachment, view);
        }

        // TODO: Refactor to shared code
        public static NcResult DownloadAttachment (McAttachment attachment)
        {
            if (McAbstrFileDesc.FilePresenceEnum.Error == attachment.FilePresence) {
                // Clear the error code so the download will be attempted again.
                attachment.DeleteFile ();
            }
            if (McAbstrFileDesc.FilePresenceEnum.None == attachment.FilePresence) {
                return BackEnd.Instance.DnldAttCmd (attachment.AccountId, attachment.Id, true);
            } else if (McAbstrFileDesc.FilePresenceEnum.Partial == attachment.FilePresence) {
                var token = McPending.QueryByAttachmentId (attachment.AccountId, attachment.Id).Token;
                var nr = NcResult.OK (token); // null is potentially ok; callers expect it.
                return nr;
            } 
            NcAssert.True (false, "Should not try to download an already-downloaded attachment");
            return null;
        }

        protected void MaybeRegisterStatusInd ()
        {
            if (!statusIndicatorIsRegistered) {
                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
                statusIndicatorIsRegistered = true;
            }
        }

        protected void MaybeUnregisterStatusInd ()
        {
            if (statusIndicatorIsRegistered) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                statusIndicatorIsRegistered = false;
            }
        }
            
    }
}

