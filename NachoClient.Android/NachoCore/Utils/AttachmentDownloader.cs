//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoCore.Model;
using System.Linq;
using NachoPlatform;

namespace NachoCore.Utils
{

    public interface AttachmentDownloaderDelegate {

        void AttachmentDownloadDidFinish (AttachmentDownloader downloader);
        void AttachmentDownloadDidFail (AttachmentDownloader downloader, NcResult result);
    }

    public class AttachmentDownloader
    {

        public AttachmentDownloaderDelegate Delegate;
        public object DownloadContext;
        public McAttachment Attachment {
            get {
                return _Attachment;
            }
        }
        private McAttachment _Attachment;
        private string DownloadToken;
        private bool IsListeningForStatusIndicator;
        private bool WaitingToDownloadInForeground;

        public AttachmentDownloader ()
        {
        }

        public void Download (McAttachment attachment)
        {
            _Attachment = attachment;
            if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                DownloadComplete ();
            } else {
                EnqueueDownload ();
            }
        }

        void EnqueueDownload ()
        {
            StartListeningForStatusIndicator ();
            if (NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {
                StartDownload ();
            } else {
                WaitingToDownloadInForeground = true;
            }
        }

        void StartDownload ()
        {
            NcResult result = null;
            if (Attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Error) {
                // Clear the error code so the download will be attempted again.
                Attachment.DeleteFile ();
            }
            if (Attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.None) {
                result = BackEnd.Instance.DnldAttCmd (Attachment.AccountId, Attachment.Id, true);
            } else if (Attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Partial) {
                var token = McPending.QueryByAttachmentId (Attachment.AccountId, Attachment.Id).Token;
                result = NcResult.OK (token);
            } else {
                NcAssert.True (false, "Should not try to download an already-downloaded attachment");
            }
            if (result.isError ()) {
                DownloadToken = null;
            } else {
                DownloadToken = result.GetValue<string> ();
            }
            if (DownloadToken == null) {
                FailWithResult (result);
            }
        }

        void StatusIndicatorCallback (object sender, EventArgs e)
        {
            if (!IsListeningForStatusIndicator) {
                return;
            }

            var statusEvent = (StatusIndEventArgs)e;


            if (WaitingToDownloadInForeground) {
                if (statusEvent.Status.SubKind == NcResult.SubKindEnum.Info_ExecutionContextChanged) {
                    if (NcApplication.Instance.ExecutionContext == NcApplication.ExecutionContextEnum.Foreground) {
                        WaitingToDownloadInForeground = false;
                        StartDownload ();
                    }
                }
            }else if (statusEvent.Tokens != null && statusEvent.Tokens.FirstOrDefault () == DownloadToken) {
                if (statusEvent.Status.SubKind == NcResult.SubKindEnum.Info_AttDownloadUpdate) {
                    CheckComplete ();
                } else if (statusEvent.Status.SubKind == NcResult.SubKindEnum.Error_AttDownloadFailed) {
                    ClearPending ();
                    if (NcApplication.Instance.ExecutionContext != NcApplication.ExecutionContextEnum.Foreground && statusEvent.Status.Why == NcResult.WhyEnum.UnavoidableDelay) {
                        // Failure was because we got sent to background and couldn't complete.  Retry on foreground.
                        DownloadToken = null;
                        WaitingToDownloadInForeground = true;
                    } else {
                        var result = NcResult.Error (statusEvent.Status.SubKind, statusEvent.Status.Why);
                        FailWithResult (result);
                    }
                }
            }
        }

        void CheckComplete ()
        {
            _Attachment = McAttachment.QueryById<McAttachment> (_Attachment.Id);
            if (_Attachment == null) {
                FailWithResult (NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed, NcResult.WhyEnum.Unknown));
            } else {
                if (_Attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                    DownloadComplete ();
                }
            }
        }

        void DownloadComplete ()
        {
            StopListeningForStatusIndicator ();
            IndicateSuccess ();
        }

        void StartListeningForStatusIndicator ()
        {
            if (!IsListeningForStatusIndicator) {
                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
                IsListeningForStatusIndicator = true;
            }
        }

        void StopListeningForStatusIndicator ()
        {
            if (IsListeningForStatusIndicator) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                IsListeningForStatusIndicator = false;
            }
        }

        void ClearPending ()
        {
            var localAccountId = Attachment.AccountId;
            var localDownloadToken = DownloadToken;
            NcTask.Run (delegate {
                foreach (var request in McPending.QueryByToken (localAccountId, localDownloadToken)) {
                    if (request.State == McPending.StateEnum.Failed) {
                        request.Delete ();
                    }
                }
            }, "AttachmentDownloader_ClearPending");
        }

        void FailWithResult (NcResult result)
        {
            StopListeningForStatusIndicator ();
            if (Delegate != null) {
                Delegate.AttachmentDownloadDidFail (this, result);
            }
        }

        void IndicateSuccess ()
        {
            if (Delegate != null){
                Delegate.AttachmentDownloadDidFinish (this);
            }
        }

    }
}

