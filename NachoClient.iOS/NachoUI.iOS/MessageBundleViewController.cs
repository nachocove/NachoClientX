//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using WebKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using Foundation;
using System.Linq;

namespace NachoClient.iOS
{
    public class MessageBundleViewController : NcUIViewControllerNoLeaks
    {

        McEmailMessage Message;
        NcEmailMessageBundle Bundle;
        WKWebView WebView;
        string DownloadToken;

        public MessageBundleViewController () : base()
        {
        }

        public void SetMessage (McEmailMessage message)
        {
            Message = message;
            Bundle = new NcEmailMessageBundle (Message);
            if (Bundle.NeedsUpdate) {
                UpdateBundle ();
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            ShowMessage ();
        }

        private void ShowMessage ()
        {
            if (!Bundle.NeedsUpdate) {
                if (Bundle.FullHtmlUrl != null) {
                    var request = new NSUrlRequest (new NSUrl (Bundle.FullHtmlUrl.AbsoluteUri));
                    WebView.LoadRequest (request);
                } else {
                    var html = Bundle.FullHtml;
                    WebView.LoadHtmlString (new NSString (html), new NSUrl (Bundle.BaseUrl.AbsoluteUri));
                }
            }
        }

        private void UpdateBundle ()
        {
            if (Bundle.NeedsUpdate) {
                if (!McAbstrFileDesc.IsNontruncatedBodyComplete (Message.GetBody ())) {
                    DownloadBody ();
                } else {
                    Bundle.Update ();
                    if (IsViewLoaded) {
                        ShowMessage ();
                    }
                }
            }
        }

        private void DownloadBody ()
        {
            // Download the body.
            NcResult result = BackEnd.Instance.DnldEmailBodyCmd (Message.AccountId, Message.Id, true);

            if (result.isError ()) {
                DownloadToken = null;
            } else {
                DownloadToken = result.GetValue<string> ();
            }
            if (DownloadToken != null) {
                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
                McPending.Prioritize (Message.AccountId, DownloadToken);
            } else {
                Log.Error (Log.LOG_UI, "Error starting download of body");
            }
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var statusEvent = (StatusIndEventArgs)e;
            if (DownloadToken == null) {
                return;
            }

            if (null != statusEvent.Tokens && statusEvent.Tokens.FirstOrDefault () == DownloadToken) {
                if (statusEvent.Status.SubKind == NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded) {
                    DownloadToken = null;
                    NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
                    UpdateBundle ();
                }else if (statusEvent.Status.SubKind == NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed){
                    Log.Error (Log.LOG_UI, "Error downloading body");
                    DownloadToken = null;
                    NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
                }
            }
        }

        protected override void CreateViewHierarchy ()
        {
            var config = new WKWebViewConfiguration ();
            config.Preferences.JavaScriptCanOpenWindowsAutomatically = false;
            config.Preferences.JavaScriptEnabled = true;
            WebView = new WKWebView (View.Bounds, config);
            WebView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            View.AddSubview (WebView);
        }

        protected override void ConfigureAndLayout ()
        {
        }

        protected override void Cleanup ()
        {
        }
    }
}

