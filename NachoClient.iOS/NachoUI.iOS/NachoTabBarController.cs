// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace NachoClient.iOS
{
    public partial class NachoTabBarController : UITabBarController
    {
        protected static string TabBarOrderKey = "TabBarOrder";

        public NachoTabBarController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            TabBar.BarTintColor = UIColor.White;
            TabBar.TintColor = A.Color_NachoIconGray;
            TabBar.Translucent = false;

            MoreNavigationController.NavigationBar.TintColor = A.Color_NachoBlue;
            MoreNavigationController.NavigationBar.Translucent = false;

            RestoreCustomTabBarOrder ();

            SetTabBarItem ("NachoClient.iOS.NachoNowViewController", "Now", "nav-nachonow", "nav-nachonow-active");
            SetTabBarItem ("NachoClient.iOS.CalendarViewController", "Calendar", "nav-calendar", "nav-calendar-active");
            SetTabBarItem ("NachoClient.iOS.ContactListViewController", "Contacts", "nav-contacts", "nav-contacts-active");
            SetTabBarItem ("NachoClient.iOS.InboxViewController", "Inbox", "navbar-icn-inbox", "navbar-icn-inbox-active");
            SetTabBarItem ("NachoClient.iOS.GeneralSettingsViewController", "Settings", "icn-inbox", "icn-inbox");
            SetTabBarItem ("NachoClient.iOS.SupportViewController", "Support", "icn-inbox", "icn-inbox");
            SetTabBarItem ("NachoClient.iOS.HotListViewController", "Hot", "navbar-icn-inbox", "navbar-icn-inbox-active");
            SetTabBarItem ("NachoClient.iOS.DeferredViewController", "Deferred", "navbar-icn-inbox", "navbar-icn-inbox-active");
            SetTabBarItem ("NachoClient.iOS.FoldersViewController", "Mail", "nav-folder", "nav-folder-active");
            SetTabBarItem ("NachoClient.iOS.AttachmentsViewController", "Files", "menu-icn-attachments", "menu-icn-attachments");

            FinishedCustomizingViewControllers += (object sender, UITabBarCustomizeChangeEventArgs e) => {
                SaveCustomTabBarOrder (e);
            };
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

        protected void SetTabBarItem (string typeName, string title, string imageName, string selectedImageName)
        {
            foreach (var vc in ViewControllers) {
                if (typeName == GetTabBarItemTypeName (vc)) {
                    using (var image = UIImage.FromBundle (imageName)) {
                        using (var selectedImage = UIImage.FromBundle (selectedImageName)) {
                            var item = new UITabBarItem (title, image, selectedImage);
                            vc.TabBarItem = item;
                        }
                    }
                }
            }
        }

        public void SetSettingsBadge(bool isDirty)
        {
            for (int i = 0; i < ViewControllers.Length; i++) {
                if (ViewControllers [i].GetType() == typeof(GeneralSettingsViewController)) {
                    ViewControllers [i].TabBarItem.BadgeValue = (isDirty ? @"!" : null);
                    //TODO: W/larger screen size there can be more than 5 TabBarItems visible in the list
                    if (i > 3 && isDirty) {
                        MoreNavigationController.TabBarItem.BadgeValue = @"!";
                    } else {
                        MoreNavigationController.TabBarItem.BadgeValue = null;
                    }
                }
            }
        }
    }
}
                      