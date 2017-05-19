//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using CoreGraphics;
using UIKit;
using Foundation;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;
using NachoPlatform;
using System.Text.RegularExpressions;

namespace NachoClient.iOS
{
    public class ContactsTableViewSource : UITableViewSource
    {
        bool multipleSections;
        ContactBin[] sections;

        bool allowSwiping;

        List<NcContactIndex> recent;
        List<NcContactIndex> contacts;
        List<McContactEmailAddressAttribute> searchResults = null;
        IContactsTableViewSourceDelegate owner;

        UISearchDisplayController SearchDisplayController;

        protected const string UICellReuseIdentifier = "UICell";
        protected const string ContactCellReuseIdentifier = "ContactCell";

        protected string searchToken;
        McAccount accountForSearchAPI;

        IDisposable abatementRequest = null;

        public ContactsTableViewSource ()
        {
            owner = null;
            allowSwiping = false;
        }

        public void SetOwner (IContactsTableViewSourceDelegate owner, McAccount accountForSearchAPI, bool allowSwiping, UISearchDisplayController SearchDisplayController)
        {
            this.owner = owner;
            this.accountForSearchAPI = accountForSearchAPI;
            this.allowSwiping = allowSwiping;
            this.SearchDisplayController = SearchDisplayController;
        }

        public void SetContacts (List<NcContactIndex> recent, List<NcContactIndex> contacts, bool multipleSections)
        {
            this.recent = recent;
            sections = ContactsBinningHelper.BinningContacts (ref contacts);
            this.contacts = contacts;
            this.multipleSections = multipleSections;

            if (SearchDisplayController.Active) {
                SearchDisplayController.Delegate.ShouldReloadForSearchScope (SearchDisplayController, 0);
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
                McPending.Cancel (accountForSearchAPI.Id, searchToken);
                searchToken = null;
            }
        }

        /// <summary>
        /// Tableview delegate
        /// </summary>
        public override nint NumberOfSections (UITableView tableView)
        {
            if (SearchDisplayController.SearchResultsTableView == tableView) {
                return 1;
            }
            return ((null == recent) ? 0 : 1) + (multipleSections ? 27 : 1);
        }

        public override nfloat GetHeightForHeader (UITableView tableView, nint section)
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

        public override UIView GetViewForHeader (UITableView tableView, nint section)
        {
            if (!multipleSections && (null == recent)) {
                return new UIView (new CGRect (0, 0, 0, 0));
            }
            if (SearchDisplayController.SearchResultsTableView == tableView) {
                return new UIView (new CGRect (0, 0, 0, 0));
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
            var view = new UIView (new CGRect (0, 0, tableView.Frame.Width, 32));
            var label = new UILabel ();
            label.Font = A.Font_AvenirNextDemiBold17;
            label.Text = TitleForHeader (tableView, section);
            label.SizeToFit ();
            label.Center = new CGPoint (15 + (label.Frame.Width / 2), 10);
            view.AddSubview (label);
            return view;
        }

        public override string TitleForHeader (UITableView tableView, nint section)
        {
            var n = (int)section;
            if (null != recent) {
                if (0 == section) {
                    return "Recent";
                }
                n = n - 1;
            }
            return sections [n].FirstLetter.ToString ();
        }

        /// <summary>
        /// The number of rows in the specified section.
        /// </summary>
        public override nint RowsInSection (UITableView tableview, nint section)
        {
            int rows;

            if (SearchDisplayController.SearchResultsTableView == tableview) {
                rows = ((null == searchResults) ? 0 : searchResults.Count);
            } else if ((null != recent) && (0 == section)) {
                rows = recent.Count;
            } else if (multipleSections) {
                var index = section - ((null == recent) ? 0 : 1);
                rows = sections [index].Length;
            } else {
                rows = ((null == contacts) ? 0 : contacts.Count);
            }
            return rows;
        }

        protected McContact ContactFromIndexPath (UITableView tableView, NSIndexPath indexPath, out string alternateEmailAddress)
        {
            McContact contact;

            alternateEmailAddress = null;
            if (SearchDisplayController.SearchResultsTableView == tableView) {
                var contactEmailAttribute = searchResults [indexPath.Row];
                contact = contactEmailAttribute.GetContact ();
                alternateEmailAddress = contactEmailAttribute.Value;
            } else if ((null != recent) && (0 == indexPath.Section)) {
                contact = recent [indexPath.Row].GetContact ();
            } else if (multipleSections) {
                var section = indexPath.Section - ((null == recent) ? 0 : 1);
                var index = indexPath.Row + sections [section].Start;
                contact = contacts [index].GetContact ();
            } else {
                contact = contacts [indexPath.Row].GetContact ();
            }
            return contact;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            string dummy;
            McContact contact = ContactFromIndexPath (tableView, indexPath, out dummy);
            if (null != contact) {
                owner.ContactSelectedCallback (contact);
            }
            DumpInfo (contact);
            tableView.DeselectRow (indexPath, true);
        }

        public override void AccessoryButtonTapped (UITableView tableView, NSIndexPath indexPath)
        {
            string dummy;
            McContact contact = ContactFromIndexPath (tableView, indexPath, out dummy);
            owner.ContactSelectedCallback (contact);
        }

        protected void SwipedCall (string number)
        {
            Log.Info (Log.LOG_UI, "Swiped Call");

            if (string.IsNullOrEmpty (number)) {
                Util.ComplainAbout ("No phone number", "You've selected a contact who does not have a phone number");
                return;
            }
            if (!Util.PerformAction ("tel", number)) {
                Util.ComplainAbout ("Cannot Dial", "We are unable to dial this phone number");
            }
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

        public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            return ContactCellOld.ROW_HEIGHT;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = null;
            cell = tableView.DequeueReusableCell (ContactCellReuseIdentifier);
            if (null == cell) {
                cell = ContactCellOld.CreateCell (tableView, VipButtonTouched);
            }
            string emailAddress = null;
            var contact = ContactFromIndexPath (tableView, indexPath, out emailAddress);
            ContactCellOld.ConfigureCell (tableView, cell, contact, owner, allowSwiping, emailAddress);
            return cell;
        }

        protected void VipButtonTouched (object sender, EventArgs e)
        {
            UIButton vipButton = (UIButton)sender;
            UITableViewCell containingCell = Util.FindEnclosingTableViewCell (vipButton);
            UITableView containingTable = Util.FindEnclosingTableView (vipButton);
            NSIndexPath cellIndexPath = containingTable.IndexPathForCell (containingCell);
            string dummy;
            McContact c = ContactFromIndexPath (containingTable, cellIndexPath, out dummy);

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
            if (null == abatementRequest) {
                abatementRequest = NcAbate.UITimedAbatement (TimeSpan.FromSeconds (10));
            }
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            if (null != abatementRequest) {
                abatementRequest.Dispose ();
                abatementRequest = null;
            }
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            if (!willDecelerate && null != abatementRequest) {
                abatementRequest.Dispose ();
                abatementRequest = null;
            }
        }

        public void ReconfigureVisibleCells (UITableView tableView)
        {
            if (null == tableView) {
                return;
            }
            var paths = tableView.IndexPathsForVisibleRows;
            if (null != paths) {
                foreach (var path in paths) {
                    var cell = tableView.CellAt (path);
                    if (null != cell) {
                        string emailAddress = null;
                        var contact = ContactFromIndexPath (tableView, path, out emailAddress);
                        ContactCellOld.ConfigureCell (tableView, cell, contact, owner, allowSwiping, emailAddress);
                    }
                }
            }
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
    }

    public class ContactsSearchDisplayDelegate : UISearchDisplayDelegate
    {
        ContactsGeneralSearch searcher;

        private ContactsSearchDisplayDelegate ()
        {
        }

        public ContactsSearchDisplayDelegate (ContactsGeneralSearch searcher)
        {
            this.searcher = searcher;
        }

        public override bool ShouldReloadForSearchScope (UISearchDisplayController controller, nint forSearchOption)
        {
            searcher.SearchFor (controller.SearchBar.Text);
            return false;
        }

        public override bool ShouldReloadForSearchString (UISearchDisplayController controller, string forSearchString)
        {
            if (TestMode.Instance.Process (forSearchString)) {
                return false;
            }
            searcher.SearchFor (forSearchString);
            return false;
        }
    }
}

