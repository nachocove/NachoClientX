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
            owner.ContactSelectedCallback (contact);
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

        public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            return 69;
        }

        protected const int USER_NAME_TAG = 333;
        protected const int USER_LABEL_TAG = 334;
        protected const int USER_EMAIL_TAG = 335;

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            McContact contact;
            if (SearchDisplayController.SearchResultsTableView == tableView) {
                contact = searchResults.ElementAt (indexPath.Row).GetContact ();
            } else {
                contact = contacts [indexPath.Row].GetContact ();
            }

            var cell = CreateCell ();
            ConfigureCell (cell, contact);

            return cell;
        }

        public MCSwipeTableViewCell CreateCell()
        {
            var cell = new MCSwipeTableViewCell (UITableViewCellStyle.Subtitle, ContactCellReuseIdentifier);
            NcAssert.True (null != cell);

            if (null == cell.ViewWithTag (USER_NAME_TAG)) {
                var userNameView = new UILabel (new RectangleF (65, 17, 320 - 15 - 65, 20));
                userNameView.LineBreakMode = UILineBreakMode.TailTruncation;
                userNameView.Tag = USER_NAME_TAG;
                cell.ContentView.AddSubview (userNameView);
                var userEmailView = new UILabel (new RectangleF (65, 35, 320 - 15 - 65, 20));
                userEmailView.LineBreakMode = UILineBreakMode.TailTruncation;
                userEmailView.Tag = USER_EMAIL_TAG;
                cell.ContentView.AddSubview (userEmailView);
                // User userLabelView view, if no image
                var userLabelView = new UILabel (new RectangleF (15, 15, 40, 40));
                userLabelView.Font = A.Font_AvenirNextRegular24;
                userLabelView.TextColor = UIColor.White;
                userLabelView.TextAlignment = UITextAlignment.Center;
                userLabelView.LineBreakMode = UILineBreakMode.Clip;
                userLabelView.Layer.CornerRadius = 20;
                userLabelView.Layer.MasksToBounds = true;
                userLabelView.Tag = USER_LABEL_TAG;
                cell.ContentView.AddSubview (userLabelView);
            }
            return cell;
        }

        public void ConfigureCell (MCSwipeTableViewCell cell, McContact contact)
        {
            var displayName = contact.GetDisplayName ();
            var displayEmailAddress = contact.GetEmailAddress ();

            int colorIndex = 1;

            if (!String.IsNullOrEmpty (displayEmailAddress)) {
                McEmailAddress emailAddress;
                if (McEmailAddress.Get (contact.AccountId, displayEmailAddress, out emailAddress)) {
                    displayEmailAddress = emailAddress.CanonicalEmailAddress;
                    colorIndex = emailAddress.ColorIndex;
                }
            }

            if (1 == colorIndex) {
                colorIndex = contact.CircleColor;
            }

            ConfigureSwipes (cell, contact.Id);

            cell.TextLabel.Text = null;
            cell.DetailTextLabel.Text = null;

            var TextLabel = cell.ViewWithTag (USER_NAME_TAG) as UILabel;
            var DetailTextLabel = cell.ViewWithTag (USER_EMAIL_TAG) as UILabel;
            var labelView = cell.ViewWithTag (USER_LABEL_TAG) as UILabel;

            // Both empty
            if (String.IsNullOrEmpty (displayName) && String.IsNullOrEmpty (displayEmailAddress)) {
                TextLabel.Text = "Contact has no name or email address";
                TextLabel.TextColor = UIColor.LightGray;
                TextLabel.Font = A.Font_AvenirNextRegular14;
                labelView.Hidden = true;
                return;
            }

            // Name empty
            if (String.IsNullOrEmpty (displayName)) {
                TextLabel.Text = displayEmailAddress;
                TextLabel.TextColor = A.Color_0B3239;
                TextLabel.Font = A.Font_AvenirNextDemiBold17;
                labelView.Hidden = false;
                labelView.Text = Util.NameToLetters (displayEmailAddress);
                labelView.BackgroundColor = Util.ColorForUser (colorIndex);
                return;
            }

            // Email empty
            if (String.IsNullOrEmpty (displayEmailAddress)) {
                TextLabel.Text = displayName;
                DetailTextLabel.Text = "Contact has no email address";
                TextLabel.TextColor = A.Color_0B3239;
                TextLabel.Font = A.Font_AvenirNextDemiBold17;
                DetailTextLabel.TextColor = UIColor.LightGray;
                DetailTextLabel.Font = A.Font_AvenirNextRegular12;
                labelView.Hidden = false;
                labelView.Text = Util.NameToLetters (displayName);
                labelView.BackgroundColor = Util.ColorForUser (colorIndex);
                return;
            }

            // Everything
            TextLabel.Text = displayName;
            DetailTextLabel.Text = displayEmailAddress;
            TextLabel.TextColor = A.Color_0B3239;
            TextLabel.Font = A.Font_AvenirNextDemiBold17;
            DetailTextLabel.TextColor = A.Color_0B3239;
            DetailTextLabel.Font = A.Font_AvenirNextRegular14;

            labelView.Hidden = false;
            labelView.Text = Util.NameToLetters (displayName);
            labelView.BackgroundColor = Util.ColorForUser (colorIndex);
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

