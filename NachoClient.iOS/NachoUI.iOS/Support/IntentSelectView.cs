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
    [Register ("IntentSelectView")]

    public class IntentSelectView: UIView
    {
        MessageComposeViewController owner;
        protected float yOffset;

        public enum MessageType
        {
            Compose,
            Reply,
            Forward,
        }

        NcMessageIntent messageIntent;
        McEmailMessage emailMessage;

        public IntentSelectView (ref NcMessageIntent messageIntent, ref McEmailMessage emailMessage)
        {
            this.messageIntent = messageIntent;
            this.emailMessage = emailMessage;
        }

        public void SetOwner (MessageComposeViewController owner)
        {
            this.owner = owner;
        }

        public IntentSelectView (IntPtr handle) : base (handle)
        {

        }

        public void CreateView ()
        {
            this.BackgroundColor = UIColor.DarkGray.ColorWithAlpha (.85f);
            this.Frame = new RectangleF(0, 0, owner.View.Frame.Width, owner.View.Frame.Height);
            this.Tag = 101;

            float viewHeight = 64 + (messageIntent.GetIntentList ().Count * 41);
            UIView viewBody = new UIView ();
            viewBody.Layer.CornerRadius = 8f;
            viewBody.Frame = new RectangleF (15, (owner.View.Frame.Height - viewHeight) / 2.0f - 50, owner.View.Frame.Width - 30, viewHeight);
            viewBody.BackgroundColor = UIColor.White;

            yOffset = 14;

            UILabel messageIntentsLabel = new UILabel (new RectangleF (viewBody.Frame.Width / 2 - 80, yOffset, 160, 25));
            messageIntentsLabel.Text = "Message Intents";
            messageIntentsLabel.TextAlignment = UITextAlignment.Center;
            messageIntentsLabel.Font = A.Font_AvenirNextRegular17;
            messageIntentsLabel.TextColor = A.Color_NachoDarkText;
            viewBody.Add (messageIntentsLabel);

            UIButton dismissView = new UIButton (new RectangleF (20, yOffset + 2, 20, 20));
            dismissView.SetImage (UIImage.FromBundle ("icn-close"), UIControlState.Normal);
            dismissView.TouchUpInside += (object sender, EventArgs e) => {
                this.DismissView ();
            };
            viewBody.Add (dismissView);

            yOffset = messageIntentsLabel.Frame.Bottom + 16;

            Util.AddHorizontalLine (0, yOffset - 5, viewBody.Frame.Width, A.Color_NachoLightBorderGray, viewBody);

            int curItem = 0;
            foreach (var intent in messageIntent.GetIntentList ()) {
                curItem++;
                UIButton intentButton = new UIButton (new RectangleF (20, yOffset, viewBody.Frame.Width - 60, 40));
                intentButton.BackgroundColor = UIColor.White;
                intentButton.SetTitle (intent.value, UIControlState.Normal);
                intentButton.SetTitleColor (A.Color_NachoTextGray, UIControlState.Normal);
                intentButton.Font = A.Font_AvenirNextRegular14;
                intentButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
                intentButton.TouchUpInside += (object sender, EventArgs e) => {
                    messageIntent.SetType(intent);
                    messageIntent.SetMessageIntent (ref emailMessage);
                    owner.intentDateTypeEnum = MessageDeferralType.None;
                    owner.PopulateMessageFromSelectedIntent ();
                    this.DismissView ();
                };

                if (curItem > 2) {
                    UIButton dueDateButton = new UIButton (new RectangleF (viewBody.Frame.Width - 40, yOffset + 6f, 25, 25));
                    dueDateButton.SetImage (UIImage.FromBundle ("icn-defer"), UIControlState.Normal);
                    dueDateButton.TouchUpInside += (object sender, EventArgs e) => {
                        messageIntent.SetType(intent);
                        messageIntent.SetMessageIntent (ref emailMessage);
                        owner.PerformSegue ("SegueToMessagePriority", owner);
                    };
                    viewBody.Add (dueDateButton);
                }

                viewBody.Add (intentButton);
                if (curItem < messageIntent.GetIntentList ().Count) {
                    Util.AddHorizontalLine (20, intentButton.Frame.Bottom, viewBody.Frame.Width - 20, A.Color_NachoLightBorderGray, viewBody);
                }

                yOffset = intentButton.Frame.Bottom + 1;
            }

            this.Add (viewBody);
            owner.View.Add (this);
            this.Hidden = true;
        }

        public void ShowView ()
        {
            if (this.Hidden) {
                this.Hidden = false;
            }
        }

        public void DismissView ()
        {
            if (!this.Hidden) {
                this.Hidden = true;
            }
        }
    }
}
