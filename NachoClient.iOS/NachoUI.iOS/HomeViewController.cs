// This file has been autogenerated from a class added in the UI designer.

using System;
using Foundation;
using UIKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class HomeViewController : NcUIViewController
    {
        public UIPageViewController pageController;
        int accountId;
        public UIPageControl pageDots;
        public bool isFirstLoad = true;
        public UIButton closeTutorial;

        public HomeViewController (IntPtr handle) : base (handle)
        {
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

            // Help & demo pages
            InitializePageViewController ();

        }


        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
                this.NavigationController.NavigationBar.Hidden = true;

            }

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            accountId = LoginHelpers.GetCurrentAccountId ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
                this.NavigationController.NavigationBar.Hidden = false;
            }
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "Info_EmailMessageSetChanged Status Ind (Tutorial)");
                LoginHelpers.SetFirstSyncCompleted (accountId, true);
            }
            if (NcResult.SubKindEnum.Info_InboxPingStarted == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "Info_InboxPingStarted Status Ind (Tutorial)");
                LoginHelpers.SetFirstSyncCompleted (accountId, true);
            }
            if (NcResult.SubKindEnum.Info_AsAutoDComplete == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "Info_AsAutoDComplete Status Ind (Tutorial)");
                LoginHelpers.SetAutoDCompleted (LoginHelpers.GetCurrentAccountId (), true);
            }
        }


        public void InitializePageViewController ()
        {
            View.BackgroundColor = A.Color_NachoGreen;
            // Initialize the first page
            HomePageController firstPageController = new HomePageController (0);
            firstPageController.owner = this;   // set up our own "dots" for page indicator
            // remove the navigation bars
            // overlay the page controller and the dismiss button

            pageDots = new UIPageControl (); // page indicators; will get updates as datasource updates
            UIView dotsAndDismissContainerView = new UIView(); // contain pageDots and the dismiss button
            //setup 
            pageDots.Pages = this.TotalPages;
            pageDots.CurrentPage = 0;
            pageDots.BackgroundColor = UIColor.White;
            pageDots.PageIndicatorTintColor = UIColor.Gray;
            pageDots.CurrentPageIndicatorTintColor = UIColor.Black;

            //containerView.Frame = new System.Drawing.RectangleF (0, View.Frame.Bottom - 50, View.Frame.Width, 50);
            dotsAndDismissContainerView.Frame = new CoreGraphics.CGRect (0, this.View.Frame.Bottom-35  , this.View.Frame.Width, 35);
            dotsAndDismissContainerView.BackgroundColor = UIColor.White;
            pageDots.Frame = new CoreGraphics.CGRect(20,0, 62, 35);  // relative to containerView
            this.pageController = new UIPageViewController (UIPageViewControllerTransitionStyle.Scroll, 
                UIPageViewControllerNavigationOrientation.Horizontal, UIPageViewControllerSpineLocation.None);

            this.pageController.SetViewControllers (new UIViewController[] { firstPageController }, UIPageViewControllerNavigationDirection.Forward, 
                false, s => {
                });
            this.NavigationController.NavigationBarHidden = true;
            this.pageController.DataSource = new PageDataSource (this);


            this.pageController.View.Frame = this.View.Bounds;
            this.View.AddSubview (this.pageController.View);

            //Simulates a user dismissing tutorial, or the tutorial finishing on its own
            closeTutorial = new UIButton (new CoreGraphics.CGRect (View.Frame.Width-145, 0, 130, 35));
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
                LoginHelpers.SetHasViewedTutorial (accountId, true);
                PerformSegue(StartupViewController.NextSegue(), this);
            };
            dotsAndDismissContainerView.Add (pageDots);
            dotsAndDismissContainerView.Add (closeTutorial);

            Util.AddHorizontalLine (0, 0, dotsAndDismissContainerView.Frame.Width, A.Color_NachoBorderGray, dotsAndDismissContainerView);

            View.Add (dotsAndDismissContainerView);
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
                    return newPage;
                }

            }

            /* 
           public override int GetPresentationCount (UIPageViewController pageViewController)
            {
                // NOTE: Don't call the base implementation on a Model class
                // see http://docs.xamarin.com/guides/ios/application_fundamentals/delegates,_protocols,_and_events
                //throw new NotImplementedException ();
                return 4;
            }
            public override int GetPresentationIndex (UIPageViewController pageViewController)
            {
                // NOTE: Don't call the base implementation on a Model class
                // see http://docs.xamarin.com/guides/ios/application_fundamentals/delegates,_protocols,_and_events
                return 0;
            }
            */

        }
    }
}
