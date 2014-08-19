// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using Xamarin.Contacts;
using SWRevealViewControllerBinding;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    public partial class ContactsViewController : NcUITableViewController, IContactsTableViewSourceDelegate
    {
        ContactsTableViewSource contactTableViewSource;

        public ContactsViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Manages the search bar & auto-complete table.
            contactTableViewSource = new ContactsTableViewSource ();
            contactTableViewSource.SetOwner (this, SearchDisplayController);

            TableView.Source = contactTableViewSource;
            TableView.SeparatorColor = A.Color_NachoSeparator;
            SearchDisplayController.SearchResultsTableView.Source = contactTableViewSource;

            NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { A.RevealButton (this), A.NachoNowButton (this) };

            addButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("ContactsToContactEdit", new SegueHolder (null));
            };
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            LoadContacts ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_ContactSetChanged == s.Status.SubKind) {
                LoadContacts ();
            }
        }

        protected void LoadContacts ()
        {
            NachoClient.Util.HighPriority ();
            var contacts = McContact.AllContactsSortedByName ();
            contactTableViewSource.SetContacts (contacts);
            TableView.ReloadData ();
            NachoClient.Util.RegularPriority ();
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("ContactsToContact")) {
                var h = sender as SegueHolder;
                var c = (McContact)h.value;
                ContactViewController destinationController = (ContactViewController)segue.DestinationViewController;
                destinationController.contact = c;
                return;
            }
            if (segue.Identifier.Equals ("ContactsToContactDetail")) {
                var h = sender as SegueHolder;
                var c = (McContact)h.value;
                ContactDetailViewController destinationController = (ContactDetailViewController)segue.DestinationViewController;
                destinationController.contact = c;
                return;
            }
            if (segue.Identifier.Equals ("ContactsToContactEdit")) {
                return;
            }
            if (segue.Identifier.Equals ("SegueToNachoNow")) {
                return;
            }
            if (segue.Identifier.Equals ("ContactsToQR")) {
//                var h = sender as SegueHolder;
//                var c = (McContact)h.value;
//                var attendee = new McAttendee (c.GetDisplayNameOrEmailAddress(), c.GetEmailAddress (), NcAttendeeType.Optional);
//                List<McAttendee> attendeeList = new List<McAttendee> ();
//                attendeeList.Add (attendee);
//                EventViewController evc = (EventViewController)segue.DestinationViewController;
//                evc.SetEventPresetFields (attendeeList);
//                evc.SetCalendarItem (null, CalendarItemEditorAction.create);
                var h = sender as SegueHolder;
                MessageComposeViewController mcvc = (MessageComposeViewController)segue.DestinationViewController;
                mcvc.SetEmailPresetFields (new NcEmailAddress (NcEmailAddress.Kind.To, (string)h.value));
                return;
            }
            if (segue.Identifier.Equals ("ContactsToMessageCompose")) {
                var h = sender as SegueHolder;
                MessageComposeViewController mcvc = (MessageComposeViewController)segue.DestinationViewController;
                mcvc.SetEmailPresetFields (new NcEmailAddress (NcEmailAddress.Kind.To, (string)h.value));
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        /// IContactsTableViewSourceDelegate
        public void PerformSegueForDelegate (string identifier, NSObject sender)
        {
            PerformSegue (identifier, sender);
        }

        /// IContactsTableViewSourceDelegate
        public void ContactSelectedCallback (McContact contact)
        {
            PerformSegue ("ContactsToContactDetail", new SegueHolder (contact));
        }
    }
}
