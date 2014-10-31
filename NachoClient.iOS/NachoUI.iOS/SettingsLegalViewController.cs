// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;
using System.Net;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using NachoPlatform;


namespace NachoClient.iOS
{
    public partial class SettingsLegalViewController : NcUIViewControllerNoLeaks
    {
        protected string url;
        protected string navigationBarTitle;
        protected string key;
        protected bool loadFromWeb;

        protected string urlSourceCode;
        protected float yOffset;

        protected const string CACHE_MODULE = "CACHE";

        UIScrollView scrollView = new UIScrollView();
        UIView contentView = new UIView();

        protected const int WEB_VIEW_TAG = 100;
        protected const int INTERIOR_VIEW_TAG = 101;
        protected const int TEXT_VIEW_TAG = 102;

        protected UIBarButtonItem backButton;

        public SettingsLegalViewController (IntPtr handle) : base (handle)
        {
        }

        public void SetProperties (string url, string navigationBarTitle, string key, bool loadFromWeb)
        {
            this.url = url;
            this.navigationBarTitle = navigationBarTitle;
            this.key = key;
            this.loadFromWeb = loadFromWeb;
            NavigationItem.Title = navigationBarTitle;
        }

        protected override void CreateViewHierarchy ()
        {
            scrollView.AddSubview (contentView);
            View.AddSubview (scrollView);

            backButton = new UIBarButtonItem();
            backButton.Clicked += BackButtonClicked;
            backButton.Image = UIImage.FromBundle ("nav-backarrow");
            backButton.TintColor = A.Color_NachoBlue;
            NavigationItem.LeftBarButtonItem = backButton;

            View.BackgroundColor = A.Color_NachoBackgroundGray;

            yOffset = 20;

            UIView interiorView = new UIView (new RectangleF (12, yOffset, View.Frame.Width - 24, View.Frame.Height - 100));
            interiorView.BackgroundColor = UIColor.White;
            interiorView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            interiorView.Layer.BorderWidth = 1.0f;
            interiorView.Layer.CornerRadius = GeneralSettingsViewController.VIEW_CORNER_RADIUS;
            interiorView.Tag = INTERIOR_VIEW_TAG;
            UIImageView nachoLogoImageView;
            using (var nachoLogo = UIImage.FromBundle ("Bootscreen-1")) {
                nachoLogoImageView = new UIImageView (nachoLogo);
            }
            nachoLogoImageView.Frame = new RectangleF (interiorView.Frame.Width / 2 - 40, 18, 80, 80);
            interiorView.Add (nachoLogoImageView);

            yOffset = nachoLogoImageView.Frame.Bottom + 15;

            if (loadFromWeb) {
                UIWebView webView = new UIWebView (new RectangleF (10, yOffset, interiorView.Frame.Width - 20, interiorView.Frame.Height - yOffset - 10));
                webView.Tag = WEB_VIEW_TAG;
                interiorView.Add (webView);
                contentView.AddSubview (interiorView);
                if (hasNetworkConnection ()) {
                    urlSourceCode = new WebClient ().DownloadString (url);
                    webView.LoadHtmlString (urlSourceCode, new NSUrl("about:blank"));
                    webView.LoadError += HandleLoadError;
                    webView.LoadFinished += CacheUrlHtml;
                } else {
                    HandleLoadError (this, null);
                }
            } else {
                UITextView textView = new UITextView (new RectangleF(10, yOffset, interiorView.Frame.Width - 20, interiorView.Frame.Height - yOffset - 10));
                textView.Tag = TEXT_VIEW_TAG; 
                textView.Text =  System.IO.File.ReadAllText(url);
                SizeF newSize = textView.SizeThatFits (new SizeF (textView.Frame.Width, float.MaxValue));
                textView.Frame = new RectangleF (textView.Frame.X, textView.Frame.Y, textView.Frame.Width, newSize.Height);
                interiorView.Add (textView);
                contentView.AddSubview (interiorView);
                LayoutView ();
            }
            yOffset += 20;
        }

        void CacheUrlHtml (object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty (urlSourceCode)) {
                McMutables.Set (LoginHelpers.GetCurrentAccountId (), CACHE_MODULE, key, urlSourceCode);
            }
            LayoutView ();
        }

        void CacheLoaded (object sender, EventArgs e)
        {
            LayoutView ();
        }

        void HandleLoadError (object sender, UIWebErrorArgs e)
        {
            UIWebView webView = (UIWebView)View.ViewWithTag (WEB_VIEW_TAG);
            string urlHtml = McMutables.GetOrCreate (LoginHelpers.GetCurrentAccountId (), CACHE_MODULE, key, "");
            if (!string.IsNullOrEmpty (urlHtml)) {
                webView.LoadHtmlString (urlHtml, new NSUrl("about:blank"));
            } else {
                webView.LoadHtmlString ("<h2>Sorry, you will need an internet connection to view this information.&nbsp;</h2>", new NSUrl("about:blank"));
            }
            webView.LoadFinished += CacheLoaded;
        }

        public bool hasNetworkConnection ()
        {
            if (NcCommStatus.Instance.Status != NetStatusStatusEnum.Up) {
                return false;
            } else {
                return true;
            }
        }

        protected override void ConfigureAndLayout ()
        {

        }

        protected void LayoutView()
        {
            int textViewPadding = 0;
            UIWebView theWebView = (UIWebView)View.ViewWithTag(WEB_VIEW_TAG);
            UITextView theTextView = (UITextView)View.ViewWithTag (TEXT_VIEW_TAG);
            SizeF dynamicSize = new SizeF (0, 0);

            if (null != theWebView) {
                RectangleF tempFrame = theWebView.Frame;
                tempFrame.Size = new SizeF (tempFrame.Width, 1);
                theWebView.Frame = tempFrame;
                dynamicSize = theWebView.SizeThatFits (SizeF.Empty);
                tempFrame.Size = dynamicSize;
                theWebView.Frame = tempFrame;
            }

            if (null != theTextView) {
                dynamicSize = new SizeF (0, theTextView.Frame.Height);
                textViewPadding = 60;
            }

            UIView interiorView = (UIView)View.ViewWithTag (INTERIOR_VIEW_TAG);
            interiorView.Frame = new RectangleF (interiorView.Frame.X, interiorView.Frame.Y, interiorView.Frame.Width, yOffset + dynamicSize.Height);
            scrollView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height);
            scrollView.ContentSize = new SizeF(View.Frame.Width, yOffset + dynamicSize.Height + 40 + textViewPadding);
        }

        protected override void Cleanup ()
        {
            backButton.Clicked -= BackButtonClicked;
            backButton = null;

            UIWebView webView = (UIWebView)View.ViewWithTag (WEB_VIEW_TAG);
            webView.StopLoading ();
            webView.LoadError -= HandleLoadError;
            webView.LoadFinished -= CacheUrlHtml;
            webView.LoadFinished -= CacheLoaded;
            webView = null;
        }

        protected void BackButtonClicked (object sender, EventArgs e)
        {
            DismissViewController (true, null);
        }    
    }
}
