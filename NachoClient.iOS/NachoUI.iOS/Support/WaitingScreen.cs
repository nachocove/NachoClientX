//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using UIImageEffectsBinding;
using MonoTouch.CoreGraphics;


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
        protected UIImageView swipeUpTriangle;
        protected UILabel swipeUpLabel;
        protected UIImageView topHalfSpinner;
        protected UIImageView bottomHalfSpinner;
        protected UIImageView circleMask;
        protected UIView spinnerView;
        protected UIView animationBlocker;
        public UITapGestureRecognizer tappedLabel;
        protected UILabel dismissLabel;
        protected const int SPINNER_WIDTH = 150;
        protected const int SPINNER_HEIGHT = 338;
        protected const int MASK_DIAMETER = 80;
        protected float LOWER_SECTION_Y_VAL;

        protected bool quitLoadingAnimation;

        public WaitingScreen ()
        {

        }

        public void SetOwner (AdvancedLoginViewController owner)
        {
            this.owner = owner;
        }

        public WaitingScreen (RectangleF frame)
        {
            this.Frame = frame;
        }

        public WaitingScreen (IntPtr handle) : base (handle)
        {

        }

        public void CreateView ()
        {
            this.BackgroundColor = A.Color_NachoGreen;
            LOWER_SECTION_Y_VAL = this.Frame.Height - 437 + 64;

            spinnerView = new UIView (new RectangleF (this.Frame.Width / 2 - 40, LOWER_SECTION_Y_VAL, MASK_DIAMETER, MASK_DIAMETER));
            spinnerView.BackgroundColor = A.Color_NachoRed;
            spinnerView.Layer.CornerRadius = MASK_DIAMETER / 2;
            spinnerView.Layer.MasksToBounds = true;
            this.Add (spinnerView);

            bottomHalfSpinner = new UIImageView (UIImage.FromBundle ("Spinner-1@2x"));
            bottomHalfSpinner.Frame = new RectangleF (-35, LOWER_SECTION_Y_VAL - 430, SPINNER_WIDTH, SPINNER_HEIGHT);
            spinnerView.Add (bottomHalfSpinner);

            topHalfSpinner = new UIImageView (UIImage.FromBundle ("Spinner-1@2x"));
            topHalfSpinner.Frame = new RectangleF (-35, LOWER_SECTION_Y_VAL - 530, SPINNER_WIDTH, SPINNER_HEIGHT);
            spinnerView.Add (topHalfSpinner);

            circleMask = new UIImageView (maskImage (UIImage.FromBundle ("Circular-Mask")));
            circleMask.Frame = new RectangleF (this.Frame.Width / 2, LOWER_SECTION_Y_VAL + MASK_DIAMETER / 2, .5f, .5f);
            circleMask.Layer.CornerRadius = MASK_DIAMETER / 2;
            circleMask.Layer.MasksToBounds = true;
            this.Add (circleMask);

            animationBlocker = new UIView (new RectangleF (this.Frame.Width / 2 - 40, LOWER_SECTION_Y_VAL, MASK_DIAMETER, MASK_DIAMETER));
            animationBlocker.Alpha = 1.0f;
            animationBlocker.BackgroundColor = A.Color_NachoGreen;
            this.Add (animationBlocker);

            syncStatusLabel = new UILabel (new RectangleF (0, LOWER_SECTION_Y_VAL + 89, this.Frame.Width, 30));
            syncStatusLabel.Font = A.Font_AvenirNextRegular17;
            syncStatusLabel.TextColor = UIColor.White;
            syncStatusLabel.TextAlignment = UITextAlignment.Center;
            syncStatusLabel.Text = "Locating Your Server...";
            syncStatusLabel.Alpha = 0.0f;
            this.Add (syncStatusLabel);

            startedCircleImage = new UIImageView (UIImage.FromBundle ("Loginscreen-BG@2x"));
            startedCircleImage.Frame = new RectangleF (this.Frame.Width / 2 - .5f, LOWER_SECTION_Y_VAL, 1, 1);
            startedCircleImage.Alpha = 0.0f;
            this.Add (startedCircleImage);

            finishedCircleImage = new UIImageView (UIImage.FromBundle ("Bootscreen-1@2x"));
            finishedCircleImage.Frame = new RectangleF (this.Frame.Width / 2 - .5f, LOWER_SECTION_Y_VAL, 1, 1);
            finishedCircleImage.Alpha = 0.0f;
            this.Add (finishedCircleImage);

            firstTrianglesImage = new UIImageView (UIImage.FromBundle ("Bootscreen-3@2x"));
            firstTrianglesImage.Frame = new RectangleF (0, LOWER_SECTION_Y_VAL + 309, this.Frame.Width, 39);
            this.Add (firstTrianglesImage);

            secondTriangleImage = new UIImageView (UIImage.FromBundle ("Bootscreen-2@2x"));
            secondTriangleImage.Frame = new RectangleF (40, LOWER_SECTION_Y_VAL + 309, this.Frame.Width - 80, 25);
            this.Add (secondTriangleImage);

            welcomeToLabel = new UILabel (new RectangleF (100, LOWER_SECTION_Y_VAL + 89, 120, 20));
            welcomeToLabel.Font = A.Font_AvenirNextRegular17;
            welcomeToLabel.TextColor = UIColor.White;
            welcomeToLabel.Text = "Welcome To";
            welcomeToLabel.Alpha = 0.0f;
            welcomeToLabel.TextAlignment = UITextAlignment.Center;
            this.Add (welcomeToLabel);

            nachoMailLabel = new UILabel (new RectangleF (70, LOWER_SECTION_Y_VAL + 110, 180, 40));
            nachoMailLabel.Font = A.Font_AvenirNextDemiBold30;
            nachoMailLabel.TextColor = UIColor.White;
            nachoMailLabel.Text = "NachoMail";
            nachoMailLabel.Alpha = 0.0f;
            nachoMailLabel.TextAlignment = UITextAlignment.Center;
            this.Add (nachoMailLabel);

            swipeUpTriangle = new UIImageView (UIImage.FromBundle ("Bootscreen-4@2x"));
            swipeUpTriangle.Frame = new RectangleF (this.Frame.Width / 2 - 11, LOWER_SECTION_Y_VAL + 289, 22, 9);
            swipeUpTriangle.Alpha = 0.0f;
            this.Add (swipeUpTriangle);

            swipeUpLabel = new UILabel (new RectangleF (70, LOWER_SECTION_Y_VAL + 255, 180, 20));
            swipeUpLabel.Font = A.Font_AvenirNextRegular14;
            swipeUpLabel.TextColor = A.Color_NachoYellow;
            swipeUpLabel.Text = "Swipe up to start setting up";
            swipeUpLabel.Alpha = 0.0f;
            swipeUpLabel.TextAlignment = UITextAlignment.Center;
            this.Add (swipeUpLabel);

            dismissLabel = new UILabel (new RectangleF (this.Frame.Width / 2 - 50, this.Frame.Bottom - 90 , 100, 15));
            dismissLabel.Font = A.Font_AvenirNextRegular12;
            dismissLabel.TextColor = UIColor.White;
            dismissLabel.Text = "Cancel";
            dismissLabel.TextAlignment = UITextAlignment.Center;
            dismissLabel.UserInteractionEnabled = true;
            UITapGestureRecognizer dismissLabelTap = new UITapGestureRecognizer (() => {
                DismissView ();
                owner.ConfigureView (AdvancedLoginViewController.LoginStatus.EnterInfo);
            });
            dismissLabel.AddGestureRecognizer (dismissLabelTap);
            this.Add (dismissLabel);

            this.Hidden = true;
        }

        protected UIImage maskImage (UIImage maskImage)
        {
            CGImage maskRef = maskImage.CGImage;
            CGImage imageMask = CGImage.CreateMask (maskRef.Width, maskRef.Height, maskRef.BitsPerComponent, maskRef.BitsPerPixel, maskRef.BytesPerRow, maskRef.DataProvider, null, true);
            return new UIImage (imageMask);
        }

        public void SetLoadingText (string loadingMessage)
        {
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

        public void ShowView ()
        {
            this.Hidden = false;
            owner.NavigationItem.Title = "";
            owner.NavigationController.NavigationBar.SetBackgroundImage (new UIImage (), UIBarMetrics.Default);
            owner.NavigationController.NavigationBar.BackgroundColor = A.Color_NachoGreen;
            owner.NavigationController.NavigationBar.ShadowImage = new UIImage ();
            quitLoadingAnimation = false;
            StartLoadingAnimation ();
        }

        public void DismissView ()
        {
            owner.stopBeIfRunning ();
            owner.NavigationItem.Title = "Account Setup";
            this.Hidden = true;
            this.Layer.RemoveAllAnimations ();
            quitLoadingAnimation = true;
            ResetLoadingItems ();
        }

        protected void ResetLoadingItems ()
        {
            syncStatusLabel.Alpha = 0.0f;
            circleMask.Transform = CGAffineTransform.MakeIdentity ();
            circleMask.Frame = new RectangleF (this.Frame.Width / 2, LOWER_SECTION_Y_VAL + MASK_DIAMETER / 2, .5f, .5f);
            bottomHalfSpinner.Frame = new RectangleF (-35, LOWER_SECTION_Y_VAL - 430, SPINNER_WIDTH, SPINNER_HEIGHT);
            topHalfSpinner.Frame = new RectangleF (-35, LOWER_SECTION_Y_VAL - 530, SPINNER_WIDTH, SPINNER_HEIGHT);
        }

        protected void StartLoadingAnimation ()
        {
            UIView.AnimateKeyframes (1, 0, (UIViewKeyframeAnimationOptions.OverrideInheritedDuration | UIViewKeyframeAnimationOptions.CalculationModeLinear), () => {

                UIView.AddKeyframeWithRelativeStartTime (0, .5, () => {
                    circleMask.Transform = CGAffineTransform.MakeScale (160, 160);
                    animationBlocker.Alpha = 0.0f;
                });

                UIView.AddKeyframeWithRelativeStartTime (.5, .5, () => {

                    syncStatusLabel.Alpha = 1.0f;
                });

            }, ((bool finished) => {
                ArrowAnimation (topHalfSpinner, bottomHalfSpinner, topHalfSpinner.Center, bottomHalfSpinner.Center, false);
            }));
        }

        private void ArrowAnimation (UIImageView theTopSpinner, UIImageView theBottomSpinner, PointF topSpinnerCenter, PointF bottomSpinnerCenter, bool bottomIsOnTop)
        {
            if (!quitLoadingAnimation) {

                UIView.AnimateKeyframes (3, 0, (UIViewKeyframeAnimationOptions.OverrideInheritedDuration | UIViewKeyframeAnimationOptions.CalculationModeLinear), () => {

                    UIView.AddKeyframeWithRelativeStartTime (0, 1, () => {
                        theTopSpinner.Center = new PointF (topSpinnerCenter.X, topSpinnerCenter.Y + 187.5f);
                        theBottomSpinner.Center = new PointF (bottomSpinnerCenter.X, bottomSpinnerCenter.Y + 187.5f);
                    });
                }, ((bool finished) => { 
                    if (finished) {
                        if (bottomIsOnTop) {
                            theTopSpinner.Center = bottomSpinnerCenter;
                            spinnerView.BringSubviewToFront (theTopSpinner);
                            bottomIsOnTop = false;
                        } else {
                            theBottomSpinner.Center = topSpinnerCenter;
                            spinnerView.BringSubviewToFront (theBottomSpinner);
                            bottomIsOnTop = true;
                        }
                        ArrowAnimation (theTopSpinner, theBottomSpinner, theTopSpinner.Center, theBottomSpinner.Center, bottomIsOnTop);
                    }
                }));
            }
        }

        public void StartSyncedEmailAnimation ()
        {
            UIView.AnimateKeyframes (4, .1, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {

                UIView.AddKeyframeWithRelativeStartTime (0, .075, () => {
                    dismissLabel.Alpha = 0.0f;
                    syncStatusLabel.Alpha = 0.0f;
                });

                UIView.AddKeyframeWithRelativeStartTime (.075, .075, () => {
                    circleMask.Transform = CGAffineTransform.MakeScale (.01f, .01f);
                    animationBlocker.Alpha = 1.0f;
                });

                UIView.AddKeyframeWithRelativeStartTime (.15, .075, () => {
                    startedCircleImage.Alpha = 1.0f;
                    startedCircleImage.Transform = CGAffineTransform.MakeScale (120, 120);
                    firstTrianglesImage.Frame = new RectangleF (firstTrianglesImage.Frame.X, firstTrianglesImage.Frame.Y - 39, firstTrianglesImage.Frame.Width, firstTrianglesImage.Frame.Height);
                });

                UIView.AddKeyframeWithRelativeStartTime (.225, .03, () => {
                    finishedCircleImage.Alpha = 1.0f;
                    finishedCircleImage.Transform = CGAffineTransform.MakeScale (120, 120);
                });

                UIView.AddKeyframeWithRelativeStartTime (.225, .075, () => {
                    secondTriangleImage.Frame = new RectangleF (secondTriangleImage.Frame.X, secondTriangleImage.Frame.Y - 25, secondTriangleImage.Frame.Width, secondTriangleImage.Frame.Height);
                });

                UIView.AddKeyframeWithRelativeStartTime (.3, .075, () => {
                    welcomeToLabel.Alpha = 1.0f;
                    nachoMailLabel.Alpha = 1.0f;
                });

                UIView.AddKeyframeWithRelativeStartTime (.375, .075, () => {
                    swipeUpTriangle.Frame = new RectangleF (swipeUpTriangle.Frame.X, swipeUpTriangle.Frame.Y - 10f, swipeUpTriangle.Frame.Width, swipeUpTriangle.Frame.Height);
                    swipeUpTriangle.Alpha = 1.0f;
                });

                UIView.AddKeyframeWithRelativeStartTime (.45, .075, () => {
                    swipeUpLabel.Alpha = 1.0f;
                    swipeUpLabel.Frame = new RectangleF (swipeUpLabel.Frame.X, swipeUpLabel.Frame.Y - 2f, swipeUpLabel.Frame.Width, swipeUpLabel.Frame.Height);
                });

                UIView.AddKeyframeWithRelativeStartTime (.525, .075, () => {
                    swipeUpTriangle.Transform = CGAffineTransform.MakeTranslation (0, 15);
                });

                UIView.AddKeyframeWithRelativeStartTime (.6, .075, () => {
                    swipeUpTriangle.Transform = CGAffineTransform.MakeTranslation (0, 2);
                });

                UIView.AddKeyframeWithRelativeStartTime (.675, .25, () => {
                });

            }, ((bool finished) => {

                owner.PerformSegue (StartupViewController.NextSegue (), this);

            }));
        }
    }
}
