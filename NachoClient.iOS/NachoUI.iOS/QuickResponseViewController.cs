// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using System.Collections.Generic;
using Foundation;
using UIKit;
using NachoCore.Brain;
using NachoCore.Model;

namespace NachoClient.iOS
{

    public interface QuickResponseViewControllerDelegate {

        void QuickResponseViewDidSelectResponse (QuickResponseViewController vc, NcQuickResponse.QRTypeEnum whatType, NcQuickResponse.QuickResponse response, McEmailMessage.IntentType intentType);
    }

    public partial class QuickResponseViewController : NcUIViewController
    {
        public QuickResponseViewControllerDelegate ResponseDelegate;
        protected nfloat yOffset;
        protected NcQuickResponse ncQuick;

        protected static readonly nfloat X_INDENT = 30;

        public QuickResponseViewController () : base ()
        {
            ModalTransitionStyle = UIModalTransitionStyle.CrossDissolve;
        }

        public QuickResponseViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            CreateView ();
        }

        public void SetProperties (NcQuickResponse.QRTypeEnum whatType)
        {
            ncQuick = new NcQuickResponse (whatType);
        }

        public override UIStatusBarStyle PreferredStatusBarStyle ()
        {
            return UIStatusBarStyle.LightContent;
        }

        public void CreateView ()
        {
            UIView qrView = new UIView (View.Frame);
            qrView.BackgroundColor = A.Color_NachoGreen;

            var navBar = new UINavigationBar (new CGRect (0, 20, View.Frame.Width, 44));
            navBar.BarStyle = UIBarStyle.Default;
            navBar.Opaque = true;
            navBar.Translucent = false;

            var navItem = new UINavigationItem ("Quick Messages");
            using (var image = UIImage.FromBundle ("modal-close")) {
                var dismissButton = new NcUIBarButtonItem (image, UIBarButtonItemStyle.Plain, null);
                dismissButton.AccessibilityLabel = "Close";
                dismissButton.Clicked += (object sender, EventArgs e) => {
                    DismissViewController (true, null);
                };
                navItem.LeftBarButtonItem = dismissButton;
            }
            navBar.Items = new UINavigationItem[] { navItem };

            qrView.AddSubview (navBar);

            yOffset = 64;

            Util.AddHorizontalLine (0, yOffset, qrView.Frame.Width, UIColor.LightGray, qrView);
            yOffset += 2;

            int curItem = 0;
            foreach (var response in ncQuick.GetResponseList()) {
                curItem++;
                UIButton quickButton = new UIButton (new CGRect (X_INDENT, yOffset, qrView.Frame.Width - 30, 40));
                quickButton.BackgroundColor = A.Color_NachoGreen;
                quickButton.SetTitle (response.body, UIControlState.Normal);
                quickButton.AccessibilityLabel = response.body;
                quickButton.SetTitleColor (UIColor.White, UIControlState.Normal);
                quickButton.Font = A.Font_AvenirNextDemiBold14;
                quickButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
                quickButton.TouchUpInside += (object sender, EventArgs e) => {
                    McEmailMessage.IntentType intentType;
                    if (null == response.intent) {
                        intentType = McEmailMessage.IntentType.None;
                    } else {
                        intentType = response.intent.type;
                    }
                    if (ResponseDelegate != null){
                        ResponseDelegate.QuickResponseViewDidSelectResponse (this, ncQuick.whatType, response, intentType);
                    }
                    DismissViewController (true, null);
                };
                qrView.Add (quickButton);
                Util.AddHorizontalLine (X_INDENT, quickButton.Frame.Bottom, View.Frame.Width - X_INDENT, UIColor.LightGray, qrView);
                yOffset = quickButton.Frame.Bottom + 1;
            }
            this.Add (qrView);
        }
    }
}
