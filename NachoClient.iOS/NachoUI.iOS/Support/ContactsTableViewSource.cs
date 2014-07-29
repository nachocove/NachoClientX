//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections.Generic;
using MCSwipeTableViewCellBinding;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class ContactsTableViewSource : UITableViewSource
    {
        List<NcContactIndex> contacts;
        List<McContactEmailAddressAttribute> searchResults = null;
        public IContactsTableViewSourceDelegate owner;

        UISearchDisplayController SearchDisplayController;

        protected const string UICellReuseIdentifier = "UICell";
        protected const string ContactCellReuseIdentifier = "ContactCell";

        public ContactsTableViewSource ()
        {
            owner = null;
        }

        public void SetOwner (IContactsTableViewSourceDelegate owner, UISearchDisplayController SearchDisplayController)
        {
            this.owner = owner;
            this.SearchDisplayController = SearchDisplayController;

            SearchDisplayController.Delegate = new SearchDisplayDelegate (this);
        }

        public void SetContacts (List<NcContactIndex> contacts)
        {
            this.contacts = contacts;
        }

        public void SetSearchResults (List<McContactEmailAddressAttribute> searchResults)
        {
            this.searchResults = searchResults;
        }

        protected bool NoContacts (UITableView tableView)
        {
            if (SearchDisplayController.SearchResultsTableView == tableView) {
                return ((null == searchResults) || (0 == searchResults.Count));
            }
            return ((null == contacts) || (0 == contacts.Count));        
        }

        /// <summary>
        /// Tableview delegate
        /// </summary>
        public override int NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        /// <summary>
        /// The number of rows in the specified section.
        /// </summary>
        public override int RowsInSection (UITableView tableview, int section)
        {
            if (SearchDisplayController.SearchResultsTableView == tableview) {
                return ((null == searchResults) ? 0 : searchResults.Count);
            }
            return ((null == contacts) ? 0 : contacts.Count);
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            McContact contact;

            if (SearchDisplayController.SearchResultsTableView == tableView) {
                var contactEmailAttribute = searchResults [indexPath.Row];
                contact = McContact.QueryById<McContact> ((int)contactEmailAttribute.ContactId);
            } else {
                contact = contacts [indexPath.Row].GetContact ();
            }
            owner.ContactSelectedCallback (contact);
        }

        public override void AccessoryButtonTapped (UITableView tableView, NSIndexPath indexPath)
        {
            McContact contact;

            if (SearchDisplayController.SearchResultsTableView == tableView) {
                var contactEmailAttribute = searchResults [indexPath.Row];
                contact = McContact.QueryById<McContact> ((int)contactEmailAttribute.ContactId);
            } else {
                contact = contacts [indexPath.Row].GetContact ();
            }

            owner.PerformSegueForDelegate ("ContactsToContactDetail", new SegueHolder (contact));
        }

 
        /// <summary>
        /// Configures the swipes.
        /// </summary>
        void ConfigureSwipes (MCSwipeTableViewCell cell, int calendarIndex)
        {
            cell.FirstTrigger = 0.20f;
            cell.SecondTrigger = 0.50f;

            UIView checkView = null;
            UIColor greenColor = null;
            UIView crossView = null;
            UIColor redColor = null;
            UIView clockView = null;
            UIColor yellowColor = null;
            UIView listView = null;
            UIColor brownColor = null;

            try { 
                checkView = ViewWithImageName ("check");
                greenColor = new UIColor (85.0f / 255.0f, 213.0f / 255.0f, 80.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (checkView, greenColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State1, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
//                    ArchiveThisMessage (calendarIndex);
                });
                crossView = ViewWithImageName ("cross");
                redColor = new UIColor (232.0f / 255.0f, 61.0f / 255.0f, 14.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (crossView, redColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State2, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
//                    DeleteThisMessage (calendarIndex);
                });
                clockView = ViewWithImageName ("clock");
                yellowColor = new UIColor (254.0f / 255.0f, 217.0f / 255.0f, 56.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (clockView, yellowColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State3, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    //                    PerformSegue ("MessageToMessagePriority", new SegueHolder (messageThreadIndex));
                });
                listView = ViewWithImageName ("list");
                brownColor = new UIColor (206.0f / 255.0f, 149.0f / 255.0f, 98.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (listView, brownColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State4, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    //                    PerformSegue ("MessageToMessageAction", new SegueHolder (messageThreadIndex));
                });
            } finally {
                if (null != checkView) {
                    checkView.Dispose ();
                }
                if (null != greenColor) {
                    greenColor.Dispose ();
                }
                if (null != crossView) {
                    crossView.Dispose ();
                }
                if (null != redColor) {
                    redColor.Dispose ();
                }
                if (null != clockView) {
                    clockView.Dispose ();
                }
                if (null != yellowColor) {
                    yellowColor.Dispose ();
                }
                if (null != listView) {
                    listView.Dispose ();
                }
                if (null != brownColor) {
                    brownColor.Dispose ();
                }
            }
        }


        UIView ViewWithImageName (string imageName)
        {
            var image = UIImage.FromBundle (imageName);
            var imageView = new UIImageView (image);
            imageView.ContentMode = UIViewContentMode.Center;
            return imageView;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            McContact contact;
            if (SearchDisplayController.SearchResultsTableView == tableView) {
                contact = searchResults.ElementAt (indexPath.Row).GetContact ();
            } else {
                contact = contacts [indexPath.Row].GetContact ();
            }

            var cell = new MCSwipeTableViewCell (UITableViewCellStyle.Subtitle, ContactCellReuseIdentifier);
            NcAssert.True (null != cell);

            ConfigureCell (cell, contact);

            return cell;
        }

        public void ConfigureCell (MCSwipeTableViewCell cell, McContact contact)
        {
            var displayName = contact.GetDisplayName ();
            var displayEmailAddress = contact.GetEmailAddress ();

            ConfigureSwipes (cell, contact.Id);

            cell.TextLabel.Text = null;
            cell.DetailTextLabel.Text = null;

            cell.Accessory = UITableViewCellAccessory.DetailDisclosureButton;

            // Both empty
            if (String.IsNullOrEmpty (displayName) && String.IsNullOrEmpty (displayEmailAddress)) {
                cell.TextLabel.Text = "Contact has no name or email address";
                cell.TextLabel.TextColor = UIColor.LightGray;
                cell.TextLabel.Font = A.Font_AvenirNextRegular14;
                return;
            }

            // Name empty
            if (String.IsNullOrEmpty (displayName)) {
                cell.TextLabel.Text = displayEmailAddress;
                cell.TextLabel.TextColor = A.Color_NachoBlack;
                cell.TextLabel.Font = A.Font_AvenirNextRegular14;
                return;
            }

            // Email empty
            if (String.IsNullOrEmpty (displayEmailAddress)) {
                cell.TextLabel.Text = displayName;
                cell.DetailTextLabel.Text = "Contact has no email address";
                cell.TextLabel.TextColor = A.Color_NachoBlack;
                cell.TextLabel.Font = A.Font_AvenirNextRegular14;
                cell.DetailTextLabel.TextColor = UIColor.LightGray;
                cell.DetailTextLabel.Font = A.Font_AvenirNextRegular12;
                return;
            }

            // Everything
            cell.TextLabel.Text = displayName;
            cell.DetailTextLabel.Text = displayEmailAddress;
            cell.TextLabel.TextColor = A.Color_NachoBlack;
            cell.TextLabel.Font = A.Font_AvenirNextRegular14;
            cell.DetailTextLabel.TextColor = UIColor.Gray;
            cell.DetailTextLabel.Font = A.Font_AvenirNextRegular12;
        }

        public override void DraggingStarted (UIScrollView scrollView)
        {
            Log.Info (Log.LOG_UI, "DraggingStarted");
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStarted),
                Account = ConstMcAccount.NotAccountSpecific,
            });
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            Log.Info (Log.LOG_UI, "DecelerationEnded");
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStopped),
                Account = ConstMcAccount.NotAccountSpecific,
            });
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            if (!willDecelerate) {
                Log.Info (Log.LOG_UI, "DraggingEnded");
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                    Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStopped),
                    Account = ConstMcAccount.NotAccountSpecific,
                });
            }
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
            new System.Threading.Thread (new System.Threading.ThreadStart (() => {
                NachoClient.Util.HighPriority ();
                var results = McContact.SearchAllContactItems (forSearchString);
                NachoClient.Util.RegularPriority ();
                InvokeOnMainThread (() => {
                    var searchResults = results;
                    SetSearchResults (searchResults);
                    UpdateSearchResultsCallback ();
                });
            })).Start ();

            return false;
        }

        /// <summary>
        /// Updates the search results async.
        /// </summary>
        public void UpdateSearchResultsCallback ()
        {
            // Totally a dummy routines that exists to remind us how to trigger 
            // the update after updating the searchResult list of contacts.
            if (null != SearchDisplayController.SearchResultsTableView) {
                NachoClient.Util.HighPriority ();
                SearchDisplayController.SearchResultsTableView.ReloadData ();
                NachoClient.Util.RegularPriority ();
            }
        }

        public class SearchDisplayDelegate : UISearchDisplayDelegate
        {
            ContactsTableViewSource owner;

            private SearchDisplayDelegate ()
            {
            }

            public SearchDisplayDelegate (ContactsTableViewSource owner)
            {
                this.owner = owner;
            }

            public override bool ShouldReloadForSearchScope (UISearchDisplayController controller, int forSearchOption)
            {
                // TODO: Trigger asynch search & return false
                string searchString = controller.SearchBar.Text;
                return owner.UpdateSearchResults (forSearchOption, searchString);
            }

            public override bool ShouldReloadForSearchString (UISearchDisplayController controller, string forSearchString)
            {
                int searchOption = controller.SearchBar.SelectedScopeButtonIndex;
                return owner.UpdateSearchResults (searchOption, forSearchString);
            }
        }
    }
}

