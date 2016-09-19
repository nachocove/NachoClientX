// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using Foundation;
using UIKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.iOS
{

    public interface HomeViewControllerDelegate {
        void HomeViewControllerDidAppear (HomeViewController vc);
    }

    public partial class HomeViewController : NcUIViewController
    {

        public McAccount.AccountServiceEnum Service;
        public HomeViewControllerDelegate AccountDelegate; 
        public UIPageControl pageDots;
        public UIButton closeTutorial;

        UIPageViewController pageController;

        public HomeViewController (IntPtr handle) : base (handle)
        {
            NavigationItem.HidesBackButton = true;
        }

        public override bool ShouldAutorotate ()
        {
            return false;
        }

        /// <summary>
        /// On first run, push the modal LaunchViewController to get credentials.
        /// </summary>
        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            InitializePageViewController ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            if (AccountDelegate != null) {
                AccountDelegate.HomeViewControllerDidAppear (this);
            }
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
        }

        public void InitializePageViewController ()
        {
            UIView dotsAndDismissContainerView = new UIView (); // contain pageDots and the dismiss button
            dotsAndDismissContainerView.Frame = new CoreGraphics.CGRect (0, this.View.Bounds.Bottom - 35, this.View.Bounds.Width, 35);
            dotsAndDismissContainerView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin;
            dotsAndDismissContainerView.BackgroundColor = UIColor.White;

            pageDots = new UIPageControl (); // page indicators; will get updates as datasource updates
            pageDots.Pages = this.TotalPages;
            pageDots.CurrentPage = 0;
            pageDots.BackgroundColor = UIColor.White;
            pageDots.PageIndicatorTintColor = UIColor.Gray;
            pageDots.CurrentPageIndicatorTintColor = UIColor.Black;
            pageDots.Frame = new CoreGraphics.CGRect (20, 0, 62, 35);  // relative to dotsAndDismissContainterView
            dotsAndDismissContainerView.Add (pageDots);

            //Simulates a user dismissing tutorial, or the tutorial finishing on its own
            closeTutorial = new UIButton (new CoreGraphics.CGRect (View.Frame.Width - 145, 0, 130, 35));
            closeTutorial.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin;
            closeTutorial.TitleLabel.TextColor = UIColor.Black;
            closeTutorial.SetTitle ("Dismiss", UIControlState.Normal);
            closeTutorial.AccessibilityLabel = "Dismiss";
            closeTutorial.TitleLabel.TextColor = UIColor.Black;
            closeTutorial.TitleLabel.Font = A.Font_AvenirNextRegular14;
            closeTutorial.SetTitleColor (UIColor.Black, UIControlState.Normal);
            closeTutorial.BackgroundColor = UIColor.White;
            //closeTutorial.BackgroundColor = A.Color_NachoRed; // debug
            closeTutorial.HorizontalAlignment = UIControlContentHorizontalAlignment.Right;
            closeTutorial.TouchUpInside += (object sender, EventArgs e) => {
                LoginHelpers.SetHasViewedTutorial (true);
                NavigationController.PopViewController(true);
            };
            dotsAndDismissContainerView.Add (closeTutorial);

            var line = Util.AddHorizontalLine (0, 0, dotsAndDismissContainerView.Frame.Width, A.Color_NachoBorderGray, dotsAndDismissContainerView);
            line.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;

            View.Add (dotsAndDismissContainerView);

            // Initialize the first page
            HomePageController firstPageController = new HomePageController (0);
            firstPageController.owner = this;   // set up our own "dots" for page indicator

            this.pageController = new UIPageViewController (
                UIPageViewControllerTransitionStyle.Scroll, 
                UIPageViewControllerNavigationOrientation.Horizontal,
                UIPageViewControllerSpineLocation.None
            );

            this.pageController.SetViewControllers (new UIViewController[] { firstPageController }, UIPageViewControllerNavigationDirection.Forward, false,
                s => {
                }
            );
            this.pageController.DataSource = new PageDataSource (this);



            this.pageController.View.Frame = this.View.Bounds;
            this.pageController.View.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            this.View.AddSubview (this.pageController.View);

            // KLUDGE!
            View.BringSubviewToFront (dotsAndDismissContainerView);
        }

        protected nfloat GetPositionScale ()
        {
            return View.Frame.Height / 480f;
        }

        UIView tv;

        void CreateInitialView ()
        {

            CGRect frame = View.Frame;
            frame.Inflate (-A.Card_Horizontal_Indent, -64);

            tv = new UIView (frame);
            tv.BackgroundColor = UIColor.White;
            tv.Layer.CornerRadius = A.Card_Corner_Radius;
            tv.Layer.BorderColor = A.Card_Border_Color;
            tv.Layer.BorderWidth = A.Card_Border_Width;
            tv.ClipsToBounds = true;

            nfloat yOffset = 0;

            UIImageView nachoLogoImageView;
            using (var image = UIImage.FromBundle ("Bootscreen-1")) {
                nachoLogoImageView = new UIImageView (image);
            }
            nachoLogoImageView.Center = new CGPoint (tv.Frame.Width / 2, 130 * GetPositionScale () + nachoLogoImageView.Frame.Width / 2 - 100);
            tv.AddSubview (nachoLogoImageView);

            yOffset = nachoLogoImageView.Frame.Bottom + 40;

            var t1 = new UILabel ();
            t1.Lines = 0;
            t1.TextAlignment = UITextAlignment.Center;
            t1.Text = "Enjoy your first year of\nApollo Mail Service on us,";
            t1.Font = A.Font_AvenirNextDemiBold17;
            t1.TextColor = A.Color_11464F;
            t1.SizeToFit ();
            t1.Center = new CGPoint (tv.Frame.Width / 2, yOffset + (t1.Frame.Height / 2));
            tv.AddSubview (t1);

            yOffset = t1.Frame.Bottom;

            var t2 = new UILabel ();
            t2.Lines = 0;
            t2.TextAlignment = UITextAlignment.Center;
            t2.Text = "and after that it's only\n$1.99/year.";
            t2.Font = A.Font_AvenirNextRegular14;
            t2.TextColor = A.Color_11464F;
            t2.SizeToFit ();
            t2.Center = new CGPoint (tv.Frame.Width / 2, yOffset + (t2.Frame.Height / 2));
            tv.AddSubview (t2);

            yOffset = t2.Frame.Bottom + 30;

            var continueButton = new UIButton (UIButtonType.System);
            continueButton.Frame = new CGRect ((tv.Frame.Width - t1.Frame.Width) / 2, yOffset, t1.Frame.Width, 46);
            continueButton.BackgroundColor = A.Color_NachoSubmitButton;
            continueButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            continueButton.SetTitle ("Continue", UIControlState.Normal);
            continueButton.SetTitleColor (UIColor.White, UIControlState.Normal);
            continueButton.TitleLabel.Font = A.Font_AvenirNextDemiBold17;
            continueButton.Layer.CornerRadius = 4f;
            continueButton.Layer.MasksToBounds = true;
            continueButton.Alpha = 1.0f;
            continueButton.TouchUpInside += ContinueButtonTouchUpInside;
            continueButton.AccessibilityLabel = "Continue";
            continueButton.Enabled = true;
            tv.AddSubview (continueButton);

            UIImageView theNachos;
            using (var image = UIImage.FromBundle (Util.GetImage ("BG-S01"))) {
                theNachos = new UIImageView (image);
            }
            theNachos.Frame = new CGRect (0, tv.Frame.Height - theNachos.Frame.Height, tv.Frame.Width, theNachos.Frame.Height);
            tv.AddSubview (theNachos);

            View.AddSubview (tv);
            View.BringSubviewToFront (tv);

        }

        protected void ContinueButtonTouchUpInside (object sender, EventArgs e)
        {

            UIView.Animate (.2, 0, (UIViewAnimationOptions.CurveLinear | UIViewAnimationOptions.OverrideInheritedDuration), () => {
                tv.Alpha = 0;
            }, () => {
                InitializePageViewController ();
            });
        }

        public void NavigateForward (int i)
        {
            HomePageController forwardPageController = new HomePageController (1);
            forwardPageController.Service = Service;

            this.pageController.SetViewControllers (new UIViewController[] { forwardPageController }, UIPageViewControllerNavigationDirection.Forward, false,
                s => {
                }
            );
        }

        /// <summary>
        /// Gets the total pages in the "Book".
        /// </summary>
        /// <value>
        /// The total pages in the "Book".
        /// </value>
        public int TotalPages {
            get {
                return 5;
            }
        }

        private class PageDataSource : UIPageViewControllerDataSource
        {
            public PageDataSource (HomeViewController parentController)
            {
                this.parentController = parentController;
            }

            private HomeViewController parentController;

            public override UIViewController GetPreviousViewController (UIPageViewController pageViewController, UIViewController referenceViewController)
            {
                HomePageController currentPageController = referenceViewController as HomePageController;

                // Determine if we are on the first page
                if (currentPageController.PageIndex <= 0) {
                    return null;
                } else {
                    int previousPageIndex = currentPageController.PageIndex - 1;
                    var newPage = new HomePageController (previousPageIndex);
                    newPage.owner = this.parentController;
                    newPage.Service = parentController.Service;
                    return newPage;
                }
            }

            public override UIViewController GetNextViewController (UIPageViewController pageViewController, UIViewController referenceViewController)
            {
                HomePageController currentPageController = referenceViewController as HomePageController;

                // Determine if we are on the last page
                if (currentPageController.PageIndex >= (this.parentController.TotalPages - 1)) {
                    return null;
                } else {
                    int nextPageIndex = currentPageController.PageIndex + 1;
                    var newPage = new HomePageController (nextPageIndex);
                    newPage.owner = this.parentController;
                    newPage.Service = parentController.Service;
                    return newPage;
                }

            }

        }
    }
}
