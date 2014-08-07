// This file has been autogenerated from a class added in the UI designer.

using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using SWRevealViewControllerBinding;

namespace NachoClient.iOS
{
    public partial class HomeViewController : NcUIViewController
    {
        UIPageViewController pageController;
        int accountId;
        UIPageControl pageDots;

        public HomeViewController (IntPtr handle) : base (handle)
        {
        }

        /// <summary>
        /// On first run, push the modal LaunchViewController to get credentials.
        /// </summary>
        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            /*
            // ideally we get rid of Navcontroller and have a dismiss button in toolbar
            // that said, for now, leave the NachoNow navigation controller so we can exit
            // the tutorial.
               
            UIBarButtonItem dismissButton = new UIBarButtonItem (UIBarButtonSystemItem.Done);
            dismissButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("HomeToNachoNow", this);
            };
            NavigationItem.RightBarButtonItem = dismissButton;

            this.NavigationController.NavigationBar.Translucent = true;
            this.NavigationController.NavigationBar.BackgroundColor = UIColor.Clear;
            */

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();

            // Multiple buttons on the left side
            NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { revealButton, nachoButton };
            using (var nachoImage = UIImage.FromBundle ("Nacho-Cove-Icon")) {
                nachoButton.Image = nachoImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal);
            }
            nachoButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("SegueToNachoNow", this);
            };

            // Help & demo pages
            InitializePageViewController ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
           if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            accountId = LoginHelpers.GetCurrentAccountId ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_FolderSyncSucceeded == s.Status.SubKind) {
                LoginHelpers.SetFirstSyncCompleted (accountId, true);
            }
        }


        public void InitializePageViewController ()
        {
            // Initialize the first page
            HomePageController firstPageController = new HomePageController (0);
            // set up our own "dots" for page indicator
            // remove the navigation bars
            // overlay the page controller and the dismiss button
           
            pageDots = new UIPageControl (); // page indicators; will get updates as datasource updates
            UIView containerView = new UIView(); // contain pageDots and the dismiss button


            //setup 
            pageDots.Pages = this.TotalPages;
            pageDots.CurrentPage = 0;
            pageDots.BackgroundColor = A.Color_NachoGreen;

            //containerView.Frame = new System.Drawing.RectangleF (0, View.Frame.Bottom - 50, View.Frame.Width, 50);
            containerView.Frame = new System.Drawing.RectangleF (0, this.View.Frame.Height-50  , this.View.Frame.Width, 50);
            containerView.BackgroundColor = A.Color_NachoGreen;
            pageDots.Frame = new System.Drawing.RectangleF(10,0, 50, 40);  // relative to containerView



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
            UIButton closeTutorial = new UIButton (new System.Drawing.RectangleF (View.Frame.Width-100, View.Frame.Top, 100, 50));
            closeTutorial.SetTitle ("Dismiss", UIControlState.Normal);
            closeTutorial.TitleLabel.TextColor = UIColor.White;
            closeTutorial.TitleLabel.Font = A.Font_AvenirNextRegular14;
            closeTutorial.BackgroundColor = A.Color_NachoGreen;
            //closeTutorial.BackgroundColor = A.Color_NachoRed; // debug
            closeTutorial.TouchUpInside += (object sender, EventArgs e) => {
                LoginHelpers.SetHasViewedTutorial (accountId, true);
                PerformSegue(StartupViewController.NextSegue(), this);
            };

            containerView.Add (pageDots);
            containerView.Add (closeTutorial);
            View.Add (containerView);
        }

        /// <summary>
        /// Gets the total pages in the "Book".
        /// </summary>
        /// <value>
        /// The total pages in the "Book".
        /// </value>
        public int TotalPages {
            get {
                return 4;
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
                    this.parentController.pageDots.CurrentPage = previousPageIndex;
                    return new HomePageController (previousPageIndex);
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
                    this.parentController.pageDots.CurrentPage = nextPageIndex;
                    return new HomePageController (nextPageIndex);
                }

            }

          /*  public override int GetPresentationCount (UIPageViewController pageViewController)
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
