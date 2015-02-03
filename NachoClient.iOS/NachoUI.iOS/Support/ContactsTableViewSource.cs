﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;
using System.Text.RegularExpressions;

namespace NachoClient.iOS
{
    public class ContactsTableViewSource : UITableViewSource
    {
        bool multipleSections;
        int[] sectionStart;
        int[] sectionLength;

        bool allowSwiping;

        List<NcContactIndex> recent;
        List<NcContactIndex> contacts;
        List<McContactEmailAddressAttribute> searchResults = null;
        IContactsTableViewSourceDelegate owner;

        UISearchDisplayController SearchDisplayController;

        protected const string UICellReuseIdentifier = "UICell";
        protected const string ContactCellReuseIdentifier = "ContactCell";

        protected string searchToken;
        McAccount account;

        public ContactsTableViewSource ()
        {
            owner = null;
            allowSwiping = false;
        }

        public void SetOwner (IContactsTableViewSourceDelegate owner, McAccount account, bool allowSwiping, UISearchDisplayController SearchDisplayController)
        {
            this.owner = owner;
            this.account = account;
            this.allowSwiping = allowSwiping;
            this.SearchDisplayController = SearchDisplayController;
            SearchDisplayController.Delegate = new SearchDisplayDelegate (this);
        }

        protected void FindRange (char uppercaseTarget, out int index, out int count)
        {
            int hit = -1;

            for (int i = 0; i < contacts.Count; i++) {
                var c = contacts [i];
                if (uppercaseTarget <= c.FirstLetter [0]) {
                    hit = i;
                    break;
                }
            }

            if (-1 == hit) {
                count = 0;
                index = contacts.Count;
                return;
            }

            index = hit;
            count = 0;
            while ((hit < contacts.Count) && (uppercaseTarget == contacts [hit].FirstLetter [0])) {
                count = count + 1;
                hit = hit + 1;
            }
        }

        public void SetContacts (List<NcContactIndex> recent, List<NcContactIndex> contacts, bool multipleSections)
        {
            this.recent = recent;
            this.contacts = contacts;
            this.multipleSections = multipleSections;

            foreach (var c in contacts) {
                if (String.IsNullOrEmpty (c.FirstLetter)) {
                    c.FirstLetter = " ";
                } else {
                    c.FirstLetter = c.FirstLetter.ToUpper ();
                }
            }

            sectionStart = new int[27];
            sectionLength = new int[27];

            int index;
            int count;
            FindRange ('A', out index, out count);

            sectionStart [26] = 0;
            sectionLength [26] = index;

            sectionStart [0] = index;
            sectionLength [0] = count;

            for (int i = 1; i < 26; i++) {
                FindRange ((char)(((int)'A') + i), out index, out count);
                sectionStart [i] = index;
                sectionLength [i] = count;
            }
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
            int n = (null == recent ? 0 : recent.Count) + (null == contacts ? 0 : contacts.Count);
            return (0 == n);
        }

        public new void Dispose ()
        {
            base.Dispose ();
            if (null != searchToken) {
                BackEnd.Instance.Cancel (account.Id, searchToken);
                searchToken = null;
            }
        }

        /// <summary>
        /// Tableview delegate
        /// </summary>
        public override int NumberOfSections (UITableView tableView)
        {
            if (SearchDisplayController.SearchResultsTableView == tableView) {
                return 1;
            }
            return ((null == recent) ? 0 : 1) + (multipleSections ? 27 : 1);
        }

        public override float GetHeightForHeader (UITableView tableView, int section)
        {
            if (SearchDisplayController.SearchResultsTableView == tableView) {
                return 0;
            }
            if (multipleSections || (null != recent)) {
                return 32;
            } else {
                return 0;
            }
        }

        public override UIView GetViewForHeader (UITableView tableView, int section)
        {
            if (!multipleSections && (null == recent)) {
                return new UIView (new RectangleF (0, 0, 0, 0));
            }
            if (SearchDisplayController.SearchResultsTableView == tableView) {
                return new UIView (new RectangleF (0, 0, 0, 0));
            }
//            if ((null != recent) && (0 == section)) {
//                using(var image = UIImage.FromBundle("contacts-recent")) {
//                    var imageView = new UIImageView(image);
//                    imageView.AutoresizingMask = UIViewAutoresizing.FlexibleRightMargin | UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin;
//                    imageView.Center = new PointF (15 + (imageView.Frame.Width / 2), 16 + (imageView.Frame.Height / 2));
//                    var viewX = new UIView (new RectangleF (0, 0, tableView.Frame.Width, 32));
//                    viewX.AddSubview(imageView);
//                    return viewX;
//                }            
//            }
            var view = new UIView (new RectangleF (0, 0, tableView.Frame.Width, 32));
            var label = new UILabel ();
            label.Font = A.Font_AvenirNextDemiBold17;
            label.Text = TitleForHeader (tableView, section);
            label.SizeToFit ();
            label.Center = new PointF (15 + (label.Frame.Width / 2), 10);
            view.AddSubview (label);
            return view;
        }

        public override string TitleForHeader (UITableView tableView, int section)
        {
            String header = "ABCDEFGHIJKLMNOPQRSTUVWXYZ#";

            var n = section;
            if (null != recent) {
                if (0 == section) {
                    return "Recent";
                }
                n = n - 1;
            }
            return header.Substring (n, 1);
        }

        /// <summary>
        /// The number of rows in the specified section.
        /// </summary>
        public override int RowsInSection (UITableView tableview, int section)
        {
            int rows;

            if (SearchDisplayController.SearchResultsTableView == tableview) {
                rows = ((null == searchResults) ? 0 : searchResults.Count);
            } else if ((null != recent) && (0 == section)) {
                rows = recent.Count;
            } else if (multipleSections) {
                var index = section - ((null == recent) ? 0 : 1);
                rows = sectionLength [index];
            } else {
                rows = ((null == contacts) ? 0 : contacts.Count);
            }
            return rows;
        }

        protected McContact ContactFromIndexPath (UITableView tableView, NSIndexPath indexPath)
        {
            McContact contact;

            if (SearchDisplayController.SearchResultsTableView == tableView) {
                var contactEmailAttribute = searchResults [indexPath.Row];
                contact = contactEmailAttribute.GetContact ();
            } else if ((null != recent) && (0 == indexPath.Section)) {
                contact = recent [indexPath.Row].GetContact ();
            } else if (multipleSections) {
                var section = indexPath.Section - ((null == recent) ? 0 : 1);
                var index = indexPath.Row + sectionStart [section];
                contact = contacts [index].GetContact ();
            } else {
                contact = contacts [indexPath.Row].GetContact ();
            }
            return contact;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            McContact contact = ContactFromIndexPath (tableView, indexPath);
            owner.ContactSelectedCallback (contact);
            DumpInfo (contact);
            tableView.DeselectRow (indexPath, true);
        }

        public override void AccessoryButtonTapped (UITableView tableView, NSIndexPath indexPath)
        {
            McContact contact = ContactFromIndexPath (tableView, indexPath);
            owner.ContactSelectedCallback (contact);
        }

        protected void SwipedQuickMessage (string address)
        {
            Log.Info (Log.LOG_UI, "Swiped Quick Message");

            if (string.IsNullOrEmpty (address)) {
                Util.ComplainAbout ("No email address", "You've selected a contact who does not have an email address");
                return;
            }
            owner.PerformSegueForDelegate ("ContactsToQuickMessageCompose", new SegueHolder (address));
        }

        protected void SwipedEmail (string address)
        {
            Log.Info (Log.LOG_UI, "Swiped Email Compose");

            if (string.IsNullOrEmpty (address)) {
                Util.ComplainAbout ("No email address", "You've selected a contact who does not have an email address");
                return;
            }
            owner.PerformSegueForDelegate ("ContactsToMessageCompose", new SegueHolder (address));
        }

        protected void SwipedCall (string number)
        {
            Log.Info (Log.LOG_UI, "Swiped Call");

            if (string.IsNullOrEmpty (number)) {
                Util.ComplainAbout ("No phone number", "You've selected a contact who does not have a phone number");
                return;
            }
            Util.PerformAction ("tel", number);
        }

        protected void SwipedSMS (string number)
        {
            Log.Info (Log.LOG_UI, "Swiped SMS");

            if (string.IsNullOrEmpty (number)) {
                Util.ComplainAbout ("No phone number", "You've selected a contact who does not have a phone number");
                return;
            }
            Util.PerformAction ("sms", number);
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
            return ContactCell.ROW_HEIGHT;
        }
            
        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = null;
            cell = tableView.DequeueReusableCell (ContactCellReuseIdentifier);
            if (null == cell) {
                cell = ContactCell.CreateCell (tableView, VipButtonTouched);
            }
            var contact = ContactFromIndexPath (tableView, indexPath);
            ContactCell.ConfigureCell (tableView, cell, contact, owner, allowSwiping);
            return cell;
        }

        protected void VipButtonTouched (object sender, EventArgs e)
        {
            UIButton vipButton = (UIButton)sender;
            UITableViewCell containingCell = Util.FindEnclosingTableViewCell (vipButton);
            UITableView containingTable = Util.FindEnclosingTableView (vipButton);
            NSIndexPath cellIndexPath = containingTable.IndexPathForCell (containingCell);
            McContact c = ContactFromIndexPath (containingTable, cellIndexPath);

            c.SetVIP (!c.IsVip);
            using (var image = UIImage.FromBundle (c.IsVip ? "contacts-vip-checked" : "contacts-vip")) {
                vipButton.SetImage (image, UIControlState.Normal);
            }
        }

        public void ScrollToSectionIncludingRecent (UITableView tableView, int index)
        {
            if (null == recent) {
                index -= 1; // Recent index will become -1
            }
            if (0 <= index) {
                var p = NSIndexPath.FromItemSection (NSRange.NotFound, index);
                tableView.ScrollToRow (p, UITableViewScrollPosition.Top, true);
            }
        }

        public override void DraggingStarted (UIScrollView scrollView)
        {
            NachoCore.Utils.NcAbate.HighPriority ("ContactsTableView DraggingStarted");
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            NachoCore.Utils.NcAbate.RegularPriority ("ContactsTableView DecelerationEnded");
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            if (!willDecelerate) {
                NachoCore.Utils.NcAbate.RegularPriority ("ContactsTableView DraggingEnded");
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
        /// <param name="doGalSearch">True if it should issue a GAL search as well</param>.
        public bool UpdateSearchResults (int forSearchOption, string forSearchString, bool doGalSearch = true)
        {
            NachoCore.Utils.NcAbate.HighPriority ("ContactTableViewSource UpdateSearchResults");
            if ((null != account) && doGalSearch) {
                // Issue a GAL search. The status indication handler will update the search results
                // (with doGalSearch = false) to reflect potential matches from GAL.
                if (String.IsNullOrEmpty (searchToken)) {
                    searchToken = BackEnd.Instance.StartSearchContactsReq (account.Id, forSearchString, null);
                } else {
                    BackEnd.Instance.SearchContactsReq (account.Id, forSearchString, null, searchToken);
                }
            }
            // We immeidately display matches from our db.
            var results = McContact.SearchAllContactsWithEmailAddresses (forSearchString, true);
            SetSearchResults (results);
            NachoCore.Utils.NcAbate.RegularPriority ("ContactTableViewSource UpdateSearchResults");
            return true;
        }

        protected void DumpInfo (McContact contact)
        {
            if (null == contact) {
                Log.Debug (Log.LOG_UI, "contact is null");
                return;
            }
            foreach (var a in contact.EmailAddresses) {
                var e = McEmailAddress.QueryById<McEmailAddress> (a.EmailAddress);
                if (null != e) {
                    Log.Debug (Log.LOG_UI, "contact Id={0} emailAddressId={1} email={2} score={3}", contact.Id, e.Id, e.CanonicalEmailAddress, e.Score);
                }
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

