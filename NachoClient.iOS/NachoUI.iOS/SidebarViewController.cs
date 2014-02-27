// This file has been autogenerated from a class added in the UI designer.

using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Collections.Generic;
using SWRevealViewControllerBinding;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class SidebarViewController : UITableViewController
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

     
        class SidebarMenu
        {
            public int Indent;
            public string SegueName;
            public string DisplayName;
            public McFolder Folder;
            public bool isDeviceContactsKludge;
            public bool isDeviceCalendarKludge;
            public string IconName;

            public SidebarMenu (McFolder folder, string displayName, string segueName)
            {
                Indent = 0;
                SegueName = segueName;
                DisplayName = displayName;
                Folder = folder;
                isDeviceContactsKludge = false;
                isDeviceCalendarKludge = false;
            }

            public SidebarMenu (McFolder folder, string displayName, string segueName, string iconName) :
                this (folder, displayName, segueName)
            {
                IconName = iconName;
            }
        };

        List<SidebarMenu> topMenu;
        List<SidebarMenu> menu;
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

            topMenu = new List<SidebarMenu> ();
            menu = new List<SidebarMenu> ();

            email = new NachoFolders (NachoFolders.FilterForEmail);
            contacts = new NachoFolders (NachoFolders.FilterForContacts);
            calendars = new NachoFolders (NachoFolders.FilterForCalendars);

            topMenu.Add (new SidebarMenu (null, "Now", SidebarToNachoNowSegueId, "ic_action_time"));

            menu.Add (new SidebarMenu (null, "Folders", SidebarToFoldersSegueId));

            for (int i = 0; i < email.Count (); i++) {
                McFolder f = email.GetFolder (i);
                var m = new SidebarMenu (f, f.DisplayName, SidebarToMessagesSegueId);
                m.Indent = 1;
                menu.Add (m);
                if (f.DisplayName.Equals ("Inbox")) {
                    topMenu.Add (new SidebarMenu (f, f.DisplayName, SidebarToMessagesSegueId, "ic_action_email"));
                }
            }
            menu.Add (new SidebarMenu (null, "Later", SidebarToDeferredMessagesSegueId));

            topMenu.Add (new SidebarMenu (null, "Contacts", SidebarToContactsSegueId, "ic_action_group"));
            menu.Add (new SidebarMenu (null, "Contacts", SidebarToContactsSegueId));
            for (int i = 0; i < contacts.Count (); i++) {
                McFolder f = contacts.GetFolder (i);
                var m = new SidebarMenu (f, f.DisplayName, SidebarToContactsSegueId);
                m.Indent = 1;
                menu.Add (m);
            }
            var deviceContacts = new SidebarMenu (null, "Device Contacts", SidebarToContactsSegueId);
            deviceContacts.isDeviceContactsKludge = true;
            menu.Add (deviceContacts);

            topMenu.Add (new SidebarMenu (null, "Calendars", SidebarToCalendarSegueId, "ic_action_event"));
            menu.Add (new SidebarMenu (null, "Calendars", SidebarToCalendarSegueId));
            for (int i = 0; i < calendars.Count (); i++) {
                McFolder f = calendars.GetFolder (i);
                var m = new SidebarMenu (f, f.DisplayName, SidebarToCalendarSegueId);
                m.Indent = 1;
                menu.Add (m);
            }
            var deviceCalendar = new SidebarMenu (null, "Device Calendar", SidebarToCalendarSegueId);
            deviceCalendar.isDeviceCalendarKludge = true;
            menu.Add (deviceCalendar);

            menu.Add (new SidebarMenu (null, "Tutorial", SidebarToHomeSegueId));
            menu.Add (new SidebarMenu (null, "Accounts", "SidebarToAccounts"));
            menu.Add (new SidebarMenu (null, "Settings", "SidebarToSettings"));

            TableView.ReloadData ();
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            Log.Info (Log.LOG_UI, "PrepareForSegue: {0}", segue.Identifier);

            NSIndexPath indexPath = this.TableView.IndexPathForSelectedRow;
            UIViewController destViewController = (UIViewController)segue.DestinationViewController;

            SidebarMenu m;

            if (0 == indexPath.Section) {
                m = topMenu [indexPath.Row];
            } else {
                m = menu [indexPath.Row];
            }

            destViewController.Title = m.DisplayName;

            switch (segue.Identifier) {
            case SidebarToContactsSegueId:
                {
                    ContactsViewController vc = (ContactsViewController)destViewController;
                    vc.UseDeviceContacts = m.isDeviceContactsKludge;
                }
                break;
            case SidebarToCalendarSegueId:
                {
                    CalendarViewController vc = (CalendarViewController)destViewController;
                    vc.UseDeviceCalendar = m.isDeviceCalendarKludge;
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

        /// <summary>
        /// Called by the TableView to determine how many sections(groups) there are.
        /// </summary>
        public override int NumberOfSections (UITableView tableView)
        {
            return 2;
        }

        /// <summary>
        /// Called by the TableView to determine how many cells to create for that particular section.
        /// </summary>
        public override int RowsInSection (UITableView tableview, int section)
        {
            if (0 == section) {
                return topMenu.Count; // Now, Inbox, Contacts, Calendars
            } else {
                return menu.Count;
            }
        }

        /// <summary>
        /// Called by the TableView to get the actual UITableViewCell to render for the particular row
        /// </summary>
        public override UITableViewCell GetCell (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
        {
            SidebarMenu m = null;
            if (0 == indexPath.Section) {
                m = topMenu [indexPath.Row];
            } else {
                m = menu [indexPath.Row];
            }
            var cell = tableView.DequeueReusableCell (m.SegueName);
            NachoCore.NachoAssert.True (null != cell);
            cell.TextLabel.Text = m.DisplayName;
            if (null == m.IconName) {
                cell.ImageView.Image = null;
            } else {
                cell.ImageView.Image = UIImage.FromBundle (m.IconName);
            }
            return cell;
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

