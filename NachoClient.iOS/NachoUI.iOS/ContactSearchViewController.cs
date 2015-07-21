// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using CoreGraphics;
using System.Globalization;
using System.Linq;
using System.Text;
using Foundation;
using UIKit;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.iOS
{
    /// <summary>
    /// Contacts view controller.
    /// Fetches data from an INachoContacts object.
    /// TODO: Extend INachoContacts with filtering.
    /// Handles search in an INachoContacts.
    /// Handles async search too.
    /// </summary>
    public partial class ContactSearchViewController : NcUITableViewController, IContactsTableViewSourceDelegate, INachoContactChooser
    {
        // Interface
        protected INachoContactChooserDelegate owner;
        protected NcEmailAddress address;
        protected McAccount account;
        protected string initialSearchString;
        // Internal state
        ContactsTableViewSource contactTableViewSource;

        UIBarButtonItem cancelButton;
        UIBarButtonItem searchButton;

        public ContactSearchViewController (IntPtr handle) : base (handle)
        {
        }

        public void SetOwner (INachoContactChooserDelegate owner, McAccount account, NcEmailAddress address, NachoContactType type)
        {
            this.owner = owner;
            this.account = account;
            this.address = address;
            this.initialSearchString = "";
        }

        public void Cleanup ()
        {
            this.owner = null;
            this.contactTableViewSource.Dispose ();
            this.contactTableViewSource = null;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Manages the search bar & auto-complete table.
            contactTableViewSource = new ContactsTableViewSource ();
            contactTableViewSource.SetOwner (this, account, false, SearchDisplayController);

            cancelButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (cancelButton, "icn-close");
            cancelButton.AccessibilityLabel = "Cancel";
            NavigationItem.LeftBarButtonItem = cancelButton;

            cancelButton.Clicked += (sender, e) => {
                owner = null;
                NavigationController.PopViewController (true); 
            };

            searchButton = new NcUIBarButtonItem (UIBarButtonSystemItem.Search);
            searchButton.AccessibilityLabel = "Search";
            searchButton.TintColor = A.Color_NachoBlue;
            NavigationItem.RightBarButtonItem = searchButton;
            searchButton.Clicked += (object sender, EventArgs e) => {
                SearchDisplayController.SearchBar.BecomeFirstResponder ();
            };

            TableView.Source = contactTableViewSource;
            SearchDisplayController.SearchResultsTableView.Source = contactTableViewSource;

            if ((null != initialSearchString) && (0 != initialSearchString.Length)) {
                SearchDisplayController.SearchBar.Text = initialSearchString;
            }
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

        public override bool HidesBottomBarWhenPushed {
            get {
                return this.NavigationController.TopViewController == this;
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_ContactSetChanged == s.Status.SubKind) {
                LoadContacts ();
            }
            if (NcResult.SubKindEnum.Info_ContactSearchCommandSucceeded == s.Status.SubKind) {
                LoadContacts ();
                var sb = SearchDisplayController.SearchBar;
                if (contactTableViewSource.UpdateSearchResults (sb.SelectedScopeButtonIndex, sb.Text, false)) {
                    SearchDisplayController.SearchResultsTableView.ReloadData ();
                }
            }
            if (NcResult.SubKindEnum.Info_ContactLocalSearchComplete == s.Status.SubKind) {
                SearchDisplayController.SearchResultsTableView.ReloadData ();
            }
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);

            PermissionManager.DealWithContactsPermission ();
        }

        protected void LoadContacts ()
        {
            NachoCore.Utils.NcAbate.HighPriority ("ContactSearchViewController LoadContacts");
            var contacts = McContact.AllContactsSortedByName (true);
            contactTableViewSource.SetContacts (null, contacts, false);
            TableView.ReloadData ();
            NachoCore.Utils.NcAbate.RegularPriority ("ContactSearchViewController LoadContacts");
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("ContactsToContactDetail")) {
                var h = sender as SegueHolder;
                var c = (McContact)h.value;
                ContactDetailViewController destinationController = (ContactDetailViewController)segue.DestinationViewController;
                destinationController.contact = c;
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
            address.contact = contact;
            address.address = contact.GetEmailAddress ();
            owner.UpdateEmailAddress (this, address);
            if (null != owner) {
                owner.DismissINachoContactChooser (this);
            }
        }

        /// IContactsTableViewSourceDelegate
        public void EmailSwipeHandler (McContact contact)
        {
            NcAssert.CaseError ();
        }

        /// IContactsTableViewSourceDelegate
        public void CallSwipeHandler (McContact contact)
        {
            NcAssert.CaseError ();
        }
 
    }
}

