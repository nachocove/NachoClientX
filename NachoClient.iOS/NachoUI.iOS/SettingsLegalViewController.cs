// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;

namespace NachoClient.iOS
{
    public partial class SettingsLegalViewController : NcUIViewController
    {
        protected string url;
        protected string navigationBarTitle;
        protected bool loadFromWeb;

        public SettingsLegalViewController (IntPtr handle) : base (handle)
        {
        }

        public void SetProperties (string url, string navigationBarTitle, bool loadFromWeb)
        {
            this.url = url;
            this.navigationBarTitle = navigationBarTitle;
            this.loadFromWeb = loadFromWeb;
            NavigationItem.Title = navigationBarTitle;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            UIBarButtonItem backButton = new UIBarButtonItem();
            backButton.Clicked += (object sender, EventArgs e) => {
                DismissViewController(true, null);
            };
            backButton.Image = UIImage.FromBundle ("nav-backarrow");
            backButton.TintColor = A.Color_NachoBlue;
            NavigationItem.LeftBarButtonItem = backButton;

            View.BackgroundColor = A.Color_NachoBackgroundGray;

            float yOffset = 20;

            UIView interiorView = new UIView (new RectangleF (12, yOffset, View.Frame.Width - 24, View.Frame.Height - 100));
            interiorView.BackgroundColor = UIColor.White;
            interiorView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            interiorView.Layer.BorderWidth = 1.0f;
            interiorView.Layer.CornerRadius = 6;

            UIImageView nachoLogoImageView;
            using (var nachoLogo = UIImage.FromBundle ("Bootscreen-1")) {
                nachoLogoImageView = new UIImageView (nachoLogo);
            }
            nachoLogoImageView.Frame = new RectangleF (interiorView.Frame.Width / 2 - 40, 18, 80, 80);
            interiorView.Add (nachoLogoImageView);

            yOffset = nachoLogoImageView.Frame.Bottom + 20;

            if (loadFromWeb) {
                UIWebView webView = new UIWebView (new RectangleF (10, yOffset, interiorView.Frame.Width - 20, interiorView.Frame.Height - yOffset - 10));
                webView.LoadRequest (new NSUrlRequest (new NSUrl (url)));
                interiorView.Add (webView);
            } else {
                UITextView textView = new UITextView (new RectangleF(10, yOffset, interiorView.Frame.Width - 20, interiorView.Frame.Height - yOffset - 10));
                textView.Text =  System.IO.File.ReadAllText(url);
                interiorView.Add (textView);
            }

            View.AddSubview (interiorView);
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }
    }
}
