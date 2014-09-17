// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace NachoClient.iOS
{
    public partial class NachoTabBarController : UITabBarController
    {
        public NachoTabBarController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            CreateTabBar ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }

        private void CreateTabBar ()
        {

            TabBar.BarTintColor = UIColor.White;
            TabBar.TintColor = A.Color_NachoIconGray;
            TabBar.Translucent = false;

            MoreNavigationController.NavigationBar.TintColor = A.Color_NachoBlue;
            MoreNavigationController.NavigationBar.Translucent = false;
        }
    }
}
