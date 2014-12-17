// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

using NachoCore.Model;
using NachoCore.Utils;
using System.Drawing;

namespace NachoClient.iOS
{
    public partial class NachoTabBarController : UITabBarController
    {
        protected static string TabBarOrderKey = "TabBarOrder";

        // UI elements needed to customize the "More" tab.
        protected UITableView existingTableView;
        protected AccountInfoView accountInfoView;
        protected static NachoTabBarController instance;

        public NachoTabBarController (IntPtr handle) : base (handle)
        {
        }

        protected UITabBarItem nachoNowItem;
        protected UITabBarItem foldersItem;

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

            RestoreCustomTabBarOrder ();

            nachoNowItem = SetTabBarItem ("NachoClient.iOS.NachoNowViewController", "Now", "nav-nachonow", "nav-nachonow-active"); // Done
            SetTabBarItem ("NachoClient.iOS.CalendarViewController", "Calendar", "nav-calendar", "nav-calendar-active"); // Done
            SetTabBarItem ("NachoClient.iOS.ContactListViewController", "Contacts", "nav-contacts", "nav-contacts-active"); // Done
            SetTabBarItem ("NachoClient.iOS.InboxViewController", "Inbox", "nav-mail", "nav-mail-active"); // Done
            SetTabBarItem ("NachoClient.iOS.GeneralSettingsViewController", "Settings", "more-settings", "more-settings-active"); // Done
            SetTabBarItem ("NachoClient.iOS.SupportViewController", "Support", "more-support", "more-support-active"); // Done
            SetTabBarItem ("NachoClient.iOS.HotListViewController", "Hot", "nav-mail", "nav-mail-active"); // Done
            SetTabBarItem ("NachoClient.iOS.DeferredViewController", "Deferred", "nav-mail", "nav-mail-active"); // Done
            foldersItem = SetTabBarItem ("NachoClient.iOS.FoldersViewController", "Mail", "nav-mail", "nav-mail-active"); // Done
            SetTabBarItem ("NachoClient.iOS.AttachmentsViewController", "Files", "more-files", "more-files-active"); // Done

            FinishedCustomizingViewControllers += (object sender, UITabBarCustomizeChangeEventArgs e) => {
                SaveCustomTabBarOrder (e);
            };

            InsertAccountInfoIntoMoreTab ();
        }

        // Fires only when app starts; not on all fg events
        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            var accountId = LoginHelpers.GetCurrentAccountId ();

            var emailMessageIdString = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EmailNotificationKey, accountId.ToString ());
            if (!String.IsNullOrEmpty (emailMessageIdString)) {
                SwitchToNachoNow ();
            }

            var eventMessageString = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EventNotificationKey, accountId.ToString ());
            if (!String.IsNullOrEmpty (eventMessageString)) {
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

        public void SwitchToNachoNow ()
        {
            var navigationController = FindTabRoot (nachoNowItem);
            if (0 == navigationController.ViewControllers.Length) {
                navigationController = MoreNavigationController;
            }
            var nachoNowViewController = (NachoNowViewController)FindViewController (navigationController);
            this.SelectedViewController = navigationController;
            if (null != nachoNowViewController) {
                nachoNowViewController.HandleNotifications ();
            }
        }

        public void SwitchToFolders ()
        {
            var folderTab = FindTabRoot (foldersItem);
            this.SelectedViewController = folderTab;
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

        public void SetSettingsBadge (bool isDirty)
        {
            for (int i = 0; i < ViewControllers.Length; i++) {
                if (ViewControllers [i].GetType () == typeof(GeneralSettingsViewController)) {
                    ViewControllers [i].TabBarItem.BadgeValue = (isDirty ? @"!" : null);
                    if (i > (TabBar.Items.Length - 2) && isDirty) {
                        MoreNavigationController.TabBarItem.BadgeValue = @"!";
                    } else {
                        MoreNavigationController.TabBarItem.BadgeValue = null;
                    }
                }
            }
        }

        protected void InsertAccountInfoIntoMoreTab ()
        {
            var moreTabController = MoreNavigationController.TopViewController;

            existingTableView = (UITableView)moreTabController.View;
            existingTableView.TintColor = A.Color_NachoGreen;
            existingTableView.ScrollEnabled = false;
            var cellHeight = 0f;
            foreach (var cell in existingTableView.VisibleCells) {
                cell.TextLabel.Font = A.Font_AvenirNextMedium14;
                cellHeight = cell.Frame.Height;
            }

            var newView = new UIScrollView (existingTableView.Frame);

            newView.BackgroundColor = A.Color_NachoBackgroundGray;

            accountInfoView = new AccountInfoView (new RectangleF (
                A.Card_Horizontal_Indent, A.Card_Vertical_Indent,
                newView.Frame.Width - 2 * A.Card_Horizontal_Indent, 80));
            accountInfoView.OnAccountSelected = AccountTapHandler;

            var tableHeight = (((existingTableView.NumberOfRowsInSection(0)) + 2) * cellHeight) + 5;

            existingTableView.Frame = new RectangleF (
                A.Card_Horizontal_Indent, accountInfoView.Frame.Bottom + A.Card_Vertical_Indent,
                newView.Frame.Width - 2 * A.Card_Horizontal_Indent, tableHeight);
            existingTableView.Layer.CornerRadius = A.Card_Corner_Radius;
            existingTableView.Layer.MasksToBounds = true;
            existingTableView.Layer.BorderWidth = A.Card_Border_Width;
            existingTableView.Layer.BorderColor = A.Card_Border_Color;

            newView.ContentSize = new SizeF (moreTabController.View.Frame.Width, existingTableView.Frame.Bottom - A.Card_Vertical_Indent);

            newView.AddSubview (accountInfoView);
            newView.AddSubview (existingTableView);
            moreTabController.View = newView;

            ConfigureAccountInfo ();
        }

        private void AccountTapHandler (McAccount account)
        {
            UIStoryboard x = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
            var vc = (AccountSettingsViewController)x.InstantiateViewController ("AccountSettingsViewController");
            MoreNavigationController.PushViewController (vc, true);
        }

        public void ConfigureAccountInfo ()
        {
            var account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();
            accountInfoView.Configure (account);
        }

        public static void ReconfigureMoreTab ()
        {
            instance.ConfigureAccountInfo ();
        }

        protected void ViewControllerSelectedHandler (object sender, UITabBarSelectionEventArgs e)
        {
            if (e.ViewController == MoreNavigationController) {
                // The user has tapped on the "More" tab in the tab bar. Do what we can to
                // make sure the "More" view is up to date.
                ConfigureAccountInfo ();
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
    }
}
