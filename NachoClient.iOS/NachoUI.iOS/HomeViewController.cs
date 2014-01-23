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
    public partial class HomeViewController : UIViewController
    {
        AppDelegate appDelegate { get; set; }
        UIPageViewController pageController;

        public HomeViewController (IntPtr handle) : base (handle)
        {
            Log.Info (Log.LOG_UI, "HomeViewController initialized");
            appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
        }

        /// <summary>
        /// On first run, push the modal LaunchViewController to get credentials.
        /// </summary>
        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();
            this.View.AddGestureRecognizer (this.RevealViewController ().PanGestureRecognizer);

            // Help & demo pages
            InitializePageViewController ();

            // Initial view
            if (0 == BackEnd.Instance.Db.Table<McAccount> ().Count ()) {
                PerformSegue ("HomeToLaunch", this); // modal
            } else {
                PerformSegue ("HomeToFolders", this); // push
            }
        }

        public void InitializePageViewController()
        {
            // Initialize the first page
            HomePageController firstPageController = new HomePageController(0);

            this.pageController = new UIPageViewController(UIPageViewControllerTransitionStyle.PageCurl, 
                UIPageViewControllerNavigationOrientation.Horizontal, UIPageViewControllerSpineLocation.Min);

            this.pageController.SetViewControllers(new UIViewController[] { firstPageController }, UIPageViewControllerNavigationDirection.Forward, 
                false, s => { });

            this.pageController.DataSource = new PageDataSource(this);

            this.pageController.View.Frame = this.View.Bounds;
            this.View.AddSubview(this.pageController.View);
        }

        /// <summary>
        /// Gets the total pages in the "Book".
        /// </summary>
        /// <value>
        /// The total pages in the "Book".
        /// </value>
        public int TotalPages
        {
            get {
                return 3;
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
                    return new HomePageController (nextPageIndex);
                }

            }
        }
    }
}
