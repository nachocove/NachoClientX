//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
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
            new SwipeActionDescriptor (CALL_SWIPE_TAG, 0.5f, UIImage.FromBundle ("contacts-call-swipe"),
                "Dial", A.Color_NachoSwipeActionOrange);
        private static SwipeActionDescriptor EMAIL_BUTTON =
            new SwipeActionDescriptor (EMAIL_SWIPE_TAG, 0.5f, UIImage.FromBundle ("contacts-email-swipe"),
                "Email", A.Color_NachoSwipeActionMatteBlack);

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

        public ContactsTableViewSource ()
        {
            owner = null;
            allowSwiping = false;
        }

        public void SetOwner (IContactsTableViewSourceDelegate owner, bool allowSwiping, UISearchDisplayController SearchDisplayController)
        {
            this.owner = owner;
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
        protected const int SET_VIP_TAG = 337;
        protected const int USER_PORTRAIT_TAG = 338;

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = null;
            cell = tableView.DequeueReusableCell (ContactCellReuseIdentifier);
            if (null == cell) {
                cell = CreateCell (tableView);
            }
            var contact = ContactFromIndexPath (tableView, indexPath);
            ConfigureCell (tableView, cell, contact);
            return cell;
        }

        public UITableViewCell CreateCell (UITableView tableView)
        {
            var cell = new UITableViewCell (UITableViewCellStyle.Subtitle, ContactCellReuseIdentifier);

            cell.Layer.CornerRadius = 15;
            cell.Layer.MasksToBounds = true;
            cell.SelectionStyle = UITableViewCellSelectionStyle.None;

            var view = new SwipeActionView (new RectangleF (0, 0, cell.ContentView.Frame.Width, ROW_HEIGHT));
            view.BackgroundColor = UIColor.White;
            view.SetAction (CALL_BUTTON, SwipeSide.LEFT);
            view.SetAction (EMAIL_BUTTON, SwipeSide.RIGHT);
            view.Tag = SWIPE_VIEW_TAG;
            if (!allowSwiping) {
                view.DisableSwipe ();
            }

            cell.ContentView.AddSubview (view);

            UIButton toggleVipButton = new UIButton (new RectangleF (cell.ContentView.Frame.Right - 30, 10, 20, 20));
            toggleVipButton.Tag = SET_VIP_TAG;
            toggleVipButton.TouchUpInside += VipButtonTouched;
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
            var userLabelView = new UILabel (new RectangleF (15, 10, 40, 40));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 20;
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Tag = USER_LABEL_TAG;
            view.AddSubview (userLabelView);

            var userImageView = new UIImageView (new RectangleF (15, 10, 40, 40));
            userImageView.Layer.CornerRadius = 20;
            userImageView.Layer.MasksToBounds = true;
            userImageView.Tag = USER_PORTRAIT_TAG;
            view.AddSubview (userImageView);

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

        protected void CallSwipeHandler (McContact contact)
        {
            if (0 == contact.PhoneNumbers.Count) {
                owner.PerformSegueForDelegate ("SegueToContactDefaultSelection", new SegueHolder (contact, ContactDefaultSelectionViewController.DefaultSelectionType.PhoneNumberAdder));
            } else if (1 == contact.PhoneNumbers.Count) {
                Util.PerformAction ("tel", contact.GetPhoneNumber ());
            } else {
                foreach (var p in contact.PhoneNumbers) {
                    if (p.IsDefault) {
                        Util.PerformAction ("tel", p.Value);
                        return;
                    }
                }
                owner.PerformSegueForDelegate ("SegueToContactDefaultSelection", new SegueHolder (contact, ContactDefaultSelectionViewController.DefaultSelectionType.DefaultPhoneSelector));
            }
        }

        protected void EmailSwipeHandler (McContact contact)
        {
            if (0 == contact.EmailAddresses.Count) {
                owner.PerformSegueForDelegate ("SegueToContactDefaultSelection", new SegueHolder (contact, ContactDefaultSelectionViewController.DefaultSelectionType.EmailAdder));
            } else if (1 == contact.EmailAddresses.Count) {
                owner.PerformSegueForDelegate ("SegueToMessageCompose", new SegueHolder (contact.GetEmailAddress ()));
            } else {
                foreach (var e in contact.EmailAddresses) {
                    if (e.IsDefault) {
                        owner.PerformSegueForDelegate ("SegueToMessageCompose", new SegueHolder (e.Value));
                        return;
                    }
                }
                owner.PerformSegueForDelegate ("SegueToContactDefaultSelection", new SegueHolder (contact, ContactDefaultSelectionViewController.DefaultSelectionType.DefaultEmailSelector));
            }
        }

        public void ConfigureCell (UITableView tableView, UITableViewCell cell, McContact contact)
        {
            var titleLabel = (UILabel)cell.ViewWithTag (TITLE_LABEL_TAG);
            var subtitle1Label = (UILabel)cell.ViewWithTag (SUBTITLE1_LABEL_TAG);
            var subtitle2Label = (UILabel)cell.ViewWithTag (SUBTITLE2_LABEL_TAG);
            var labelView = (UILabel)cell.ViewWithTag (USER_LABEL_TAG);
            var portraitView = (UIImageView)cell.ViewWithTag (USER_PORTRAIT_TAG);

            labelView.Hidden = true;
            portraitView.Hidden = true;

            var view = (SwipeActionView)cell.ViewWithTag (SWIPE_VIEW_TAG);
            view.EnableSwipe (null != contact);

            if (null == contact) {
                titleLabel.Text = "This contact is unavailable";
                titleLabel.TextColor = UIColor.LightGray;
                titleLabel.Font = A.Font_AvenirNextRegular14;
                subtitle1Label.Text = "";
                subtitle2Label.Text = "";
                view.OnSwipe = null;
                view.OnClick = null;
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

            if (0 == contact.PortraitId) {
                ConfigureLabelView (labelView, contact, colorIndex);
                labelView.Hidden = false;
            } else {
                portraitView.Image = Util.ImageOfContact (contact);
                portraitView.Hidden = false;
            }

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
            view.OnSwipe = (SwipeActionView activeView, SwipeActionView.SwipeState state) => {
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

            var toggleVipButton = (UIButton)view.ViewWithTag (SET_VIP_TAG);
            using (var image = UIImage.FromBundle (contact.IsVip ? "contacts-vip-checked" : "contacts-vip")) {
                toggleVipButton.SetImage (image, UIControlState.Normal);
            }
        }

        protected void ConfigureLabelView (UILabel labelView, McContact contact, int colorIndex)
        {
            labelView.Hidden = false;
            labelView.Text = NachoCore.Utils.ContactsHelper.GetInitials (contact);
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
        public bool UpdateSearchResults (int forSearchOption, string forSearchString)
        {
            NachoCore.Utils.NcAbate.HighPriority ("ContactTableViewSource UpdateSearchResults");
            var results = McContact.SearchAllContactItems (forSearchString);
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

