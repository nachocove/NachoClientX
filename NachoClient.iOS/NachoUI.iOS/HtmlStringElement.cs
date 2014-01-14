//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using MonoTouch.Foundation;

namespace NachoClient.iOS
{
    /// <summary>
    ///  Used to display a cell with an HTML string
    /// </summary>
    public class HtmlStringElement : Element
    {
        string Html;
        UIWebView web;
        static NSString hkey = new NSString ("HtmlStringElement");

        public HtmlStringElement (string caption, string html) : base (caption)
        {
            Html = html;
        }

        protected override NSString CellKey {
            get {
                return hkey;
            }
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            var cell = tv.DequeueReusableCell (CellKey);
            if (cell == null) {
                cell = new UITableViewCell (UITableViewCellStyle.Default, CellKey);
                cell.SelectionStyle = UITableViewCellSelectionStyle.Blue;
            }
            cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;

            cell.TextLabel.Text = Caption;
            return cell;
        }

        static bool NetworkActivity {
            set {
                UIApplication.SharedApplication.NetworkActivityIndicatorVisible = value;
            }
        }
        // We use this class to dispose the web control when it is not
        // in use, as it could be a bit of a pig, and we do not want to
        // wait for the GC to kick-in.
        class WebViewController : UIViewController
        {
            HtmlStringElement container;

            public WebViewController (HtmlStringElement container) : base ()
            {
                this.container = container;
            }

            public bool Autorotate { get; set; }

            public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
            {
                return Autorotate;
            }
        }

        public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
        {
            int i = 0;
            var vc = new WebViewController (this) {
                Autorotate = dvc.Autorotate
            };

            web = new UIWebView (UIScreen.MainScreen.Bounds) {
                BackgroundColor = UIColor.White,
                ScalesPageToFit = true,
                AutoresizingMask = UIViewAutoresizing.All
            };
            web.LoadStarted += delegate {
                // this is called several times and only one UIActivityIndicatorView is needed
                if (i++ == 0) {
                    var indicator = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.White);
                    vc.NavigationItem.RightBarButtonItem = new UIBarButtonItem (indicator);
                    indicator.StartAnimating ();
                }
                NetworkActivity = true;
            };
            web.LoadFinished += delegate {
                if (--i == 0) {
                    // we stopped loading, remove indicator and dispose of UIWebView
                    vc.NavigationItem.RightBarButtonItem = null;
                    web.StopLoading ();
                    web.Dispose ();
                }
                NetworkActivity = false;
            };
            web.LoadError += (webview, args) => {
                NetworkActivity = false;
                vc.NavigationItem.RightBarButtonItem = null;
                if (web != null) {
                    web.LoadHtmlString (String.Format ("<html><center><font size=+5 color='red'>{0}:<br>{1}</font></center></html>", "An error occurred:", args.Error.LocalizedDescription), null);
                }
            };
            vc.NavigationItem.Title = Caption;

            vc.View.AutosizesSubviews = true;
            vc.View.AddSubview (web);

            dvc.ActivateController (vc);
            web.LoadHtmlString (Html, null);
        }
    }
}

