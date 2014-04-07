// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using System.Drawing;
using MonoTouch.CoreAnimation;
using MonoTouch.CoreGraphics;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using SWRevealViewControllerBinding;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class SidebarViewController : UIViewController
    {
        ///   cellIDs for segues
        ///      "SidebarToAccounts"
        ///      "SidebarToCalendar"
        ///      "SidebarToContacts"
        ///      "SidebarToFolders"
        ///      "SidebarToSettings"
        ///      "SidebarToMessages"
        ///      "SidebarToDeferredMessages"
        ///      "SidebarToNachoNow"
        ///      "SidebarToHome"

     
        protected class SidebarMenu
        {
            public McFolder Folder;
            public McItem.ItemSource Source;
            public string DisplayName;
            public string SegueName;
            public string IconName;

            public SidebarMenu (McFolder folder, string displayName, string segueName)
            {
                SegueName = segueName;
                DisplayName = displayName;
                Folder = folder;
                Source = McItem.ItemSource.ActiveSync;
            }

            public SidebarMenu (McFolder folder, string displayName, string segueName, string iconName) :
                this (folder, displayName, segueName)
            {
                IconName = iconName;
            }
        };

        Section menu;
        Section topMenu;
        NachoFolders email;
        NachoFolders contacts;
        NachoFolders calendars;
        const string SidebarToFoldersSegueId = "SidebarToFolders";
        const string SidebarToContactsSegueId = "SidebarToContacts";
        const string SidebarToCalendarSegueId = "SidebarToCalendar";
        const string SidebarToMessagesSegueId = "SidebarToMessages";
        const string SidebarToDeferredMessagesSegueId = "SidebarToDeferredMessages";
        const string SidebarToNachoNowSegueId = "SidebarToNachoNow";
        const string SidebarToHomeSegueId = "SidebarToHome";
        const string SidebarToAccountsSegueId = "SidebarToAccounts";
        const string SidebarToSettingsSegueId = "SidebarToSettings";

        public UITableView tableview;

        public SidebarViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            this.RevealViewController ().Delegate = new SWRevealDelegate ();
        }

        /// <summary>
        /// Update the list of folders when we appear
        /// </summary>
        /// <param name="animated">If set to <c>true</c> animated.</param>
        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            this.NavigationController.ToolbarHidden = true;

            // Start fresh
            var subviews = View.Subviews;
            foreach (var s in subviews) {
                s.RemoveFromSuperview ();
            }

            // Fade out at top.
            // TODO: Make this nicer
            var top = new UIView (new RectangleF (0.0f, 0.0f, View.Frame.Width, 22.0f));
            top.BackgroundColor = UIColor.Clear;
            AddWhiteGradient (top);
            View.AddSubview (top);

            topMenu = new ThinSection (UIColor.White);
            menu = new SectionWithLineSeparator ();

            email = new NachoFolders (NachoFolders.FilterForEmail);
            contacts = new NachoFolders (NachoFolders.FilterForContacts);
            calendars = new NachoFolders (NachoFolders.FilterForCalendars);

            AddToTopMenu (new SidebarMenu (null, "Now!", SidebarToNachoNowSegueId, "Nacho-Cove-Icon"));
            AddToMenu (new SidebarMenu (null, "Tasks", SidebarToDeferredMessagesSegueId, "ic_action_time"));

            AddToMenu (new SidebarMenu (null, "Folders", SidebarToFoldersSegueId, "ic_action_collection"));

            for (int i = 0; i < email.Count (); i++) {
                McFolder f = email.GetFolder (i);
                AddToMenu (new SidebarMenu (f, f.DisplayName, SidebarToMessagesSegueId));
                if (f.DisplayName.Equals ("Inbox")) {
                    AddToTopMenu (new SidebarMenu (f, f.DisplayName, SidebarToMessagesSegueId, "ic_action_email"));
                }
            }

            AddToTopMenu (new SidebarMenu (null, "Contacts", SidebarToContactsSegueId, "ic_action_group"));
            AddToMenu (new SidebarMenu (null, "Contacts", SidebarToContactsSegueId, "ic_action_group"));
            for (int i = 0; i < contacts.Count (); i++) {
                McFolder f = contacts.GetFolder (i);
                AddToMenu (new SidebarMenu (f, f.DisplayName, SidebarToContactsSegueId));
            }
//            var deviceContacts = new SidebarMenu (null, "Device Contacts", SidebarToContactsSegueId);
//            deviceContacts.Source = McItem.ItemSource.Device;
//            AddToMenu (deviceContacts);

            AddToTopMenu (new SidebarMenu (null, "Calendar", SidebarToCalendarSegueId, "ic_action_event"));
            AddToMenu (new SidebarMenu (null, "Calendars", SidebarToCalendarSegueId, "ic_action_event"));
            for (int i = 0; i < calendars.Count (); i++) {
                McFolder f = calendars.GetFolder (i);
                AddToMenu (new SidebarMenu (f, f.DisplayName, SidebarToCalendarSegueId));
            }
//            var deviceCalendar = new SidebarMenu (null, "Device Calendar", SidebarToCalendarSegueId);
//            deviceCalendar.Source = McItem.ItemSource.Device;
//            AddToMenu (deviceCalendar);

            AddToMenu (new SidebarMenu (null, "Help", SidebarToHomeSegueId, "ic_action_help"));
            AddToMenu (new SidebarMenu (null, "Settings", SidebarToSettingsSegueId, "ic_action_settings"));
            AddToMenu (new SidebarMenu (null, "Accounts", SidebarToAccountsSegueId, "ic_action_accounts"));

            var root = new RootElement ("");
            root.Add (topMenu);
            root.Add (menu);
            var dvc = new DialogViewController (root);
            tableview = (UITableView)dvc.View;
            tableview.SeparatorColor = UIColor.Clear;
            tableview.BackgroundColor = UIColor.White;
            View.AddSubview (tableview);
            View.SendSubviewToBack (tableview);
        }

        protected StyledStringElement PrepareMenuElement(SidebarMenu m)
        {
            StyledStringElement e;
            if (null == m.IconName) {
                e = new StyledStringElementWithIndent (m.DisplayName);
            } else {
                using (var image = UIImage.FromBundle (m.IconName)) {
                    var scaledImage = image.Scale (new System.Drawing.SizeF (22.0f, 22.0f));
                    e = new StyledStringElementWithIcon (m.DisplayName, scaledImage);
                }
            }
            e.Tapped += () => {
                FireSegue (m);
            };
            return e;
        }

        protected void AddToTopMenu (SidebarMenu m)
        {
            topMenu.Add (PrepareMenuElement (m));
        }

        protected void AddToMenu (SidebarMenu m)
        {
            menu.Add (PrepareMenuElement(m));
        }

        protected void FireSegue (SidebarMenu m)
        {
            var holder = new SegueHolder (m);
            PerformSegue (m.SegueName, holder);
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            Log.Info (Log.LOG_UI, "PrepareForSegue: {0}", segue.Identifier);

            UIViewController destViewController = (UIViewController)segue.DestinationViewController;

            var holder = (SegueHolder)sender;
            var m = (SidebarMenu)holder.value;

            destViewController.Title = m.DisplayName;

            switch (segue.Identifier) {
            case SidebarToContactsSegueId:
                {
                    ContactsViewController vc = (ContactsViewController)destViewController;
                    vc.UseDeviceContacts = (m.Source == McItem.ItemSource.Device);
                }
                break;
            case SidebarToCalendarSegueId:
                {
                    CalendarViewController vc = (CalendarViewController)destViewController;
                    vc.UseDeviceCalendar = (m.Source == McItem.ItemSource.Device);
                }
                break;
            case SidebarToMessagesSegueId:
                {
                    MessageListViewController vc = (MessageListViewController)destViewController;
                    var messageList = new NachoEmailMessages (m.Folder);
                    vc.SetEmailMessages (messageList);
                }
                break;
            case SidebarToDeferredMessagesSegueId:
                {
                    MessageListViewController vc = (MessageListViewController)destViewController;
                    var messageList = new NachoDeferredEmailMessages ();
                    vc.SetEmailMessages (messageList);
                }
                break;
            default:
                // No worries; nothing to send to destination view controller
                break;
            }

            if (segue.GetType () == typeof(SWRevealViewControllerSegue)) {
                Log.Info (Log.LOG_UI, "PrepareForSqueue: SWRevealViewControllerSegue");
                SWRevealViewControllerSegue swSegue = (SWRevealViewControllerSegue)segue;
                swSegue.PerformBlock = PerformBlock;
            }
        }

        /// <summary>
        /// Started from PrepareForSegue
        /// </summary>
        public void PerformBlock (SWRevealViewControllerSegue s, UIViewController svc, UIViewController dvc)
        {
            Log.Info (Log.LOG_UI, "PrepareForSegue: PerformBlock");
            UINavigationController navController = (UINavigationController)this.RevealViewController ().FrontViewController;
            navController.SetViewControllers (new UIViewController[] { dvc }, true);
            this.RevealViewController ().SetFrontViewPosition (FrontViewPosition.Left, true);
        }

        public void AddWhiteGradient(UIView view)
        {
            var layer = new CAGradientLayer();
            var colors = new CGColor[] {
                UIColor.White.CGColor,
                UIColor.Clear.CGColor,
            };
            layer.Colors = colors;
            layer.Frame = view.Frame;
            view.Layer.AddSublayer (layer);
        }

        public class SWRevealDelegate : SWRevealViewControllerDelegate
        {
            public override void WillMoveToPosition (SWRevealViewController revealController, FrontViewPosition position)
            {
                if (SWRevealViewControllerBinding.FrontViewPosition.Left == revealController.FrontViewPosition) {
                    var lockingView = new UIView ();
                    lockingView.Alpha = 0.5f;
                    lockingView.BackgroundColor = UIColor.Black;
                    lockingView.TranslatesAutoresizingMaskIntoConstraints = false;
                    var tap = new UITapGestureRecognizer (revealController, new MonoTouch.ObjCRuntime.Selector ("revealToggle:"));
                    lockingView.AddGestureRecognizer (tap);
                    lockingView.AddGestureRecognizer (revealController.PanGestureRecognizer);
                    lockingView.Tag = 1000;
                    revealController.FrontViewController.View.AddSubview (lockingView);
                    NSDictionary viewsDictionary = new NSDictionary ("lockingView", lockingView);
                    revealController.FrontViewController.View.AddConstraints (NSLayoutConstraint.FromVisualFormat ("|[lockingView]|", 0, null, viewsDictionary));
                    revealController.FrontViewController.View.AddConstraints (NSLayoutConstraint.FromVisualFormat ("V:|[lockingView]|", 0, null, viewsDictionary));
                    lockingView.SizeToFit ();
                } else {
                    var lockingView = revealController.FrontViewController.View.ViewWithTag (1000);
                    if (null != lockingView) {
                        lockingView.RemoveFromSuperview ();
                    }
                }
            }
        }
    }
}

