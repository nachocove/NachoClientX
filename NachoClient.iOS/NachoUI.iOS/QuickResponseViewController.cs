// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using System.Collections.Generic;
using Foundation;
using UIKit;
using UIImageEffectsBinding;
using NachoCore.Brain;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public partial class QuickResponseViewController : NcUIViewController
    {
        MessageComposeViewController owner;
        protected nfloat yOffset;
        protected NcQuickResponse ncQuick;

        protected static readonly nfloat X_INDENT = 30;

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

        public void SetOwner (MessageComposeViewController owner)
        {
            this.owner = owner;
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
                var dismissButton = new UIBarButtonItem (image, UIBarButtonItemStyle.Plain, null);
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
                quickButton.SetTitleColor (UIColor.White, UIControlState.Normal);
                quickButton.Font = A.Font_AvenirNextDemiBold14;
                quickButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
                quickButton.TouchUpInside += (object sender, EventArgs e) => {
                    if (null == response.intent) {
                        owner.PopulateMessageFromSelectedIntent (McEmailMessage.IntentType.None, MessageDeferralType.None, DateTime.MinValue);
                    } else {
                        owner.PopulateMessageFromSelectedIntent (response.intent.type, MessageDeferralType.None, DateTime.MinValue);
                    }
                    owner.PopulateMessageFromQR (ncQuick.whatType, response);
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
