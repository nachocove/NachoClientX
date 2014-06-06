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
    public partial class ContactSearchViewController : NcUITableViewController
    {
        // Interface
        public ContactChooserViewController owner;
        public string initialSearchString;
        // Internal state
        public bool UseDeviceContacts;
        INachoContacts contacts;
        List<McContactStringAttribute> searchResults = null;
        /// <summary>
        ///  Must match the id in the prototype cell.
        /// </summary>
        static readonly NSString CellSegueID = new NSString ("ContactsToContact");

        public ContactSearchViewController (IntPtr handle) : base (handle)
        {
        }

        /// <summary>
        /// Setup the search bar & auto-complete handler.
        /// Setup the navigation hooks for the sidebar controller.
        /// Request permission for the device address book (really, here?)
        /// Tables cells and search cells both trigger segues to a detail page.
        /// </summary>
        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            TableView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;
            TableView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height);

            // Manages the search bar & auto-complete table.
            SearchDisplayController.Delegate = new SearchDisplayDelegate (this);

            contacts = NcContactManager.Instance.GetNachoContacts ();
            TableView.ReloadData ();

            // Let's be ready to search!
            SearchDisplayController.Active = true;
            SearchDisplayController.SearchBar.BecomeFirstResponder ();

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
        }

        /// <summary>
        /// Prepares for segue.
        /// </summary>
        /// <param name="segue">Segue in charge</param>
        /// <param name="sender">Typically the cell that was clicked.</param>
        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            // The "+" button segues with ContactsToNewContact
            // Cells segue with CellSegueID, ContactsToContact
            if (segue.Identifier.Equals (CellSegueID)) {
                McContact contact;
                UITableViewCell cell = (UITableViewCell)sender;
                NSIndexPath indexPath = SearchDisplayController.SearchResultsTableView.IndexPathForCell (cell);
                if (null != indexPath) {
                    contact = searchResults.ElementAt (indexPath.Row).GetContact ();
                } else {
                    indexPath = TableView.IndexPathForCell (cell);
                    contact = contacts.GetContactIndex (indexPath.Row).GetContact ();
                }
                ContactViewController destinationController = (ContactViewController)segue.DestinationViewController;
                destinationController.contact = contact;
                destinationController.Title = contact.DisplayName;
            }
        }

        public override int NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override int RowsInSection (UITableView tableview, int section)
        {
            if (SearchDisplayController.SearchResultsTableView == tableview) {
                return ((null == searchResults) ? 0 : searchResults.Count ());
            }
            return ((null == contacts) ? 0 : contacts.Count ());
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            // Hey!  This next bit is different from the normal implementation
            // of GetCell. This doesn't use the tableView parameter to get the
            // cell.  Instead, the main table is always used.  This provides a
            // cell that's hooked up to the segue and has matching attributes.
            UITableViewCell cell = TableView.DequeueReusableCell ("Subtitle");
            // Should always get a prototype cell
            NcAssert.True (null != cell);

            McContact contact;
            if (SearchDisplayController.SearchResultsTableView == tableView) {
                contact = searchResults.ElementAt (indexPath.Row).GetContact ();
            } else {
                contact = contacts.GetContactIndex (indexPath.Row).GetContact ();
            }

            cell.TextLabel.Text = contact.DisplayName;
            cell.DetailTextLabel.Text = contact.DisplayEmailAddress;

            return cell;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
//            base.RowSelected (tableView, indexPath);

            McContact contact;
            if (SearchDisplayController.SearchResultsTableView == tableView) {
                contact = searchResults.ElementAt (indexPath.Row).GetContact ();
            } else {
                contact = contacts.GetContactIndex (indexPath.Row).GetContact ();
            }
            owner.DoublePop (this, contact);
        }

        /// <summary>
        /// Updates the search results.
        /// Return false if an asynch update is triggers.
        /// For async, the table and view should be updated in UpdateSearchResultsCallback.  
        /// </summary>
        /// <returns><c>true</c>, if search results are updated, <c>false</c> otherwise.</returns>
        /// <param name="forSearchOption">Index of the selected tab.</param>
        /// <param name="forSearchString">The prefix string to search for.</param>
        public bool UpdateSearchResults (int forSearchOption, string forSearchString)
        {
            var account = NcModel.Instance.Db.Table<McAccount> ().First ();
            searchResults = McContact.SearchAllContactItems (account.Id, forSearchString);
            return true;
        }

        protected bool StartsWithIgnoringNull (string prefix, string target)
        {
            NcAssert.True (null != prefix);
            // Can't match a field that doesn't exist
            if (null == target) {
                return false;
            }
            // TODO: Verify that we really want InvariantCultureIgnoreCase
            return target.StartsWith (prefix, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Updates the search results async.
        /// </summary>
        public void UpdateSearchResultsCallback ()
        {
            // Totally a dummy routines that exists to remind us how to trigger 
            // the update after updating the searchResult list of contacts.
            if (null != SearchDisplayController.SearchResultsTableView) {
                SearchDisplayController.SearchResultsTableView.ReloadData ();
            }
        }

        public class SearchDisplayDelegate : UISearchDisplayDelegate
        {
            ContactSearchViewController v;

            private SearchDisplayDelegate ()
            {
            }

            public SearchDisplayDelegate (ContactSearchViewController owner)
            {
                v = owner;
            }

            public override bool ShouldReloadForSearchScope (UISearchDisplayController controller, int forSearchOption)
            {
                // TODO: Trigger asynch search & return false
                string searchString = controller.SearchBar.Text;
                return v.UpdateSearchResults (forSearchOption, searchString);
            }

            public override bool ShouldReloadForSearchString (UISearchDisplayController controller, string forSearchString)
            {
                // TODO: Trigger asynch search & return false
                int searchOption = controller.SearchBar.SelectedScopeButtonIndex;
                return v.UpdateSearchResults (searchOption, forSearchString);
            }
        }
    }
}

