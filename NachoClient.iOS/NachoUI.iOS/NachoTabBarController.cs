// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

using NachoCore.Model;

namespace NachoClient.iOS
{
    public partial class NachoTabBarController : UITabBarController
    {
        protected static string TabBarOrderKey = "TabBarOrder";

        public NachoTabBarController (IntPtr handle) : base (handle)
        {
        }

        protected UITabBarItem nachoNowItem;

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

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
            SetTabBarItem ("NachoClient.iOS.FoldersViewController", "Mail", "nav-mail", "nav-mail-active"); // Done
            SetTabBarItem ("NachoClient.iOS.AttachmentsViewController", "Files", "more-files", "more-files-active"); // Done

            FinishedCustomizingViewControllers += (object sender, UITabBarCustomizeChangeEventArgs e) => {
                SaveCustomTabBarOrder (e);
            };
                
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

        public void SwitchToNachoNow()
        {
            var navigationController = SelectTabRoot (nachoNowItem);
            var nachoNowViewController = (NachoNowViewController)navigationController.TopViewController;
            nachoNowViewController.HandleNotifications ();
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
    }
}
                      