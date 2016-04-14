// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Linq;
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
        protected UITableView moreTableView;
        protected UIScrollView moreScrollView;
        protected static NachoTabBarController instance;

        SwitchAccountButton switchAccountButton;

        public NachoTabBarController () : base ()
        {

            var nowNavController = new UINavigationController (new NachoNowViewController ());
            nachoNowItem = nowNavController.TabBarItem = MakeTabBarItem ("Hot", "nav-nachonow");

            var inboxNavController = new UINavigationController (new InboxViewController ());
            inboxItem = inboxNavController.TabBarItem = MakeTabBarItem ("Inbox", "nav-mail");

            var chatsNavController = new UINavigationController (new ChatsViewController ());
            chatsItem = chatsNavController.TabBarItem = MakeTabBarItem ("Chats", "nav-chat");

            var calendarNavController = new UINavigationController (new CalendarViewController ());
            calendarNavController.TabBarItem = MakeTabBarItem ("Calendar", "nav-calendar");

            var contactsNavController = new UINavigationController (new ContactListViewController ());
            contactsNavController.TabBarItem = MakeTabBarItem ("Contacts", "nav-contacts");

            var foldersNavController = new UINavigationController (new FoldersViewController ());
            foldersItem = foldersNavController.TabBarItem = MakeTabBarItem ("Mail", "nav-mail");

            var deferredNavController = new UINavigationController (new DeferredViewController ());
            deferredItem = deferredNavController.TabBarItem = MakeTabBarItem ("Deferred", "nav-mail");

            var deadlinesNavController = new UINavigationController (new DeadlinesViewController ());
            deadlinesItem = deadlinesNavController.TabBarItem = MakeTabBarItem ("Deadlines", "nav-mail");

            var filesNavController = new UINavigationController (new FileListViewController ());
            filesNavController.TabBarItem = MakeTabBarItem ("Files", "more-files");

            var settingsNavController = new UINavigationController (new GeneralSettingsViewController ());
            settingsItem = settingsNavController.TabBarItem = MakeTabBarItem ("Settings", "more-settings");

            var supportNavController = new UINavigationController (new SupportViewController ());
            supportNavController.TabBarItem = MakeTabBarItem ("Support", "more-support");

            var aboutNavController = new UINavigationController (new AboutUsViewController ());
            aboutNavController.TabBarItem = MakeTabBarItem ("About Nacho Mail", "more-nachomail");

            ViewControllers = new UIViewController[] {
                nowNavController,
                inboxNavController,
                chatsNavController,
                calendarNavController,
                contactsNavController,
                foldersNavController,
                deferredNavController,
                deadlinesNavController,
                filesNavController,
                settingsNavController,
                supportNavController,
                aboutNavController
            };
        }

        protected UITabBarItem nachoNowItem;
        protected UITabBarItem settingsItem;
        protected UITabBarItem foldersItem;
        protected UITabBarItem deadlinesItem;
        protected UITabBarItem deferredItem;
        protected UITabBarItem inboxItem;
        protected UITabBarItem chatsItem;

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
                UpdateNotificationBadge ();
            };

            ViewControllerSelected += (object sender, UITabBarSelectionEventArgs e) => {
                LayoutMoreTable ();
            };

            InsertAccountInfoIntoMoreTab ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            UpdateNotificationBadge ();
            UpdateChatsBadge ();
        }

        // Fires only when app starts; not on all fg events
        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            View.BackgroundColor = A.Color_NachoGreen;

            var eventNotifications = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EventNotificationKey);
            if (0 != eventNotifications.Count) {
                Log.Info (Log.LOG_UI, "NachoTabBarController: SwitchToNachoNow for event notification");
                SwitchToNachoNow ();
            }

            var emailNotifications = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EmailNotificationKey);
            if (0 != emailNotifications.Count) {
                Log.Info (Log.LOG_UI, "NachoTabBarController: SwitchToNachoNow for email notification");
                SwitchToNachoNow ();
            }

            var chatNotifications = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.ChatNotificationKey);
            if (0 != chatNotifications.Count) {
                Log.Info (Log.LOG_UI, "NachoTabBarController: SwitchToNachoNow for chat notification");
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
                UpdateNotificationBadge ();
            }
            if (NcResult.SubKindEnum.Error_PasswordWillExpire == s.Status.SubKind) {
                UpdateNotificationBadge ();
            }
            if (NcResult.SubKindEnum.Info_McCredPasswordChanged == s.Status.SubKind) {
                UpdateNotificationBadge ();
            }
            if (NcResult.SubKindEnum.Info_AccountChanged == s.Status.SubKind) {
                UpdateSwitchAccountButton ();
            }
            if (NcResult.SubKindEnum.Info_ChatMessageAdded == s.Status.SubKind || NcResult.SubKindEnum.Info_EmailMessageMarkedReadSucceeded == s.Status.SubKind) {
                UpdateChatsBadge ();
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
            tab.PopToRootViewController (false);
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
            var orderedNameList = new List<string> (tabBarOrder);
            if (!orderedNameList.Contains ("NachoClient.iOS.ChatsViewController")) {
                if (orderedNameList.Count > 2) {
                    orderedNameList.Insert (2, "NachoClient.iOS.ChatsViewController");
                } else {
                    orderedNameList.Add ("NachoClient.iOS.ChatsViewController");
                }
                NSUserDefaults.StandardUserDefaults [TabBarOrderKey] = NSArray.FromStrings (orderedNameList.ToArray ());
            }
            var initialList = ViewControllers;
            var orderedList = new List<UIViewController> ();
            foreach (var typeName in orderedNameList) {
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

        protected UITabBarItem MakeTabBarItem (string title, string imageName)
        {
            using (var image = UIImage.FromBundle (imageName).ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate)) {
                using (var selectedImage = UIImage.FromBundle (imageName + "-active").ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate)) {
                    return new UITabBarItem (title, image, selectedImage);
                }
            }
        }

        protected bool IsItemVisible (UITabBarItem item)
        {
            nint visibleItems = 5; // default

            if (null != moreTableView) {
                visibleItems = ViewControllers.Length - moreTableView.NumberOfRowsInSection (0);
            }

            for (int i = 0; i < ViewControllers.Length; i++) {
                if (ViewControllers [i].TabBarItem == item) {
                    return (i < visibleItems);
                }
            }
            return false;
        }

        protected void UpdateNotificationBadge ()
        {
            var showNotificationBadge = LoginHelpers.ShouldAlertUser ();

            settingsItem.BadgeValue = (showNotificationBadge ? @"!" : null);

            if (!IsItemVisible (settingsItem)) {
                MoreNavigationController.TabBarItem.BadgeValue = (showNotificationBadge ? @"!" : null);
            } else {
                MoreNavigationController.TabBarItem.BadgeValue = null;
            }
        }

        protected void UpdateChatsBadge ()
        {
            int unreadCount = 0;
            if (NcApplication.Instance.Account.AccountType == McAccount.AccountTypeEnum.Unified) {
                unreadCount = McChat.UnreadMessageCountForUnified ();
            } else {
                unreadCount = McChat.UnreadMessageCountForAccount (NcApplication.Instance.Account.Id);
            }
            if (unreadCount > 0) {
                chatsItem.BadgeValue = unreadCount.ToString ();
            } else {
                chatsItem.BadgeValue = null;
            }
        }

        protected void InsertAccountInfoIntoMoreTab ()
        {
            var moreTabController = MoreNavigationController.TopViewController;

            moreTabController.NavigationItem.TitleView = switchAccountButton;

            moreTableView = (UITableView)moreTabController.View;
            moreTableView.TintColor = A.Color_NachoGreen;

            moreTableView.ScrollEnabled = false;

            moreScrollView = new UIScrollView (View.Frame);

            moreScrollView.BackgroundColor = A.Color_NachoBackgroundGray;

            moreTableView.Frame = new CGRect (
                A.Card_Horizontal_Indent, A.Card_Vertical_Indent,
                moreScrollView.Frame.Width - 2 * A.Card_Horizontal_Indent, moreScrollView.Bounds.Height - 2 * A.Card_Vertical_Indent);
            moreTableView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            moreTableView.Layer.CornerRadius = A.Card_Corner_Radius;
            moreTableView.Layer.MasksToBounds = true;
            moreTableView.Layer.BorderWidth = A.Card_Border_Width;
            moreTableView.Layer.BorderColor = A.Card_Border_Color;

            moreScrollView.AddSubview (moreTableView);
            moreTabController.View = moreScrollView;

            LayoutMoreTable ();
        }

        // ViewDidAppear is not reliable
        protected void LayoutMoreTable ()
        {
            UpdateSwitchAccountButton ();
            if (null != moreTableView) {
                var tableHeight = (moreTableView.NumberOfRowsInSection (0) * 44);
                moreTableView.Frame = new CGRect (moreTableView.Frame.X, moreTableView.Frame.Y, moreTableView.Frame.Width, tableHeight);
            }

            moreScrollView.ContentSize = new CGSize (moreScrollView.Bounds.Width, moreTableView.Frame.Bottom + A.Card_Vertical_Indent);
        }

        protected void UpdateSwitchAccountButton ()
        {
            if ((null != switchAccountButton) && (null != NcApplication.Instance.Account)) {
                switchAccountButton.SetAccountImage (NcApplication.Instance.Account);
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
                foreach (var cell in ((UITableView)moreTableView).VisibleCells) {
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
            SwitchAccountViewController.ShowDropdown (MoreNavigationController.ViewControllers [0], SwitchToAccount);
        }

        void SwitchToAccount (McAccount account)
        {
            switchAccountButton.SetAccountImage (account);
        }
    }
}
