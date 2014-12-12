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
        protected UILabel accountNameLabel;
        protected UILabel emailAddressLabel;
        protected UILabel initialsCircle;
        protected UITableView existingTableView;
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

        protected UINavigationController SelectTabRoot (UITabBarItem item)
        {
            int i = 0;
            foreach (var viewController in ViewControllers) {
                if (item == viewController.TabBarItem) {
                    var vc = (UINavigationController)viewController;
                    vc.PopToRootViewController (false);
                    this.SelectedIndex = i;
                    return vc;
                }
                i = i + 1;
            }
            return null;
        }

        public void SwitchToNachoNow ()
        {
            var navigationController = SelectTabRoot (nachoNowItem);
            var nachoNowViewController = (NachoNowViewController)navigationController.TopViewController;
            nachoNowViewController.HandleNotifications ();
        }

        public void SwitchToFolders ()
        {
            SelectTabRoot (foldersItem);
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

            foreach (var cell in existingTableView.VisibleCells) {
                cell.TextLabel.Font = A.Font_AvenirNextMedium14;
            }

            var newView = new UIScrollView (existingTableView.Frame);

            newView.BackgroundColor = A.Color_NachoBackgroundGray;

            var accountInfoView = new UIView (new RectangleF (
                A.Card_Horizontal_Indent, A.Card_Vertical_Indent,
                newView.Frame.Width - 2 * A.Card_Horizontal_Indent, 100));
            accountInfoView.BackgroundColor = UIColor.White;
            accountInfoView.Layer.CornerRadius = A.Card_Corner_Radius;
            accountInfoView.Layer.MasksToBounds = true;
            accountInfoView.Layer.BorderWidth = A.Card_Border_Width;
            accountInfoView.Layer.BorderColor = A.Card_Border_Color;

            initialsCircle = new UILabel (new RectangleF (18, 20, 60, 60));
            initialsCircle.Font = A.Font_AvenirNextRegular24;
            initialsCircle.TextColor = UIColor.White;
            initialsCircle.TextAlignment = UITextAlignment.Center;
            initialsCircle.LineBreakMode = UILineBreakMode.Clip;
            initialsCircle.Layer.CornerRadius = 30;
            initialsCircle.Layer.MasksToBounds = true;
            initialsCircle.Layer.BorderColor = A.Card_Border_Color;
            initialsCircle.Layer.BorderWidth = A.Card_Border_Width;
            accountInfoView.AddSubview (initialsCircle);

            accountNameLabel = new UILabel (new RectangleF (initialsCircle.Frame.Right + 16, 31, accountInfoView.Frame.Width - (initialsCircle.Frame.Right + 26), 20));
            accountNameLabel.TextAlignment = UITextAlignment.Left;
            accountInfoView.AddSubview (accountNameLabel);

            emailAddressLabel = new UILabel (new RectangleF (accountNameLabel.Frame.X, accountNameLabel.Frame.Bottom + 6, accountNameLabel.Frame.Width, accountNameLabel.Frame.Height - 5));
            emailAddressLabel.Font = A.Font_AvenirNextMedium14;
            accountInfoView.AddSubview (emailAddressLabel);

            // checks for a 4s screen
            var tableHeight = (500 < newView.Frame.Height ? newView.Frame.Height - accountInfoView.Frame.Bottom - 3 * A.Card_Vertical_Indent - 11 : newView.Frame.Height - accountInfoView.Frame.Bottom - A.Card_Vertical_Indent + 36);

            existingTableView.Frame = new RectangleF (
                A.Card_Horizontal_Indent, accountInfoView.Frame.Bottom + A.Card_Vertical_Indent,
                newView.Frame.Width - 2 * A.Card_Horizontal_Indent, tableHeight);
            existingTableView.Layer.CornerRadius = A.Card_Corner_Radius;
            existingTableView.Layer.MasksToBounds = true;
            existingTableView.Layer.BorderWidth = A.Card_Border_Width;
            existingTableView.Layer.BorderColor = A.Card_Border_Color;

            newView.ContentSize = new SizeF (moreTabController.View.Frame.Width, existingTableView.Frame.Bottom - 2 * A.Card_Vertical_Indent);

            newView.AddSubview (accountInfoView);
            newView.AddSubview (existingTableView);
            moreTabController.View = newView;

            ConfigureAccountInfo ();

            var accountTapRecognizer = new UITapGestureRecognizer ();
            accountTapRecognizer = new UITapGestureRecognizer ();
            accountTapRecognizer.NumberOfTapsRequired = 1;
            accountTapRecognizer.AddTarget (AccountTapHandler);
            accountInfoView.AddGestureRecognizer (accountTapRecognizer);
        }

        private void AccountTapHandler (NSObject sender)
        {
            UIStoryboard x = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
            var vc = (AccountSettingsViewController)x.InstantiateViewController ("AccountSettingsViewController");
            MoreNavigationController.PushViewController (vc, true);
        }

        public void ConfigureAccountInfo ()
        {
            var account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();

            if (!string.IsNullOrEmpty (account.DisplayName)) {
                initialsCircle.Text = Util.NameToLetters (account.DisplayName);
            } else if (!string.IsNullOrEmpty (account.EmailAddr)) {
                initialsCircle.Text = Util.NameToLetters (account.EmailAddr);
            } else {
                initialsCircle.Text = "?";
            }

            McEmailAddress address;
            bool validAddress = McEmailAddress.Get (account.Id, account.EmailAddr, out address);
            initialsCircle.BackgroundColor = Util.ColorForUser (validAddress ? address.ColorIndex : Util.PickRandomColorForUser ());

            if (string.IsNullOrEmpty (account.DisplayName)) {
                accountNameLabel.Text = "Account name";
                accountNameLabel.Font = A.Font_AvenirNextRegular14;
                accountNameLabel.TextColor = UIColor.LightGray;
                emailAddressLabel.TextColor = A.Color_NachoGreen;
            } else {
                accountNameLabel.Text = account.DisplayName;
                accountNameLabel.Font = A.Font_AvenirNextDemiBold17;
                accountNameLabel.TextColor = A.Color_NachoGreen;
                emailAddressLabel.TextColor = A.Color_NachoTextGray;
            }

            emailAddressLabel.Text = account.EmailAddr;
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
