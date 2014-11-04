// This file has been autogenerated from a class added in the UI designer.

using System;
using System.IO;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;
using MonoTouch.CoreGraphics;
using MonoTouch.CoreAnimation;

using NachoCore.Utils;

// Animation guide http://developer.xamarin.com/guides/cross-platform/application_fundamentals/touch/part_1_touch_in_ios/

namespace NachoClient.iOS
{
    public partial class HomePageController : NcUIViewController
    {
        public HomeViewController owner;

        // container for iPhone screen-in-screen
        // doing these as internal globals
        protected UIView pageContainerView;
        // full screen

        protected UIView contentContainer;
        // the phone-image content
        protected UIView helperContainer;
        // the helpful text container
        protected UILabel helperTitleText;
        // text Title
        protected UILabel helperBodyText;
        // text body
        protected UIView pageView;
        protected UIView contentView;

        //View One Components
        protected UIImageView redButton;
        protected UIImageView redToolTip;
        protected UIImageView greenButton;
        protected UIImageView greenToolTip;

        protected NSTimer toolTipTimer;

        //View Two Components
        protected UIImageView mailRedDot;
        protected UIImageView nachoCardOne;
        protected UIImageView nachoCardTwo;

        protected PointF mailRedDotCenter;
        protected PointF nachoCardOneCenter;
        protected PointF nachoCardTwoCenter;

        protected NSTimer flashOneTimer;
        protected NSTimer flashTwoTimer;
        protected NSTimer swipeMailUpTimer;
        protected NSTimer swipeMailDownTimer;

        //View Three Components
        protected UIImageView pullDownDotView;
        protected UIImageView meetingMessage;

        protected NSTimer flashPullDotTimer;

        //View Four Components
        protected UIImageView emailCellView;
        protected UIImageView swipeLeftImageView;
        protected UIImageView swipeRightImageView;
        protected UIImageView swipeDotView;

        protected PointF emailCellViewCenter;
        protected PointF swipeLeftCenter;
        protected PointF swipeRightCenter;
        protected PointF swipeDotCenter;

        protected NSTimer flashDotsFirstTimer;
        protected NSTimer swipeLeftTimer;
        protected NSTimer revertLeftToCenterTimer;
        protected NSTimer flashDotsSecondTimer;
        protected NSTimer swipeRightTimer;
        protected NSTimer revertRightToCenterTimer;
        protected NSTimer moveDotsTimer;

        public HomePageController (int pageIndex) : base ("HomePageController", null)
        {
            this.PageIndex = pageIndex;
            owner = null;
        }

        public int PageIndex {
            get;
            private set;
        }

        // Helper Text Strings
        const string TitleOne = "Your Email";
        const string TitleTwo = "Navigating Your Hot List";
        const string TitleThree = "Your Meetings and Events";
        const string TitleFour = "Just One Last Thing ...";

        const string BodyOne = "Important messages go to your Hot List" + "\n" + "All messages can be found in Mail.";
        const string BodyTwo = "Quickly browse through your Hot List" + "\n" + "by swiping up and down.";
        const string BodyThree = "Your next meeting is here. Manage" + "\n" + "your schedule using Calendar.";
        const string BodyFour = "Sliding right or left elsewhere will get you" + "\n" + "shortcusts and options for the items";

        const string contentscreen = "Slide1-3.png";
        // phone-face image
        const string calendarpull = "Slide1-2.png";
        // calendar pull down
        const string msg1loc = "Slide1-1A.png";
        // Meagan message
        const string msg2loc = "Slide1-1B.png";
        // next message
        const string inboxloc = "Slide1-4.png";
        // inbox msg at bottom of screen

        string[] titleText = {
            TitleOne,
            TitleTwo,
            TitleThree,
            TitleFour,
        };
        string[] bodyText = {
            BodyOne,
            BodyTwo,
            BodyThree,
            BodyFour,
        };

        public override void ViewDidLoad ()
        {
            this.pageContainerView = new UIView (new RectangleF (0, 0, this.owner.View.Bounds.Width, this.owner.View.Bounds.Height - 48));
            pageContainerView.BackgroundColor = A.Color_NachoGreen;

            if (UIScreen.MainScreen.Bounds.Height == 480) {

                this.contentContainer = new UIView (new RectangleF (54, 60, 212, 306)); // see size of helpercontainer
                this.helperContainer = new UIView (new RectangleF (0, this.contentContainer.Frame.Bottom, pageContainerView.Frame.Width, pageContainerView.Frame.Bottom - this.contentContainer.Frame.Bottom));// contains the helpertext and labels  
                this.helperTitleText = new UILabel (new RectangleF (0, 12, helperContainer.Frame.Width, 20));
                this.helperBodyText = new UILabel (new RectangleF ((helperContainer.Frame.Width - 284) / 2, helperTitleText.Frame.Bottom, 284, 40));

            } else {
                this.contentContainer = new UIView (new RectangleF (54, 85, 212, 306)); // see size of helpercontainer
                this.helperContainer = new UIView (new RectangleF (0, this.contentContainer.Frame.Bottom, pageContainerView.Frame.Width, 130));// contains the helpertext and labels  
                this.helperTitleText = new UILabel (new RectangleF (0, 24, helperContainer.Frame.Width, 20));
                this.helperBodyText = new UILabel (new RectangleF ((helperContainer.Frame.Width - 284) / 2, helperTitleText.Frame.Bottom + 10, 284, 40));
            }

            pageView = new UIView (pageContainerView.Frame);
            contentView = new UIView (contentContainer.Frame);

            base.ViewDidLoad ();

            string leftNachos = "";
            string rightNachos = "";

            switch (this.PageIndex) {
            case 0:
                leftNachos = "BG-S01Left@2x";
                rightNachos = "BG-S01Right@2x";
                CreateViewOne ();
                break;
            case 1:
                leftNachos = "BG-S02Left@2x";
                rightNachos = "BG-S02Right@2x";
                CreateViewTwo ();
                break;
            case 2:
                leftNachos = "BG-S03Left@2x";
                rightNachos = "BG-S03Right@2x";
                CreateViewThree ();
                break;
            case 3:
                leftNachos = "BG-S04Left@2x";
                rightNachos = "BG-S04Right@2x";
                CreateViewFour ();
                break;
            }

            this.View.AddSubview (pageContainerView);
            this.View.AddSubview (pageView);
            CreateNachos (leftNachos, rightNachos);
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);

            switch (this.PageIndex) {

            case 0:
                ResetViewOne ();
                break;
            case 1:
                ResetViewTwo ();
                break;
            case 2:
                ResetViewThree ();
                break;
            case 3:
                ResetViewFour ();
                break;
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            if (this.owner == null) {
                NcAssert.True (false, "Tutorial Page has no owner");
            } else {
                this.owner.pageDots.CurrentPage = this.PageIndex; // update containerView.PageDots
            }
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);

            switch (this.PageIndex) {
            case 0:
                ResetViewOne ();
                AnimateViewOne ();
                break;
            case 1:
                ResetViewTwo ();
                AnimateViewTwo ();
                break;
            case 2:
                ResetViewThree ();
                AnimateViewThree ();
                break;
            case 3:
                ResetViewFour ();
                AnimateViewFour ();
                break;
            }
        }

        private void CreateNachos (string leftNachos, string rightNachos)
        {
            UIImageView leftSideNachos = new UIImageView (UIImage.FromBundle (leftNachos));
            leftSideNachos.Frame = new RectangleF (0, this.contentContainer.Frame.Bottom - leftSideNachos.Frame.Height / 2, this.contentContainer.Frame.X, leftSideNachos.Frame.Height / 2);
            this.View.AddSubview (leftSideNachos);

            UIImageView rightSideNachos = new UIImageView (UIImage.FromBundle (rightNachos));
            rightSideNachos.Frame = new RectangleF (this.contentContainer.Frame.X + this.contentContainer.Frame.Width, this.contentContainer.Frame.Bottom - rightSideNachos.Frame.Height / 2, this.contentContainer.Frame.X, rightSideNachos.Frame.Height / 2);
            this.View.AddSubview (rightSideNachos);
        }

        void CreateHelperText ()
        {
            helperContainer.BackgroundColor = UIColor.White;

            helperTitleText.BackgroundColor = UIColor.White; // debug
            helperTitleText.TextColor = A.Color_11464F;
            helperTitleText.Text = titleText [this.PageIndex];
            helperTitleText.Font = A.Font_AvenirNextDemiBold17;
            helperTitleText.TextAlignment = UITextAlignment.Center;
            helperContainer.Add (helperTitleText);

            helperBodyText.BackgroundColor = UIColor.White; // debug
            helperBodyText.TextColor = A.Color_9B9B9B;
            helperBodyText.Lines = 2;
            helperBodyText.Text = bodyText [this.PageIndex];
            helperBodyText.Font = A.Font_AvenirNextRegular14;
            helperBodyText.TextAlignment = UITextAlignment.Center;
            helperContainer.Add (helperBodyText);

            Util.AddHorizontalLine (0, helperContainer.Frame.Height + 10, helperContainer.Frame.Width, A.Color_NachoBorderGray, helperContainer);
        }

        protected void CreateViewOne ()
        {
            UIImageView nachoNowImageView = new UIImageView ();
            using (var image = UIImage.FromBundle ("Slide02BG")) {
                nachoNowImageView.Image = image;
            }
            nachoNowImageView.Frame = (new RectangleF (0, 0, contentContainer.Frame.Width, contentContainer.Frame.Height));
            contentContainer.AddSubview (nachoNowImageView);

            using (var image = UIImage.FromBundle ("red_pointer")) {
                redButton = new UIImageView (image);
            }
            redButton.Center = new PointF (this.contentContainer.Frame.Width / 2, this.contentContainer.Frame.Height / 4);
            redButton.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            contentContainer.AddSubview (redButton);

            using (var image = UIImage.FromBundle ("Slide02MessageUp")) {
                redToolTip = new UIImageView (image);
            }
            redToolTip.Center = new PointF (this.contentContainer.Frame.Width / 2, this.contentContainer.Frame.Height / 4 + 35);
            redToolTip.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            contentContainer.AddSubview (redToolTip);

            using (var image = UIImage.FromBundle ("teal_pointer")) {
                greenButton = new UIImageView (image);
            }
            greenButton.Center = new PointF (this.contentContainer.Frame.Width / 2, this.contentContainer.Frame.Height - 25);
            greenButton.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            contentContainer.AddSubview (greenButton);

            using (var image = UIImage.FromBundle ("Slide02MessageDown")) {
                greenToolTip = new UIImageView (image);
            }
            greenToolTip.Center = new PointF (this.contentContainer.Frame.Width / 2, this.contentContainer.Frame.Height - 60);
            greenToolTip.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            contentContainer.AddSubview (greenToolTip);

            CreateHelperText ();
            contentContainer.ClipsToBounds = true;
            pageContainerView.AddSubview (contentContainer);
            pageContainerView.AddSubview (helperContainer);
        }

        protected void CreateViewTwo ()
        {
            UIImageView nachoGrayBackground = new UIImageView ();
            using (var image = UIImage.FromBundle ("Slide03BG")) {
                nachoGrayBackground.Image = image;
            }
            nachoGrayBackground.Frame = (new RectangleF (0, 0, contentContainer.Frame.Width, contentContainer.Frame.Height));
            contentContainer.AddSubview (nachoGrayBackground);

            UIImageView nachoNowBackground = new UIImageView ();
            using (var image = UIImage.FromBundle ("Slide03MidBG")) {
                nachoNowBackground.Image = image;
            }
            nachoNowBackground.Frame = (new RectangleF (0, 0, contentContainer.Frame.Width, contentContainer.Frame.Height));
            contentContainer.AddSubview (nachoNowBackground);

            UIView cardHolderView = new UIView (new RectangleF (8, 56, contentContainer.Frame.Width - 15, contentContainer.Frame.Height - 75));
            cardHolderView.Layer.CornerRadius = 4f;
            cardHolderView.ClipsToBounds = true;
            contentContainer.AddSubview (cardHolderView);

            nachoCardOne = new UIImageView ();
            using (var image = UIImage.FromBundle ("Slide03Card01")) {
                nachoCardOne.Image = image;
            }
            nachoCardOne.Frame = (new RectangleF (0, 0, cardHolderView.Frame.Width, cardHolderView.Frame.Height));
            nachoCardOneCenter = nachoCardOne.Center;
            cardHolderView.AddSubview (nachoCardOne);

            nachoCardTwo = new UIImageView ();
            using (var image = UIImage.FromBundle ("Slide03Card02")) {
                nachoCardTwo.Image = image;
            }
            nachoCardTwo.Frame = (new RectangleF (0, nachoCardOne.Frame.Bottom + 10, cardHolderView.Frame.Width, cardHolderView.Frame.Height));
            nachoCardTwoCenter = nachoCardTwo.Center;
            cardHolderView.AddSubview (nachoCardTwo);

            mailRedDot = new UIImageView (UIImage.FromBundle ("red_pointer.png"));
            mailRedDot.Center = new PointF (nachoCardOne.Center.X, nachoCardOne.Center.Y + 30);
            mailRedDotCenter = mailRedDot.Center;
            cardHolderView.AddSubview (mailRedDot);

            CreateHelperText ();
            contentView.ClipsToBounds = true;
            pageView.AddSubview (contentContainer);
            pageView.AddSubview (helperContainer);
        }

        protected void CreateViewThree ()
        {
            UIImageView calendarBackground = new UIImageView ();
            using (var image = UIImage.FromBundle ("Slide04BG")) {
                calendarBackground.Image = image;
            }
            calendarBackground.Frame = (new RectangleF (0, 0, contentContainer.Frame.Width, contentContainer.Frame.Height));
            contentContainer.AddSubview (calendarBackground);

            meetingMessage = new UIImageView ();
            using (var image = UIImage.FromBundle ("Slide04MessageUp")) {
                meetingMessage.Image = image;
            }
            meetingMessage.Frame = (new RectangleF (contentContainer.Frame.Width / 2 - 60, 43, 120, 34));
            meetingMessage.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            contentContainer.AddSubview (meetingMessage);

            using (var image = UIImage.FromBundle ("red_pointer.png")) {
                pullDownDotView = new UIImageView (image);
            }
            pullDownDotView.Center = new PointF (meetingMessage.Center.X, 25);
            pullDownDotView.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            contentContainer.AddSubview (pullDownDotView);

            CreateHelperText ();
            contentContainer.ClipsToBounds = true;
            pageContainerView.AddSubview (contentContainer);
            pageContainerView.AddSubview (helperContainer);
        }

        private void CreateViewFour ()
        {
            UIImageView inboxBackgroundImageView = new UIImageView ();
            using (var image = UIImage.FromBundle ("Slide05BG.png")) {
                inboxBackgroundImageView.Image = image;
            }
            inboxBackgroundImageView.Frame = (new RectangleF (0, 0, contentContainer.Frame.Width, contentContainer.Frame.Height));
            contentContainer.AddSubview (inboxBackgroundImageView);

            swipeRightImageView = new UIImageView (UIImage.FromBundle ("Slide05SwipeRight.png"));
            using (var image = UIImage.FromBundle ("Slide05SwipeRight.png")) {
                swipeRightImageView = new UIImageView (image);
            }
            swipeRightImageView.Frame = new RectangleF (-163, 0, 163, 123);
            swipeRightCenter = swipeRightImageView.Center;
            contentContainer.AddSubview (swipeRightImageView);

            using (var image = UIImage.FromBundle ("Slide05Email.png")) {
                emailCellView = new UIImageView (image);
            }
            emailCellView.Frame = new RectangleF (0, 0, this.contentContainer.Frame.Width, 123);
            emailCellViewCenter = emailCellView.Center;
            contentContainer.AddSubview (emailCellView);

            using (var image = UIImage.FromBundle ("Slide05SwipeLeft.png")) {
                swipeLeftImageView = new UIImageView (image);
            }
            swipeLeftImageView.Frame = new RectangleF (this.contentContainer.Frame.Width, 0, 163, 123);
            swipeLeftCenter = swipeLeftImageView.Center;
            contentContainer.AddSubview (swipeLeftImageView);

            swipeDotView = new UIImageView (UIImage.FromBundle ("red_pointer.png"));
            swipeDotView.Center = new PointF (emailCellView.Center.X + 30, emailCellView.Center.Y);
            swipeDotCenter = swipeDotView.Center;
            contentContainer.AddSubview (swipeDotView);

            CreateHelperText ();
            contentContainer.ClipsToBounds = true;
            pageContainerView.AddSubview (contentContainer);
            pageContainerView.AddSubview (helperContainer);
        }

        protected void AnimateViewOne ()
        {
            toolTipTimer = NSTimer.CreateScheduledTimer (1, delegate {
                UIView.AnimateKeyframes (1, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                    UIView.AddKeyframeWithRelativeStartTime (0, 1, () => {
                        redButton.Layer.Transform = CATransform3D.MakeScale (1.0f, 1.0f, 1.0f);
                        redToolTip.Layer.Transform = CATransform3D.MakeScale (1.0f, 1.0f, 1.0f);
                        greenButton.Layer.Transform = CATransform3D.MakeScale (1.0f, 1.0f, 1.0f);
                        greenToolTip.Layer.Transform = CATransform3D.MakeScale (1.0f, 1.0f, 1.0f);
                    });
                }, ((bool finished) => {
                }));
            });
        }

        protected void AnimateViewTwo ()
        {
            flashOneTimer = NSTimer.CreateScheduledTimer (.5, delegate {
                UIView.AnimateKeyframes (2, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                    UIView.AddKeyframeWithRelativeStartTime (0, .5, () => {
                        mailRedDot.Alpha = 0.5f;
                    });

                    UIView.AddKeyframeWithRelativeStartTime (.5, .5, () => {
                        mailRedDot.Alpha = 1.0f;
                    });
                }, ((bool finished) => {
                }));
            });

            swipeMailUpTimer = NSTimer.CreateScheduledTimer (2.5, delegate {
                UIView.AnimateKeyframes (1, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                    UIView.AddKeyframeWithRelativeStartTime (0, 1, () => {
                        mailRedDot.Center = new PointF (mailRedDot.Center.X, mailRedDot.Center.Y - 242);
                        nachoCardOne.Center = new PointF (nachoCardOne.Center.X, nachoCardOne.Center.Y - 242);
                        nachoCardTwo.Center = new PointF (nachoCardTwo.Center.X, nachoCardTwo.Center.Y - 242);
                    });
                }, ((bool finished) => {
                }));
            });

            flashTwoTimer = NSTimer.CreateScheduledTimer (3.6, delegate {
                mailRedDot.Center = new PointF (mailRedDotCenter.X, mailRedDotCenter.Y - 80);
                UIView.AnimateKeyframes (2.1, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                    UIView.AddKeyframeWithRelativeStartTime (0, .5, () => {
                        mailRedDot.Alpha = .5f;
                    });
                    UIView.AddKeyframeWithRelativeStartTime (.5, .5, () => {
                        mailRedDot.Alpha = 1.0f;
                    });
                }, ((bool finished) => {
                }));
            });

            swipeMailDownTimer = NSTimer.CreateScheduledTimer (5.7, delegate {
                UIView.AnimateKeyframes (1, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                    UIView.AddKeyframeWithRelativeStartTime (0, 1, () => {
                        mailRedDot.Center = new PointF (mailRedDot.Center.X, mailRedDot.Center.Y + 242);
                        nachoCardOne.Center = new PointF (nachoCardOne.Center.X, nachoCardOne.Center.Y + 242);
                        nachoCardTwo.Center = new PointF (nachoCardTwo.Center.X, nachoCardTwo.Center.Y + 242);
                    });
                }, ((bool finished) => {
                }));
            });
        }

        public void AnimateViewThree ()
        {
            flashPullDotTimer = NSTimer.CreateScheduledTimer (1, delegate {
                UIView.AnimateKeyframes (1, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                    UIView.AddKeyframeWithRelativeStartTime (0, 1, () => {
                        pullDownDotView.Layer.Transform = CATransform3D.MakeScale (1.0f, 1.0f, 1.0f);
                        meetingMessage.Layer.Transform = CATransform3D.MakeScale (1.0f, 1.0f, 1.0f);
                    });
                }, ((bool finished) => {
                }));
            });
        }

        public void AnimateViewFour ()
        {

            flashDotsFirstTimer = NSTimer.CreateScheduledTimer (.5, delegate {
                UIView.AnimateKeyframes (1.5, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                    UIView.AddKeyframeWithRelativeStartTime (0, .5, () => {
                        swipeDotView.Alpha = 0.4f;
                    });

                    UIView.AddKeyframeWithRelativeStartTime (.5, .5, () => {
                        swipeDotView.Alpha = 1.0f;
                    });
                }, ((bool finished) => {
                }));
            });

            swipeLeftTimer = NSTimer.CreateScheduledTimer (3, delegate {
                UIView.AnimateKeyframes (.7, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                    UIView.AddKeyframeWithRelativeStartTime (0, 1, () => {
                        swipeDotView.Center = new PointF (swipeDotView.Center.X - swipeLeftImageView.Frame.Width, swipeDotView.Center.Y);
                        swipeLeftImageView.Center = new PointF (swipeLeftImageView.Center.X - swipeLeftImageView.Frame.Width, swipeLeftImageView.Center.Y);
                        emailCellView.Center = new PointF (emailCellView.Center.X - swipeLeftImageView.Frame.Width, emailCellView.Center.Y);
                        swipeDotView.Alpha = 0.0f;
                    });

                }, ((bool finished) => {
                }));
            });

            revertLeftToCenterTimer = NSTimer.CreateScheduledTimer (4.5, delegate {
                UIView.AnimateKeyframes (.7, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                    UIView.AddKeyframeWithRelativeStartTime (0, 1, () => {
                        swipeDotView.Center = new PointF (swipeDotView.Center.X + swipeLeftImageView.Frame.Width, swipeDotView.Center.Y);
                        swipeLeftImageView.Center = new PointF (swipeLeftImageView.Center.X + swipeLeftImageView.Frame.Width, swipeLeftImageView.Center.Y);
                        emailCellView.Center = new PointF (emailCellView.Center.X + swipeLeftImageView.Frame.Width, emailCellView.Center.Y);
                    });

                }, ((bool finished) => {

                }));
            });

            moveDotsTimer = NSTimer.CreateScheduledTimer (5.2, delegate {
                UIView.AnimateKeyframes (.2, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                    UIView.AddKeyframeWithRelativeStartTime (0, 1, () => {
                        swipeDotView.Center = new PointF (swipeDotCenter.X - 70, swipeDotCenter.Y);
                    });

                }, ((bool finished) => {

                }));
            });


            flashDotsSecondTimer = NSTimer.CreateScheduledTimer (5.7, delegate {
                UIView.AnimateKeyframes (1.7, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {

                    UIView.AddKeyframeWithRelativeStartTime (0, .5, () => {
                        swipeDotView.Alpha = 0.4f;
                    });

                    UIView.AddKeyframeWithRelativeStartTime (.5, .5, () => {
                        swipeDotView.Alpha = 1.0f;
                    });
                }, ((bool finished) => {
                }));
            });

            swipeRightTimer = NSTimer.CreateScheduledTimer (7.6, delegate {
                UIView.AnimateKeyframes (.8, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                    UIView.AddKeyframeWithRelativeStartTime (0, 1, () => {
                        swipeDotView.Center = new PointF (swipeDotView.Center.X + swipeLeftImageView.Frame.Width, swipeDotView.Center.Y);
                        emailCellView.Center = new PointF (emailCellView.Center.X + swipeLeftImageView.Frame.Width, emailCellView.Center.Y);
                        swipeRightImageView.Center = new PointF (swipeRightImageView.Center.X + swipeLeftImageView.Frame.Width, swipeRightImageView.Center.Y);
                    });

                }, ((bool finished) => {
                }));
            });

            revertRightToCenterTimer = NSTimer.CreateScheduledTimer (9.1, delegate {
                UIView.AnimateKeyframes (.8, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                    UIView.AddKeyframeWithRelativeStartTime (0, 1, () => {
                        emailCellView.Center = new PointF (emailCellView.Center.X - swipeLeftImageView.Frame.Width, emailCellView.Center.Y);
                        swipeRightImageView.Center = new PointF (swipeRightImageView.Center.X - swipeLeftImageView.Frame.Width, swipeRightImageView.Center.Y);
                    });
                }, ((bool finished) => {
                }));
            });
        }

        protected void ResetViewOne ()
        {
            //Disable any active timers
            if (null != toolTipTimer) {
                toolTipTimer.Invalidate ();
            }

            //Cancel any live animations
            redButton.Layer.RemoveAllAnimations ();
            redToolTip.Layer.RemoveAllAnimations ();
            greenButton.Layer.RemoveAllAnimations ();
            greenToolTip.Layer.RemoveAllAnimations ();

            //Place items back to original position
            redButton.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            redToolTip.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            greenButton.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            greenToolTip.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
        }

        protected void ResetViewTwo ()
        {
            //Disable any active timers
            if (null != flashOneTimer) {
                flashOneTimer.Invalidate ();
            }
            if (null != swipeMailUpTimer) {
                swipeMailUpTimer.Invalidate ();
            }
            if (null != flashTwoTimer) {
                flashTwoTimer.Invalidate ();
            }
            if (null != swipeMailDownTimer) {
                swipeMailDownTimer.Invalidate ();
            }

            //Cancel any live animations
            nachoCardOne.Layer.RemoveAllAnimations ();
            nachoCardTwo.Layer.RemoveAllAnimations ();
            mailRedDot.Layer.RemoveAllAnimations ();

            //Move items back to original position
            nachoCardOne.Center = nachoCardOneCenter;
            nachoCardTwo.Center = nachoCardTwoCenter;
            mailRedDot.Center = mailRedDotCenter;
        }

        protected void ResetViewThree ()
        {
            if (null != flashPullDotTimer) {
                flashPullDotTimer.Invalidate ();
            }

            pullDownDotView.Layer.RemoveAllAnimations ();
            meetingMessage.Layer.RemoveAllAnimations ();

            pullDownDotView.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
            meetingMessage.Layer.Transform = CATransform3D.MakeScale (0.0f, 0.0f, 1.0f);
        }

        protected void ResetViewFour ()
        {
            //Disable any active timers
            if (null != flashDotsFirstTimer) {
                flashDotsFirstTimer.Invalidate ();
            }
            if (null != swipeLeftTimer) {
                swipeLeftTimer.Invalidate ();
            }
            if (null != revertLeftToCenterTimer) {
                revertLeftToCenterTimer.Invalidate ();
            }

            if (null != moveDotsTimer) {
                moveDotsTimer.Invalidate ();
            }

            if (null != flashDotsSecondTimer) {
                flashDotsSecondTimer.Invalidate ();
            }
            if (null != swipeRightTimer) {
                swipeRightTimer.Invalidate ();

            }
            if (null != revertRightToCenterTimer) {
                revertRightToCenterTimer.Invalidate ();
            } 

            //Cancel any live animations
            swipeLeftImageView.Layer.RemoveAllAnimations ();
            swipeRightImageView.Layer.RemoveAllAnimations ();
            emailCellView.Layer.RemoveAllAnimations ();
            swipeDotView.Layer.RemoveAllAnimations ();

            //Move items back to original position
            swipeLeftImageView.Center = swipeLeftCenter;
            swipeRightImageView.Center = swipeRightCenter;
            emailCellView.Center = emailCellViewCenter;
            swipeDotView.Center = swipeDotCenter;
        }
    }
}
