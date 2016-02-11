//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoCore.Model;
using System.Linq;
using NachoPlatform;

namespace NachoCore.Utils
{

    public interface MessageDownloadDelegate {

        void MessageDownloadDidFinish (MessageDownloader downloader);
        void MessageDownloadDidFail (MessageDownloader downloader, NcResult result);
    }

    public class MessageDownloader
    {
        
        public MessageDownloadDelegate Delegate;
        public bool CreateBundleIfNeeded = true;
        public McEmailMessage Message {
            get {
                return _Message;
            }
        }
        public NcEmailMessageBundle Bundle;
        private McEmailMessage _Message;
        private string DownloadToken;
        private bool IsListeningForStatusIndicator;
        private bool WaitingToDownloadInForeground;

        public MessageDownloader ()
        {
        }

        public void Download (McEmailMessage message)
        {
            _Message = message;
            if (Message.BodyId != 0) {
                var body = McBody.QueryById<McBody> (Message.BodyId);
                if (McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                    DownloadComplete ();
                } else {
                    EnqueueDownload ();
                }
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
            var result = BackEnd.Instance.DnldEmailBodyCmd (Message.AccountId, Message.Id, true);
            if (result.isError ()) {
                if (result.SubKind != NcResult.SubKindEnum.Error_FilePresenceIsComplete) {
                    Log.Warn (Log.LOG_UI, "DnldEmailBodyCmd({0}:{1}) failed with error: {2}", Message.Id, Message.AccountId, result);
                }
                DownloadToken = null;
            } else {
                DownloadToken = result.GetValue<string> ();
            }
            if (DownloadToken == null) {
                // Race condition: need to double check the message is still there & the body is still missing
                var message = McEmailMessage.QueryById<McEmailMessage> (Message.Id);
                if (message != null) {
                    var body = message.GetBody ();
                    if (McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                        // we hit the race condition
                        _Message = message;
                        DownloadComplete ();
                    } else {
                        // Download failed
                        Log.Warn (Log.LOG_UI, "Failed to start body download for message {0} in account {1}", Message.Id, Message.AccountId);
                        FailWithResult (result);
                    }
                } else {
                    // The message has been deleted
                    Log.Warn (Log.LOG_UI, "Failed to start body download for message {0} in account {1}, and it looks like the message has been deleted.", Message.Id, Message.AccountId);
                    if (Delegate != null) {
                        Delegate.MessageDownloadDidFail (this, NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing));
                    }
                }
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
                if (statusEvent.Status.SubKind == NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded) {
                    DownloadComplete ();
                } else if (statusEvent.Status.SubKind == NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed) {
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

        void DownloadComplete ()
        {
            StopListeningForStatusIndicator ();
            var message = McEmailMessage.QueryById<McEmailMessage> (Message.Id);
            Message.BodyId = message.BodyId;
            if (CreateBundleIfNeeded) {
                CheckBundle ();
            } else {
                IndicateSuccess ();
            }
        }

        void CheckBundle ()
        {
            if (Bundle == null) {
                Bundle = new NcEmailMessageBundle (Message);
            }
            if (Bundle.NeedsUpdate) {
                NcTask.Run (delegate {
                    Bundle.Update ();
                    InvokeOnUIThread.Instance.Invoke (BundleUpdated);
                }, "MessageDownloader_UpdateBundle");
            } else {
                BundleUpdated ();
            }
        }

        void BundleUpdated ()
        {
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
            var localAccountId = Message.AccountId;
            var localDownloadToken = DownloadToken;
            NcTask.Run (delegate {
                foreach (var request in McPending.QueryByToken (localAccountId, localDownloadToken)) {
                    if (request.State == McPending.StateEnum.Failed) {
                        request.Delete ();
                    }
                }
            }, "MessageDownloader_ClearPending");
        }

        void FailWithResult (NcResult result)
        {
            StopListeningForStatusIndicator ();
            if (Delegate != null) {
                Delegate.MessageDownloadDidFail (this, result);
            }
        }

        void IndicateSuccess ()
        {
            if (Delegate != null){
                Delegate.MessageDownloadDidFinish (this);
            }
        }

    }
}

