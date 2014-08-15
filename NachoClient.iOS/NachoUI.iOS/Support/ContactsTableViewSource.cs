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
using System.Text.RegularExpressions;

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

        enum WhichItems
        {
            None,
            Email,
            Name,
            Phone,
            EmailAndName,
            EmailAndPhone,
            NameAndPhone,
            EmailNameAndPhone}

        ;

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
            DumpInfo (contact);
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
        protected const int USER_PHONE_TAG = 336;

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

        public MCSwipeTableViewCell CreateCell ()
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
                var userPhoneView = new UILabel (new RectangleF (65, 56, 320 - 15 - 65, 14));
                userPhoneView.LineBreakMode = UILineBreakMode.TailTruncation;
                userPhoneView.Tag = USER_PHONE_TAG;
                cell.ContentView.AddSubview (userPhoneView);

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
            var displayPhoneNumber = contact.GetPhoneNumber ();

            WhichItems theItems;

            bool hasEmail = false;
            bool hasName = false;
            bool hasPhone = false;

            if (!String.IsNullOrEmpty (displayName)) {
                hasName = true;
            }
            if (!String.IsNullOrEmpty (displayEmailAddress)) {
                hasEmail = true;
            }
            if (!String.IsNullOrEmpty (displayPhoneNumber)) {
                hasPhone = true;
                string phoneNumberNoWhiteSpaces = Regex.Replace (displayPhoneNumber, @"\s+", ""); 
                if (phoneNumberNoWhiteSpaces.ToCharArray ().Length == 10) {
                    displayPhoneNumber = String.Format ("{0:(###) ###-####}", double.Parse (phoneNumberNoWhiteSpaces));
                }
            }

            if (!hasName && !hasPhone && !hasEmail) {
                theItems = WhichItems.None;
            } else if (hasName && hasEmail && hasPhone) {
                theItems = WhichItems.EmailNameAndPhone;
            } else if (hasName && hasEmail) {
                theItems = WhichItems.EmailAndName;
            } else if (hasName && hasPhone) {
                theItems = WhichItems.NameAndPhone;
            } else if (hasEmail && hasPhone) {
                theItems = WhichItems.EmailAndPhone;
            } else if (hasEmail) {
                theItems = WhichItems.Email;
            } else if (hasName) {
                theItems = WhichItems.Name;
            } else {
                theItems = WhichItems.Phone;
            }

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

            var TitleLabel = cell.ViewWithTag (USER_NAME_TAG) as UILabel;
            var SubTitleLabelOne = cell.ViewWithTag (USER_EMAIL_TAG) as UILabel;
            var SubTitleLabelTwo = cell.ViewWithTag (USER_PHONE_TAG) as UILabel;
            var labelView = cell.ViewWithTag (USER_LABEL_TAG) as UILabel;

            switch (theItems) {
            case WhichItems.None:
                TitleLabel.Text = "Contact has no name, email address, or phone";
                TitleLabel.TextColor = UIColor.LightGray;
                TitleLabel.Font = A.Font_AvenirNextRegular14;
                labelView.Hidden = true;
                return;
            case WhichItems.Name:
                TitleLabel.Text = displayName;
                SubTitleLabelOne.Text = "Contact has no email address";
                TitleLabel.TextColor = A.Color_0B3239;
                TitleLabel.Font = A.Font_AvenirNextDemiBold17;
                SubTitleLabelOne.TextColor = UIColor.LightGray;
                SubTitleLabelOne.Font = A.Font_AvenirNextRegular12;
                ConfigureLabelView (labelView, displayName, colorIndex);
                return;
            case WhichItems.Email:
                TitleLabel.Text = displayEmailAddress;
                TitleLabel.TextColor = A.Color_0B3239;
                TitleLabel.Font = A.Font_AvenirNextDemiBold17;
                TitleLabel.Frame = new RectangleF (65, (69 / 2) - 10, 320 - 15 - 65, 20);
                ConfigureLabelView (labelView, displayEmailAddress, colorIndex);
                return;
            case WhichItems.Phone:
                TitleLabel.Text = displayPhoneNumber;
                SubTitleLabelOne.Text = "Contact has no email address";
                TitleLabel.TextColor = A.Color_0B3239;
                TitleLabel.Font = A.Font_AvenirNextDemiBold17;
                SubTitleLabelOne.TextColor = UIColor.LightGray;
                SubTitleLabelOne.Font = A.Font_AvenirNextRegular12;
                ConfigureLabelView (labelView, displayPhoneNumber, colorIndex);
                return;
            case WhichItems.EmailAndName:
                TitleLabel.Text = displayName;
                SubTitleLabelOne.Text = displayEmailAddress;
                TitleLabel.TextColor = A.Color_0B3239;
                TitleLabel.Font = A.Font_AvenirNextDemiBold17;
                SubTitleLabelOne.TextColor = A.Color_0B3239;
                SubTitleLabelOne.Font = A.Font_AvenirNextRegular14;
                ConfigureLabelView (labelView, displayName, colorIndex);
                return;
            case WhichItems.EmailAndPhone:
                TitleLabel.Text = displayEmailAddress;
                SubTitleLabelOne.Text = displayPhoneNumber;
                TitleLabel.TextColor = A.Color_0B3239;
                TitleLabel.Font = A.Font_AvenirNextDemiBold17;
                SubTitleLabelOne.TextColor = A.Color_0B3239;
                SubTitleLabelOne.Font = A.Font_AvenirNextRegular14;
                ConfigureLabelView (labelView, displayEmailAddress, colorIndex);
                return;
            case WhichItems.NameAndPhone:
                TitleLabel.Text = displayName;
                SubTitleLabelOne.Text = displayPhoneNumber;
                TitleLabel.TextColor = A.Color_0B3239;
                TitleLabel.Font = A.Font_AvenirNextDemiBold17;
                SubTitleLabelOne.TextColor = A.Color_0B3239;
                SubTitleLabelOne.Font = A.Font_AvenirNextRegular14;
                ConfigureLabelView (labelView, displayName, colorIndex);
                return;
            case WhichItems.EmailNameAndPhone:
                TitleLabel.Text = displayName;
                SubTitleLabelOne.Text = displayEmailAddress;
                SubTitleLabelTwo.Text = displayPhoneNumber;
                TitleLabel.Frame = new RectangleF (TitleLabel.Frame.X, TitleLabel.Frame.Y - 10, TitleLabel.Frame.Width, TitleLabel.Frame.Height);
                TitleLabel.TextColor = A.Color_0B3239;
                TitleLabel.Font = A.Font_AvenirNextDemiBold17;
                SubTitleLabelOne.TextColor = A.Color_0B3239;
                SubTitleLabelOne.Font = A.Font_AvenirNextRegular12;
                SubTitleLabelOne.Frame = new RectangleF (SubTitleLabelOne.Frame.X, SubTitleLabelOne.Frame.Y - 10, SubTitleLabelOne.Frame.Width, SubTitleLabelOne.Frame.Height);
                SubTitleLabelTwo.TextColor = A.Color_0B3239;
                SubTitleLabelTwo.Font = A.Font_AvenirNextRegular12;
                SubTitleLabelTwo.Frame = new RectangleF (SubTitleLabelTwo.Frame.X, SubTitleLabelTwo.Frame.Y - 10, SubTitleLabelTwo.Frame.Width, SubTitleLabelTwo.Frame.Height);
                ConfigureLabelView (labelView, displayName, colorIndex);
                return;
            default:
                return;
            }
        }

        protected void ConfigureLabelView (UILabel labelView, string labelText, int colorIndex)
        {
            labelView.Hidden = false;
            labelView.Text = Util.NameToLetters (labelText);
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

