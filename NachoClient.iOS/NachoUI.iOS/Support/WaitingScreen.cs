//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using CoreGraphics;
using System.Collections.Generic;
using Foundation;
using UIKit;
using UIImageEffectsBinding;
using CoreAnimation;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    [Register ("WaitingScreen")]

    public class WaitingScreen: UIView
    {
        AdvancedLoginViewController owner;

        protected UILabel syncStatusLabel;
        protected UIImageView startedCircleImage;
        protected UIImageView finishedCircleImage;
        protected UIImageView firstTrianglesImage;
        protected UIImageView secondTriangleImage;
        protected UILabel welcomeToLabel;
        protected UILabel nachoMailLabel;
        protected UIImageView topHalfSpinner;
        protected UIImageView bottomHalfSpinner;
        protected UIImageView circleMask;
        protected UIView spinnerView;
        protected UIView animationBlocker;
        protected UIButton supportButton;
        protected UIButton dismissButton;
        protected const int SPINNER_WIDTH = 150;
        protected const int SPINNER_HEIGHT = 338;
        protected const int MASK_DIAMETER = 80;
        protected nfloat LOWER_SECTION_Y_VAL;
        protected CGPoint topHalfSpinnerCenter;
        protected CGPoint bottomHalfSpinnerCenter;

        public WaitingScreen ()
        {
        }

        public WaitingScreen (IntPtr handle) : base (handle)
        {
        }

        public WaitingScreen (CGRect rect, AdvancedLoginViewController owner) : base (rect)
        {
            this.owner = owner;

            this.BackgroundColor = A.Color_NachoGreen;
            LOWER_SECTION_Y_VAL = this.Frame.Height - 437 + 64 + 64;

            spinnerView = new UIView (new CGRect (this.Frame.Width / 2 - 40, LOWER_SECTION_Y_VAL, MASK_DIAMETER, MASK_DIAMETER));
            spinnerView.BackgroundColor = A.Color_NachoRed;
            spinnerView.Layer.CornerRadius = MASK_DIAMETER / 2;
            spinnerView.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            spinnerView.Layer.MasksToBounds = true;
            this.AddSubview (spinnerView);

            bottomHalfSpinner = new UIImageView (UIImage.FromBundle ("Spinner-1@2x"));
            bottomHalfSpinner.Frame = new CGRect (-35, -180, SPINNER_WIDTH, SPINNER_HEIGHT);
            bottomHalfSpinnerCenter = bottomHalfSpinner.Center;
            spinnerView.AddSubview (bottomHalfSpinner);

            topHalfSpinner = new UIImageView (UIImage.FromBundle ("Spinner-1@2x"));
            topHalfSpinner.Frame = new CGRect (-35, -370, SPINNER_WIDTH, SPINNER_HEIGHT);
            topHalfSpinnerCenter = topHalfSpinner.Center;
            spinnerView.AddSubview (topHalfSpinner);

            circleMask = new UIImageView (maskImage (UIImage.FromBundle ("Circular-Mask")));
            circleMask.Frame = new CGRect (this.Frame.Width / 2, LOWER_SECTION_Y_VAL + MASK_DIAMETER / 2, .5f, .5f);
            circleMask.Layer.CornerRadius = MASK_DIAMETER / 2;
            circleMask.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            circleMask.Layer.MasksToBounds = true;
            this.AddSubview (circleMask);

            animationBlocker = new UIView (new CGRect (this.Frame.Width / 2 - 40, LOWER_SECTION_Y_VAL, MASK_DIAMETER, MASK_DIAMETER));
            animationBlocker.Alpha = 1.0f;
            animationBlocker.BackgroundColor = A.Color_NachoGreen;
            this.AddSubview (animationBlocker);

            syncStatusLabel = new UILabel (new CGRect (0, LOWER_SECTION_Y_VAL + 89, this.Frame.Width, 30));
            syncStatusLabel.Font = A.Font_AvenirNextRegular17;
            syncStatusLabel.TextColor = UIColor.White;
            syncStatusLabel.TextAlignment = UITextAlignment.Center;
            syncStatusLabel.Text = "Verifying Your Server...";
            syncStatusLabel.Alpha = 0.0f;
            this.AddSubview (syncStatusLabel);

            startedCircleImage = new UIImageView (UIImage.FromBundle ("Loginscreen-BG@2x"));
            startedCircleImage.Frame = new CGRect (this.Frame.Width / 2 - .5f, LOWER_SECTION_Y_VAL, 1, 1);
            startedCircleImage.Alpha = 0.0f;
            this.AddSubview (startedCircleImage);

            finishedCircleImage = new UIImageView (UIImage.FromBundle ("Bootscreen-1@2x"));
            finishedCircleImage.Frame = new CGRect (this.Frame.Width / 2 - .5f, LOWER_SECTION_Y_VAL, 1, 1);
            finishedCircleImage.Alpha = 0.0f;
            this.AddSubview (finishedCircleImage);

            firstTrianglesImage = new UIImageView (UIImage.FromBundle ("Bootscreen-3@2x"));
            firstTrianglesImage.Frame = new CGRect (0, LOWER_SECTION_Y_VAL + 309, this.Frame.Width, 39);
            this.AddSubview (firstTrianglesImage);

            secondTriangleImage = new UIImageView (UIImage.FromBundle ("Bootscreen-2@2x"));
            secondTriangleImage.Frame = new CGRect (40, LOWER_SECTION_Y_VAL + 309, this.Frame.Width - 80, 25);
            this.AddSubview (secondTriangleImage);

            welcomeToLabel = new UILabel (new CGRect (this.Frame.Width / 2 - (120 / 2), LOWER_SECTION_Y_VAL + 89, 120, 20));
            welcomeToLabel.Font = A.Font_AvenirNextRegular17;
            welcomeToLabel.TextColor = UIColor.White;
            welcomeToLabel.Text = "Welcome To";
            welcomeToLabel.Alpha = 0.0f;
            welcomeToLabel.TextAlignment = UITextAlignment.Center;
            this.AddSubview (welcomeToLabel);

            nachoMailLabel = new UILabel (new CGRect (this.Frame.Width / 2 - (180 / 2), LOWER_SECTION_Y_VAL + 110, 180, 40));
            nachoMailLabel.Font = A.Font_AvenirNextDemiBold30;
            nachoMailLabel.TextColor = UIColor.White;
            nachoMailLabel.Text = "Nacho Mail";
            nachoMailLabel.Alpha = 0.0f;
            nachoMailLabel.TextAlignment = UITextAlignment.Center;
            this.AddSubview (nachoMailLabel);

            supportButton = new UIButton (UIButtonType.System);
            supportButton.SetTitle ("Customer Support", UIControlState.Normal);
            supportButton.SetTitleColor (UIColor.White, UIControlState.Normal);
            supportButton.Font = A.Font_AvenirNextRegular12;
            supportButton.ContentMode = UIViewContentMode.Center;
            supportButton.BackgroundColor = UIColor.Clear;
            supportButton.AccessibilityLabel = "Dismiss";
            supportButton.TouchUpInside += (object sender, EventArgs e) => {
                owner.SegueToSupport ();
            };
            supportButton.SizeToFit ();
            ViewFramer.Create (supportButton).Center (this.Frame.Width / 2, this.Frame.Bottom - 70);
            this.AddSubview (supportButton);

            dismissButton = new UIButton (UIButtonType.System);
            dismissButton.SetTitle ("Return to Account Setup", UIControlState.Normal);
            dismissButton.SetTitleColor (UIColor.White, UIControlState.Normal);
            dismissButton.Font = A.Font_AvenirNextRegular12;
            dismissButton.ContentMode = UIViewContentMode.Center;
            dismissButton.BackgroundColor = UIColor.Clear;
            dismissButton.AccessibilityLabel = "Dismiss";
            dismissButton.TouchUpInside += (object sender, EventArgs e) => {
                owner.ReturnToAdvanceView ();
            };
            dismissButton.SizeToFit ();
            ViewFramer.Create (dismissButton).Center (this.Frame.Width / 2, this.Frame.Bottom - 40);
            this.AddSubview (dismissButton);
        }

        protected UIImage maskImage (UIImage maskImage)
        {
            CGImage maskRef = maskImage.CGImage;

            // Xamarin Unified API bug
            int width = (int)maskRef.Width;
            int height = (int)maskRef.Height;
            int bpc = (int)maskRef.BitsPerComponent;
            int bpp = (int)maskRef.BitsPerPixel;
            int bpr = (int)maskRef.BytesPerRow;
            CGImage imageMask = CGImage.CreateMask (width, height, bpc, bpp, bpr, maskRef.DataProvider, null, true);
            return new UIImage (imageMask);
        }

        public void ShowView (string loadingMessage = null)
        {
            if (this.Hidden) {
                this.Hidden = false;
                this.Superview.BringSubviewToFront (this);
                owner.NavigationItem.Title = "";
                Util.ConfigureNavBar (true, owner.NavigationController);
                ResetLoadingItems ();
                StartLoadingAnimation ();
            }
            // Does the loading message need to change?
            if (!String.IsNullOrEmpty (loadingMessage) && !syncStatusLabel.Text.Equals (loadingMessage)) {
                UIView.AnimateKeyframes (.5, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                    UIView.AddKeyframeWithRelativeStartTime (0, .5, () => {
                        syncStatusLabel.Alpha = 0.0f;
                    });
                    UIView.AddKeyframeWithRelativeStartTime (.5, .5, () => {
                        syncStatusLabel.Text = loadingMessage;
                        syncStatusLabel.Alpha = 1.0f;
                    });

                }, ((bool finished) => {
                }));
            }
        }

        public void HideView()
        {
            DismissView ();
        }

        public void DismissView ()
        {
            bottomHalfSpinner.Layer.RemoveAllAnimations ();
            topHalfSpinner.Layer.RemoveAllAnimations ();
            owner.NavigationItem.Title = "Account Setup";
            Util.ConfigureNavBar (false, owner.NavigationController);
            this.Hidden = true;
        }

        protected void ResetLoadingItems ()
        {
            syncStatusLabel.Alpha = 0.0f;
            circleMask.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            spinnerView.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            bottomHalfSpinner.Center = bottomHalfSpinnerCenter;
            topHalfSpinner.Center = topHalfSpinnerCenter;
            spinnerView.BringSubviewToFront (topHalfSpinner);
        }

        protected void StartLoadingAnimation ()
        {
            UIView.AnimateKeyframes (1, 0, (UIViewKeyframeAnimationOptions.OverrideInheritedDuration | UIViewKeyframeAnimationOptions.CalculationModeCubicPaced), () => {

                UIView.AddKeyframeWithRelativeStartTime (0, .5, () => {
                    circleMask.Layer.Transform = CATransform3D.MakeScale (1.0f, 1.0f, 1.0f);
                    spinnerView.Layer.Transform = CATransform3D.MakeScale (1.0f, 1.0f, 1.0f);
                    animationBlocker.Alpha = 0.0f;
                });

                UIView.AddKeyframeWithRelativeStartTime (.5, .5, () => {
                    syncStatusLabel.Alpha = 1.0f;
                });

            }, ((bool finished) => {
                ArrowAnimation (topHalfSpinner, bottomHalfSpinner, topHalfSpinnerCenter, bottomHalfSpinnerCenter, false);
            }));
        }

        private void ArrowAnimation (UIImageView theTopSpinner, UIImageView theBottomSpinner, CGPoint topSpinnerCenter, CGPoint bottomSpinnerCenter, bool bottomIsOnTop)
        {
            Animate (3, 0, (UIViewAnimationOptions.Repeat | UIViewAnimationOptions.OverrideInheritedDuration | UIViewAnimationOptions.OverrideInheritedOptions | UIViewAnimationOptions.OverrideInheritedCurve | UIViewAnimationOptions.CurveLinear), () => {
                theTopSpinner.Center = new CGPoint (topSpinnerCenter.X, topSpinnerCenter.Y + 190f);
                theBottomSpinner.Center = new CGPoint (bottomSpinnerCenter.X, bottomSpinnerCenter.Y + 190f);
            }, (() => { 
            }));
        }

        public void StartSyncedEmailAnimation (int accountId)
        {
            UIView.AnimateKeyframes (4, .1, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {

                UIView.AddKeyframeWithRelativeStartTime (0, .075, () => {
                    dismissButton.Alpha = 0.0f;
                    supportButton.Alpha = 0.0f;
                    syncStatusLabel.Alpha = 0.0f;
                });

                UIView.AddKeyframeWithRelativeStartTime (.075, .075, () => {
                    circleMask.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
                    animationBlocker.Alpha = 1.0f;
                });

                UIView.AddKeyframeWithRelativeStartTime (.15, .075, () => {
                    startedCircleImage.Alpha = 1.0f;
                    startedCircleImage.Transform = CGAffineTransform.MakeScale (120, 120);
                    firstTrianglesImage.Frame = new CGRect (firstTrianglesImage.Frame.X, firstTrianglesImage.Frame.Y - 39, firstTrianglesImage.Frame.Width, firstTrianglesImage.Frame.Height);
                });

                UIView.AddKeyframeWithRelativeStartTime (.225, .03, () => {
                    finishedCircleImage.Alpha = 1.0f;
                    finishedCircleImage.Transform = CGAffineTransform.MakeScale (120, 120);
                });

                UIView.AddKeyframeWithRelativeStartTime (.225, .075, () => {
                    secondTriangleImage.Frame = new CGRect (secondTriangleImage.Frame.X, secondTriangleImage.Frame.Y - 25, secondTriangleImage.Frame.Width, secondTriangleImage.Frame.Height);
                });

                UIView.AddKeyframeWithRelativeStartTime (.3, .075, () => {
                    welcomeToLabel.Alpha = 1.0f;
                    nachoMailLabel.Alpha = 1.0f;
                });

            }, ((bool finished) => {
                owner.FinishedSyncedEmailAnimation (accountId);
            }));
        }
    }
}
