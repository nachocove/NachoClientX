// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;

using Foundation;
using UIKit;

using NachoCore.Model;
using NachoCore.Utils;
using CoreGraphics;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class NachoTabBarController : UITabBarController
    {
        protected static string TabBarOrderKey = "TabBarOrder";

        // UI elements needed to customize the "More" tab.
        protected UITableView existingTableView;
        protected static NachoTabBarController instance;

        SwitchAccountButton switchAccountButton;

        public NachoTabBarController (IntPtr handle) : base (handle)
        {
        }

        protected UITabBarItem nachoNowItem;
        protected UITabBarItem settingsItem;
        protected UITabBarItem foldersItem;
        protected UITabBarItem deadlinesItem;
        protected UITabBarItem deferredItem;
        protected UITabBarItem inboxItem;

        protected const int TABLEVIEW_TAG = 1999;

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            instance = this;

            ViewControllerSelected += ViewControllerSelectedHandler;
            ShouldSelectViewController += ViewControllerShouldSelectHandler;

            TabBar.BarTintColor = UIColor.White;
            TabBar.TintColor = A.Color_NachoIconGray;
            TabBar.SelectedImageTintColor = A.Color_NachoGreen;
            TabBar.Translucent = false;

            MoreNavigationController.NavigationBar.TintColor = A.Color_NachoBlue;
            MoreNavigationController.NavigationBar.Translucent = false;

            switchAccountButton = new SwitchAccountButton (SwitchAccountButtonPressed);

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            RestoreCustomTabBarOrder ();

            nachoNowItem = SetTabBarItem ("NachoClient.iOS.NachoNowViewController", "Hot", "nav-nachonow", "nav-nachonow-active"); // Done
            SetTabBarItem ("NachoClient.iOS.CalendarViewController", "Calendar", "nav-calendar", "nav-calendar-active"); // Done
            SetTabBarItem ("NachoClient.iOS.ContactListViewController", "Contacts", "nav-contacts", "nav-contacts-active"); // Done
            inboxItem = SetTabBarItem ("NachoClient.iOS.InboxViewController", "Inbox", "nav-mail", "nav-mail-active"); // Done
            settingsItem = SetTabBarItem ("NachoClient.iOS.GeneralSettingsViewController", "Settings", "more-settings", "more-settings-active"); // Done
            SetTabBarItem ("NachoClient.iOS.SupportViewController", "Support", "more-support", "more-support-active"); // Done
            // SetTabBarItem ("NachoClient.iOS.HotListViewController", "Hot List", "nav-mail", "nav-mail-active"); // Done
            deferredItem = SetTabBarItem ("NachoClient.iOS.DeferredViewController", "Deferred", "nav-mail", "nav-mail-active"); // Done
            deadlinesItem = SetTabBarItem ("NachoClient.iOS.DeadlinesViewController", "Deadlines", "nav-mail", "nav-mail-active"); // Done
            foldersItem = SetTabBarItem ("NachoClient.iOS.FoldersViewController", "Mail", "nav-mail", "nav-mail-active"); // Done
            SetTabBarItem ("NachoClient.iOS.FileListViewController", "Files", "more-files", "more-files-active"); // Done
            SetTabBarItem ("NachoClient.iOS.AboutUsViewController", "About Nacho Mail", "more-nachomail", "more-nachomail-active"); // Done


            // This code is for testing purposes only.  It must never be compiled as part of a product build.
            // Change "#if false" to "#if true" when you want to run this code in the simulator, then discard
            // the change when you are done testing.  The committed version of the file must always have
            // "#if false".
            #if false
            var existingControllers = new List<UIViewController> (ViewControllers);
            existingControllers.Add (new ManageItemsViewController ());
            ViewControllers = existingControllers.ToArray ();
            // Use the same icons as the Settings tab.
            SetTabBarItem ("NachoClient.iOS.ManageItemsViewController", "Manage", "more-settings", "more-settings-active");
            #endif

            FinishedCustomizingViewControllers += (object sender, UITabBarCustomizeChangeEventArgs e) => {
                SaveCustomTabBarOrder (e);
                UpdateNotificationBadge (NcApplication.Instance.Account.Id);
            };

            ViewControllerSelected += (object sender, UITabBarSelectionEventArgs e) => {
                LayoutMoreTable ();
            };

            InsertAccountInfoIntoMoreTab ();
        }

        // Fires only when app starts; not on all fg events
        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            UpdateNotificationBadge (NcApplication.Instance.Account.Id);

            var accountId = NcApplication.Instance.Account.Id;
            switchAccountButton.SetAccountImage (NcApplication.Instance.Account);

            var emailMessageIdString = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EmailNotificationKey, accountId.ToString ());
            if (!String.IsNullOrEmpty (emailMessageIdString)) {
                Log.Info (Log.LOG_UI, "NachoTabBarController: SwitchToNachoNow(emailMessageIdString={0}", emailMessageIdString);
                SwitchToNachoNow ();
            }

            var eventMessageIdString = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EventNotificationKey, accountId.ToString ());
            if (!String.IsNullOrEmpty (eventMessageIdString)) {
                Log.Info (Log.LOG_UI, "NachoTabBarController: SwitchToNachoNow(eventMessageIdString={0}", eventMessageIdString);
                SwitchToNachoNow ();
            }
        }

        protected UINavigationController FindTabRoot (UITabBarItem item)
        {
            foreach (var viewController in ViewControllers) {
                if (item == viewController.TabBarItem) {
                    var vc = (UINavigationController)viewController;
                    return vc;
                }
            }
            return null;
        }

        protected UIViewController FindViewController (UINavigationController vc)
        {
            foreach (var v in vc.ViewControllers) {
                if (v is NachoNowViewController) {
                    return v;
                }
            }
            return null;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_StatusBarHeightChanged == s.Status.SubKind) {
                LayoutMoreTable ();
            }
            if (NcResult.SubKindEnum.Info_UserInterventionFlagChanged == s.Status.SubKind) {
                UpdateNotificationBadge (s.Account.Id);
            }
            if (NcResult.SubKindEnum.Error_PasswordWillExpire == s.Status.SubKind) {
                UpdateNotificationBadge (s.Account.Id);
            }
            if (NcResult.SubKindEnum.Info_McCredPasswordChanged == s.Status.SubKind) {
                UpdateNotificationBadge (s.Account.Id);
            }
        }

        public void SwitchToNachoNow ()
        {
            var navigationController = FindTabRoot (nachoNowItem);
            if (0 == navigationController.ViewControllers.Length) {
                navigationController = MoreNavigationController;
            }
            var nachoNowViewController = (NachoNowViewController)FindViewController (navigationController);
            this.SelectedViewController = navigationController;
            if (null != nachoNowViewController) {
                Log.Info (Log.LOG_UI, "SwitchToNachoNow HandleNotifications");
                nachoNowViewController.HandleNotifications ();
            } else {
                Log.Info (Log.LOG_UI, "SwitchToNachoNow view controller is null");
            }
        }

        void SwitchTo (UITabBarItem item)
        {
            var tab = FindTabRoot (item);
            tab.PopToRootViewController(false);
            this.SelectedViewController = tab;
        }


        public void SwitchToFolders ()
        {
            SwitchTo (foldersItem);
        }

        public void SwitchToInbox ()
        {
            SwitchTo (inboxItem);
        }

        public void SwitchToDeferred ()
        {
            SwitchTo (deferredItem);
        }

        public void SwitchToDeadlines ()
        {
            SwitchTo (deadlinesItem);
        }

        protected string GetTabBarItemTypeName (UIViewController vc)
        {
            if (vc is UINavigationController) {
                return ((UINavigationController)vc).TopViewController.GetType ().ToString ();
            } else {
                return vc.GetType ().ToString ();
            }
        }

        protected void SaveCustomTabBarOrder (UITabBarCustomizeChangeEventArgs e)
        {
            if (e.Changed) {
                var tabOrderArray = new List<String> ();
                foreach (var viewController in e.ViewControllers) {
                    tabOrderArray.Add (GetTabBarItemTypeName (viewController));                  
                }
                NSArray stringArray = NSArray.FromStrings (tabOrderArray.ToArray ());
                NSUserDefaults.StandardUserDefaults [TabBarOrderKey] = stringArray;
            }
        }

        protected void RestoreCustomTabBarOrder ()
        {
            var tabBarOrder = NSUserDefaults.StandardUserDefaults.StringArrayForKey (TabBarOrderKey);
            if (null == tabBarOrder) {
                return;
            }
            var initialList = ViewControllers;
            var orderedList = new List<UIViewController> ();
            foreach (var typeName in tabBarOrder) {
                for (int i = 0; i < initialList.Length; i++) {
                    var vc = initialList [i];
                    if ((null != vc) && (typeName == GetTabBarItemTypeName (vc))) {
                        orderedList.Add (vc);
                        initialList [i] = null;
                    }
                }
            }
            foreach (var vc in initialList) {
                if (null != vc) {
                    orderedList.Add (vc);
                }
            }
            ViewControllers = orderedList.ToArray ();
        }

        protected UITabBarItem SetTabBarItem (string typeName, string title, string imageName, string selectedImageName)
        {
            foreach (var vc in ViewControllers) {
                if (typeName == GetTabBarItemTypeName (vc)) {
                    using (var image = UIImage.FromBundle (imageName).ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate)) {
                        using (var selectedImage = UIImage.FromBundle (selectedImageName).ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate)) {
                            var item = new UITabBarItem (title, image, selectedImage);
                            vc.TabBarItem = item;
                            return item;
                        }
                    }
                }
            }
            return null;
        }

        protected bool IsItemVisible (UITabBarItem item)
        {
            nint visibleItems = 5; // default

            var tableView = (UITableView)View.ViewWithTag (TABLEVIEW_TAG);
            if (null != tableView) {
                visibleItems = ViewControllers.Length - tableView.NumberOfRowsInSection (0);
            }

            for (int i = 0; i < ViewControllers.Length; i++) {
                if (ViewControllers [i].TabBarItem == item) {
                    return (i < visibleItems);
                }
            }
            return false;
        }

        protected void UpdateNotificationBadge (int accountId)
        {
            var showNotificationBadge = LoginHelpers.ShouldAlertUser ();

            settingsItem.BadgeValue = (showNotificationBadge ? @"!" : null);

            if (!IsItemVisible (settingsItem)) {
                MoreNavigationController.TabBarItem.BadgeValue = (showNotificationBadge ? @"!" : null);
            } else {
                MoreNavigationController.TabBarItem.BadgeValue = null;
            }
        }

        protected void InsertAccountInfoIntoMoreTab ()
        {
            var moreTabController = MoreNavigationController.TopViewController;

            moreTabController.NavigationItem.TitleView = switchAccountButton;

            existingTableView = (UITableView)moreTabController.View;
            existingTableView.TintColor = A.Color_NachoGreen;
            existingTableView.ScrollEnabled = false;
            nfloat cellHeight = 0;
            foreach (var cell in existingTableView.VisibleCells) {
                cell.TextLabel.Font = A.Font_AvenirNextMedium14;
                cellHeight = cell.Frame.Height;
            }

            var newView = new UIScrollView (View.Frame);

            newView.BackgroundColor = A.Color_NachoBackgroundGray;

            var tableHeight = (((existingTableView.NumberOfRowsInSection (0)) + 2) * cellHeight + 25);

            existingTableView.Frame = new CGRect (
                A.Card_Horizontal_Indent, A.Card_Vertical_Indent,
                newView.Frame.Width - 2 * A.Card_Horizontal_Indent, tableHeight);
            existingTableView.Layer.CornerRadius = A.Card_Corner_Radius;
            existingTableView.Layer.MasksToBounds = true;
            existingTableView.Layer.BorderWidth = A.Card_Border_Width;
            existingTableView.Layer.BorderColor = A.Card_Border_Color;
            existingTableView.Tag = TABLEVIEW_TAG;

            newView.ContentSize = new CGSize (View.Frame.Width, existingTableView.Frame.Bottom - A.Card_Vertical_Indent - 20);

            newView.AddSubview (existingTableView);
            moreTabController.View = newView;

            LayoutMoreTable ();
        }

        protected void LayoutMoreTable ()
        {
            var tableView = (UITableView)View.ViewWithTag (TABLEVIEW_TAG);
            if (null != tableView) {
                var tableHeight = (tableView.NumberOfRowsInSection (0) * 44);
                tableView.Frame = new CGRect (tableView.Frame.X, tableView.Frame.Y, tableView.Frame.Width, tableHeight);
            }
        }

        public static void ReconfigureMoreTab ()
        {
        }

        public static void UpdateMoreTab ()
        {
        }

        protected void ViewControllerSelectedHandler (object sender, UITabBarSelectionEventArgs e)
        {
            if (e.ViewController == MoreNavigationController) {
                // The user has tapped on the "More" tab in the tab bar. Do what we can to
                // make sure the "More" view is up to date.
                UpdateMoreTab ();
                // Tweak the table cells to be closer to what we want.  We would like to
                // make other changes, but this event is triggered at the wrong time, so
                // those other changes won't stick.  The one change that does seem to stick
                // is to hide the arrow on the right side of the cell.
                foreach (var cell in ((UITableView)existingTableView).VisibleCells) {
                    if (3 == cell.Subviews.Length && cell.Subviews [2] is UIButton) {
                        cell.Subviews [2].Hidden = true;
                    }
                }
            }
        }

        protected bool ViewControllerShouldSelectHandler (UITabBarController tabBarController, UIViewController viewController)
        {
            if (viewController == MoreNavigationController) {
                // The user has tapped on the "More" tab in the tab bar.
                // Pop all pushed subviews so the "More" menu is on top.
                MoreNavigationController.PopToRootViewController (false);
            }
            return true;
        }

        void SwitchAccountButtonPressed ()
        {
            SwitchAccountViewController.ShowDropdown (MoreNavigationController.ViewControllers[0], SwitchToAccount);
        }

        void SwitchToAccount (McAccount account)
        {
            switchAccountButton.SetAccountImage (account);
        }
    }
}
