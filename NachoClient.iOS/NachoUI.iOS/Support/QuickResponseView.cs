//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using UIImageEffectsBinding;
using MonoTouch.CoreGraphics;
using NachoCore.Brain;
using NachoCore.Model;

namespace NachoClient.iOS
{
    [Register ("QuickResponseView")]

    public class QuickResponseView: UIView
    {
        MessageComposeViewController owner;
        protected float yOffset;
        protected NcQuickResponse ncQuick;
        protected McEmailMessage emailMessage;

        public QuickResponseView (NcQuickResponse.QRTypeEnum whatType, ref McEmailMessage emailMessage)
        {
            ncQuick = new NcQuickResponse (whatType);
            this.emailMessage = emailMessage;
        }

        public void SetOwner (MessageComposeViewController owner)
        {
            this.owner = owner;
        }

        public QuickResponseView (IntPtr handle) : base (handle)
        {

        }

        public void CreateView ()
        {
            this.BackgroundColor = UIColor.DarkGray.ColorWithAlpha (.85f);
            this.Frame = owner.View.Frame;

            float viewHeight = 64 + (ncQuick.GetResponseList ().Count * 41);
            UIView viewBody = new UIView ();
            viewBody.Layer.CornerRadius = 8f;
            viewBody.Frame = new RectangleF (15, (owner.View.Frame.Height - viewHeight) / 2.0f - 50, owner.View.Frame.Width - 30, viewHeight);
            viewBody.BackgroundColor = UIColor.White;

            yOffset = 14;

            UILabel quickMessageLabel = new UILabel (new RectangleF (viewBody.Frame.Width / 2 - 80, yOffset, 160, 25));
            quickMessageLabel.Text = "Quick Messages";
            quickMessageLabel.TextAlignment = UITextAlignment.Center;
            quickMessageLabel.Font = A.Font_AvenirNextRegular17;
            quickMessageLabel.TextColor = A.Color_NachoDarkText;
            viewBody.Add (quickMessageLabel);

            UIButton dismissView = new UIButton (new RectangleF (20, yOffset + 2, 20, 20));
            dismissView.SetImage (UIImage.FromBundle ("icn-close"), UIControlState.Normal);
            dismissView.TouchUpInside += (object sender, EventArgs e) => {
                this.DismissView ();
            };
            viewBody.Add (dismissView);

            yOffset = quickMessageLabel.Frame.Bottom + 16;

            Util.AddHorizontalLine (0, yOffset - 5, viewBody.Frame.Width, A.Color_NachoLightBorderGray, viewBody);

            int curItem = 0;
            foreach (var response in ncQuick.GetResponseList()) {
                curItem++;
                UIButton quickButton = new UIButton (new RectangleF (20, yOffset, viewBody.Frame.Width - 20, 40));
                quickButton.BackgroundColor = UIColor.White;
                quickButton.SetTitle (response.body, UIControlState.Normal);
                quickButton.SetTitleColor (A.Color_NachoTextGray, UIControlState.Normal);
                quickButton.Font = A.Font_AvenirNextRegular14;
                quickButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
                quickButton.TouchUpInside += (object sender, EventArgs e) => {
                    ncQuick.CreateQuickResponse (response, ref emailMessage);
                    owner.PopulateMessageFromQR (ncQuick.whatType);
                    this.DismissView ();
                };

                viewBody.Add (quickButton);
                if (curItem < ncQuick.GetResponseList ().Count) {
                    Util.AddHorizontalLine (20, quickButton.Frame.Bottom, quickButton.Frame.Width, A.Color_NachoLightBorderGray, viewBody);
                }

                yOffset = quickButton.Frame.Bottom + 1;
            }

            this.Add (viewBody);
            owner.View.Add (this);
            this.Hidden = true;
        }

        public void ShowView ()
        {
            this.Hidden = false;
        }

        public void DismissView ()
        {
            this.Hidden = true;
        }

    }
}
