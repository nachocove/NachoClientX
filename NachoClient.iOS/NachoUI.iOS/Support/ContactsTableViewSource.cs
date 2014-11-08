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
        protected const float HORIZONTAL_INDENT = 65;

        protected const int CALL_SWIPE_TAG = 100;
        protected const int EMAIL_SWIPE_TAG = 101;
        protected const int SWIPE_VIEW_TAG = 102;

        protected const float ROW_HEIGHT = 80;

        private static SwipeActionDescriptor CALL_BUTTON =
            new SwipeActionDescriptor (CALL_SWIPE_TAG, 0.25f, UIImage.FromBundle ("email-putintofolder-gray"),
                "Dial", A.Color_NachoSwipeActionGreen);
        private static SwipeActionDescriptor EMAIL_BUTTON =
            new SwipeActionDescriptor (EMAIL_SWIPE_TAG, 0.25f, UIImage.FromBundle ("email-putintofolder-gray"),
                "Email", A.Color_NachoSwipeActionRed);

        bool multipleSections;
        int[] sectionStart;
        int[] sectionLength;

        List<NcContactIndex> recent;
        List<NcContactIndex> contacts;
        List<McContactEmailAddressAttribute> searchResults = null;
        IContactsTableViewSourceDelegate owner;

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
                    return "Recents";
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
            Console.WriteLine ("RowsInSection {0} = {1}", section, rows);
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
            return ROW_HEIGHT;
        }

        protected const int TITLE_LABEL_TAG = 333;
        protected const int USER_LABEL_TAG = 334;
        protected const int SUBTITLE1_LABEL_TAG = 335;
        protected const int SUBTITLE2_LABEL_TAG = 336;

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            McContact contact = ContactFromIndexPath (tableView, indexPath);

            UITableViewCell cell = null;
            cell = tableView.DequeueReusableCell (ContactCellReuseIdentifier);
            if (cell == null) {
                cell = CreateCell (contact, tableView);
            }
            NcAssert.True (null != cell);

            ConfigureCell (cell, contact);
            return cell;
        }

        public UITableViewCell CreateCell (McContact contact, UITableView tableView)
        {
            var cell = tableView.DequeueReusableCell (ContactCellReuseIdentifier);
            if (null == cell) {
                cell = new UITableViewCell (UITableViewCellStyle.Subtitle, ContactCellReuseIdentifier);
            }

            cell.Layer.CornerRadius = 15;
            cell.Layer.MasksToBounds = true;
            cell.SelectionStyle = UITableViewCellSelectionStyle.None;

            NcAssert.True (null != cell);
            NcAssert.True (null == cell.ViewWithTag (TITLE_LABEL_TAG));

            var view = new SwipeActionView (new RectangleF (0, 0, cell.ContentView.Frame.Width, ROW_HEIGHT));
            view.BackgroundColor = UIColor.White;
            view.Tag = SWIPE_VIEW_TAG;

            view.SetAction (CALL_BUTTON, SwipeSide.RIGHT);
            view.SetAction (EMAIL_BUTTON, SwipeSide.RIGHT);

            view.OnClick = (int tag) => {
                switch (tag) {
                case EMAIL_SWIPE_TAG:
                    EmailSwipeHandler (contact);
                    break;
                case CALL_SWIPE_TAG:
                    CallSwipeHandler (contact);
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action tag {0}", tag));
                }
            };

            view.OnSwipe = (SwipeActionView.SwipeState state) => {
                switch (state) {
                case SwipeActionView.SwipeState.SWIPE_BEGIN:
                    tableView.ScrollEnabled = false;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_HIDDEN:
                    tableView.ScrollEnabled = true;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_SHOWN:
                    tableView.ScrollEnabled = false;
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown swipe state {0}", (int)state));
                }
            };
            cell.ContentView.AddSubview (view);

            UIButton toggleVipButton = new UIButton (new RectangleF(cell.ContentView.Frame.Right - 30, 10, 20, 20));
            toggleVipButton.SetImage (UIImage.FromBundle(contact.IsVip ? "contacts-vip-checked" : "contacts-vip"), UIControlState.Normal); 
            toggleVipButton.TouchUpInside += (object sender, EventArgs e) => {
                contact.SetVIP (!contact.IsVip);
                toggleVipButton.SetImage (UIImage.FromBundle(contact.IsVip ? "contacts-vip-checked" : "contacts-vip"), UIControlState.Normal);
            };
            view.AddSubview (toggleVipButton);

            var titleLabel = new UILabel (new RectangleF (HORIZONTAL_INDENT, 10, cell.Frame.Width - 15 - HORIZONTAL_INDENT - toggleVipButton.Frame.Width - 8, 20));
            titleLabel.TextColor = A.Color_NachoGreen;
            titleLabel.Font = A.Font_AvenirNextDemiBold17;
            titleLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            titleLabel.Tag = TITLE_LABEL_TAG;
            view.AddSubview (titleLabel);

            var subtitle1Label = new UILabel (new RectangleF (HORIZONTAL_INDENT, 35, cell.Frame.Width - 15 - HORIZONTAL_INDENT, 20));
            subtitle1Label.LineBreakMode = UILineBreakMode.TailTruncation;
            subtitle1Label.Font = A.Font_AvenirNextRegular14;
            subtitle1Label.Tag = SUBTITLE1_LABEL_TAG;
            view.AddSubview (subtitle1Label);

            var subtitle2Label = new UILabel (new RectangleF (HORIZONTAL_INDENT, 55, cell.Frame.Width - 15 - HORIZONTAL_INDENT, 20));
            subtitle2Label.LineBreakMode = UILineBreakMode.TailTruncation;
            subtitle2Label.Font = A.Font_AvenirNextRegular14;
            subtitle2Label.Tag = SUBTITLE2_LABEL_TAG;
            view.AddSubview (subtitle2Label);

            // User userLabelView view, if no image
            if (0 == contact.PortraitId) {
                var userLabelView = new UILabel (new RectangleF (15, 10, 40, 40));
                userLabelView.Font = A.Font_AvenirNextRegular24;
                userLabelView.TextColor = UIColor.White;
                userLabelView.TextAlignment = UITextAlignment.Center;
                userLabelView.LineBreakMode = UILineBreakMode.Clip;
                userLabelView.Layer.CornerRadius = 20;
                userLabelView.Layer.MasksToBounds = true;
                userLabelView.Tag = USER_LABEL_TAG;
                view.AddSubview (userLabelView);
            } else {
                var userImageView = new UIImageView (new RectangleF (15, 10, 40, 40));
                userImageView.Layer.CornerRadius = 20;
                userImageView.Layer.MasksToBounds = true;
                userImageView.Tag = USER_LABEL_TAG;
                view.AddSubview (userImageView);
            }
            return cell;
        }

        protected void CallSwipeHandler (McContact contact)
        {
            SwipedCall (contact.GetPhoneNumber());
            return;
//            if (contact.PhoneNumbers.Count == 0) {
//                //TODO: Display a 'enter phone number' for the contact. 
//                SwipedCall (contact.GetPhoneNumber());
//                return;
//            }
//
//            if (contact.PhoneNumbers.Count == 1) {
//                if (!contact.PhoneNumbers.FirstOrDefault ().IsDefault) {
//                    contact.PhoneNumbers.FirstOrDefault ().IsDefault = true;
//                    contact.Update ();
//                }
//                SwipedCall (contact.GetPhoneNumber());
//                return;
//            }
//
//            if (contact.PhoneNumbers.Count > 1) {
//                foreach (var e in contact.PhoneNumbers) {
//                    if (e.IsDefault) {
//                        SwipedCall (e.Value);
//                        return;
//                    }
//                }
//                //TODO: Display a 'default-phone chooser' for the contact
//                contact.PhoneNumbers.FirstOrDefault ().IsDefault = true;
//                contact.Update ();
//                SwipedCall (contact.GetPhoneNumber());
//                return;
//            }
        }

        protected void EmailSwipeHandler (McContact contact)
        {
            SwipedEmail (contact.GetEmailAddress());
            return;

//            if (contact.EmailAddresses.Count == 0) {
//                //TODO: Display a 'enter email address' for the contact. 
//                SwipedEmail (contact.GetEmailAddress());
//                return;
//            }
//
//            if (contact.EmailAddresses.Count == 1) {
//                if (!contact.EmailAddresses.FirstOrDefault ().IsDefault) {
//                    contact.EmailAddresses.FirstOrDefault ().IsDefault = true;
//                    contact.Update ();
//                }
//                SwipedEmail (contact.GetEmailAddress());
//                return;
//            }
//
//            if (contact.EmailAddresses.Count > 1) {
//                foreach (var e in contact.EmailAddresses) {
//                    if (e.IsDefault) {
//                        SwipedEmail (e.Value);
//                        return;
//                    }
//                }
//                //TODO: Display a 'default-email chooser' for the contact
//                contact.EmailAddresses.FirstOrDefault ().IsDefault = true;
//                contact.Update ();
//                SwipedEmail (contact.GetEmailAddress());
//                return;
//            }
        }

        public void ConfigureCell (UITableViewCell cell, McContact contact)
        {
            var titleLabel = cell.ViewWithTag (TITLE_LABEL_TAG) as UILabel;
            var subtitle1Label = cell.ViewWithTag (SUBTITLE1_LABEL_TAG) as UILabel;
            var subtitle2Label = cell.ViewWithTag (SUBTITLE2_LABEL_TAG) as UILabel;
            var labelView = cell.ViewWithTag (USER_LABEL_TAG) as UILabel;

            if (null == contact) {
                titleLabel.Text = "This contact is unavailable";
                titleLabel.TextColor = UIColor.LightGray;
                titleLabel.Font = A.Font_AvenirNextRegular14;
                labelView.Hidden = true;
                return;
            }

            var displayTitle = contact.GetDisplayName ();
            var displayTitleColor = A.Color_NachoDarkText;

            var displaySubtitle1 = contact.GetEmailAddress ();
            var displaySubtitle1Color = A.Color_NachoDarkText;

            var displaySubtitle2 = contact.GetPhoneNumber ();
            var displaySubtitle2Color = A.Color_NachoDarkText;

            int colorIndex = 1;
            if (!String.IsNullOrEmpty (displaySubtitle1)) {
                McEmailAddress emailAddress;
                if (McEmailAddress.Get (contact.AccountId, displaySubtitle1, out emailAddress)) {
                    displaySubtitle1 = emailAddress.CanonicalEmailAddress;
                    colorIndex = emailAddress.ColorIndex;
                }
            }
            if (1 == colorIndex) {
                colorIndex = Util.PickRandomColorForUser ();
            }

            if (String.IsNullOrEmpty (displayTitle) && !String.IsNullOrEmpty (displaySubtitle1)) {
                displayTitle = displaySubtitle1;
                displaySubtitle1 = "No name for this contact";
                displaySubtitle1Color = A.Color_NachoTextGray;
            }

            if (String.IsNullOrEmpty (displayTitle)) {
                displayTitle = "No name for this contact";
                displayTitleColor = A.Color_NachoTextGray;
            }
                
            if (String.IsNullOrEmpty (displaySubtitle1)) {
                displaySubtitle1 = "No email address for this contact";
                displaySubtitle1Color = A.Color_NachoLightText;
            }

            if (String.IsNullOrEmpty (displaySubtitle2)) {
                displaySubtitle2 = "No phone number for this contact";
                displaySubtitle2Color = A.Color_NachoLightText;
            }

            titleLabel.Text = displayTitle;
            titleLabel.TextColor = displayTitleColor;

            subtitle1Label.Text = displaySubtitle1;
            subtitle1Label.TextColor = displaySubtitle1Color;

            subtitle2Label.Text = displaySubtitle2;
            subtitle2Label.TextColor = displaySubtitle2Color;

            if (null != labelView) {
                ConfigureLabelView (labelView, displayTitle, colorIndex);
            } else {
                var imageView = cell.ViewWithTag (USER_LABEL_TAG) as UIImageView;
                imageView.Image = Util.ImageOfContact (contact);
            }
        }

        protected void ConfigureLabelView (UILabel labelView, string labelText, int colorIndex)
        {
            labelView.Hidden = false;
            labelView.Text = Util.NameToLetters (labelText);
            labelView.BackgroundColor = Util.ColorForUser (colorIndex);
        }

        public void ScrollToSection (UITableView tableView, int index)
        {
            if (0 <= index) {
                var p = NSIndexPath.FromItemSection (NSRange.NotFound, index);
                tableView.ScrollToRow (p, UITableViewScrollPosition.Top, true);
            }
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

        protected void DumpInfo (McContact contact)
        {
            foreach (var a in contact.EmailAddresses) {
                var e = McEmailAddress.QueryById<McEmailAddress> (a.EmailAddress);
                Log.Debug (Log.LOG_UI, "contact Id={0} emailAddressId={1} email={2} score={3}", contact.Id, e.Id, e.CanonicalEmailAddress, e.Score);
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

