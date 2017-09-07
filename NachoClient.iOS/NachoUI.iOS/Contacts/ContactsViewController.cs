// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using System.Collections.Generic;
using System.Linq;

using Foundation;
using UIKit;
using CoreAnimation;

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.iOS
{

    public class ContactsViewController : NachoWrappedTableViewController, IAccountSwitching, ThemeAdopter, NachoSearchControllerDelegate, INachoContactDefaultSelector
    {

        private const string ContactCellReuseIdentifier = "ContactCell";
        private const string ContactGroupHeaderReuseIdentifier = "ContactGroupHeader";

        McAccount Account;
        List<ContactGroup> ContactGroups;
        ContactCache Cache;

        UIBarButtonItem SearchButton;
        UIBarButtonItem NewContactButton;
        UIBarButtonItem DoneSwipingButton;

        ContactSearchResultsViewController SearchResultsViewController;
        NachoSearchController SearchController;

        public ContactsViewController () : base (UITableViewStyle.Plain)
        {
            Account = NcApplication.Instance.Account;
            ContactGroups = new List<ContactGroup> ();
            Cache = new ContactCache ();

            SearchButton = new NcUIBarButtonItem (UIBarButtonSystemItem.Search, ShowSearch);
            NewContactButton = new NcUIBarButtonItem (UIImage.FromBundle ("chat-sharecontact"), UIBarButtonItemStyle.Plain, NewContact);
            DoneSwipingButton = new UIBarButtonItem (NSBundle.MainBundle.LocalizedString ("Done", ""), UIBarButtonItemStyle.Plain, EndSwiping);

            UpdateNavigationItem ();
        }

        #region View Lifecycle

        public override void LoadView ()
        {
            base.LoadView ();
            AutomaticallyAdjustsScrollViewInsets = false;
            TableView.RegisterClassForCellReuse (typeof (ContactCell), ContactCellReuseIdentifier);
            TableView.RegisterClassForHeaderFooterViewReuse (typeof (ContactGroupTableViewHeaderView), ContactGroupHeaderReuseIdentifier);
            TableView.RowHeight = 64.0f;
            TableView.SectionIndexMinimumDisplayRowCount = 20;
            TableView.SectionIndexBackgroundColor = UIColor.Clear;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            Reload ();
            AdoptTheme (Theme.Active);
            StartListeningForStatusInd ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            PermissionManager.DealWithContactsPermission ();
        }

        public override void ViewDidDisappear (bool animated)
        {
            StopListeningForStatusInd ();
            base.ViewDidDisappear (animated);
        }

        public void SwitchToAccount (McAccount account)
        {
            Account = account;
            // TODO: reload recents
        }

        public override void Cleanup ()
        {
            // Clean up nav bar
            SearchButton.Clicked -= ShowSearch;
            NewContactButton.Clicked -= NewContact;
            DoneSwipingButton.Clicked -= EndSwiping;

            // Clean up search
            if (SearchController != null) {
                SearchController.Delegate = null;
            }
            if (SearchResultsViewController != null) {
                SearchResultsViewController.Cleanup ();
                SearchResultsViewController = null;
            }

            base.Cleanup ();
        }

        #endregion

        #region Theme

        Theme adoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (theme != adoptedTheme) {
                adoptedTheme = theme;
                TableView.AdoptTheme (theme);
            }
        }

        #endregion

        #region Reloading

        bool NeedsReload;
        bool IsReloading;

        void SetNeedsReload ()
        {
            NeedsReload = true;
            if (!IsReloading) {
                Reload ();
            }
        }

        void Reload ()
        {
            if (!IsReloading) {
                IsReloading = true;
                NeedsReload = false;
                NcTask.Run (() => {
                    // TODO: recents
                    //var recents = McContact.RicContactsSortedByRank (Account.Id, 5);
                    var contacts = McContact.AllContactsSortedByName ();
                    var contactGroups = ContactGroup.CreateGroups (contacts, Cache);
                    InvokeOnMainThread (() => {
                        IsReloading = false;
                        if (NeedsReload) {
                            Reload ();
                        } else {
                            HandleReloadResults (contactGroups);
                        }
                    });
                }, "ContactsViewController.Reload");
            }
        }

        void HandleReloadResults (List<ContactGroup> contactGroups)
        {
            ContactGroups = contactGroups;
            TableView.ReloadData ();
        }

        #endregion

        #region User Actions

        void ShowSearch (object sender, EventArgs e)
        {
            EndAllTableEdits ();
            if (SearchController == null) {
                SearchResultsViewController = new ContactSearchResultsViewController () { IsLongLived = true };
                SearchController = new NachoSearchController (SearchResultsViewController);
                SearchController.Delegate = this;
            }
            SearchResultsViewController.PrepareForSearching ();
            SearchController.PresentOverViewController (this);
        }

        void EndSwiping (object sender, EventArgs e)
        {
            EndSwiping ();
        }

        void NewContact (object sender, EventArgs e)
        {
            NewContact ();
        }

        void EmailContact (NSIndexPath indexPath)
        {
            var group = ContactGroups [indexPath.Section];
            var contact = group.GetCachedContact (indexPath.Row);
            var address = Util.GetContactDefaultEmail (contact);
            if (address != null) {
                ComposeMessage (address);
            } else {
                SelectDefault (contact, ContactDefaultSelectionViewController.DefaultSelectionType.DefaultEmailSelector);
            }
        }

        void CallContact (NSIndexPath indexPath)
        {
            var group = ContactGroups [indexPath.Section];
            var contact = group.GetCachedContact (indexPath.Row);
            Util.CallContact (contact, (ContactDefaultSelectionViewController.DefaultSelectionType type) => {
                SelectDefault (contact, type);
            });
        }

        #endregion


        #region Search

        public void DidChangeSearchText (NachoSearchController searchController, string text)
        {
            SearchResultsViewController.SearchForText (text);
        }

        public void DidSelectSearch (NachoSearchController searchController)
        {
        }

        public void DidEndSearch (NachoSearchController searchController)
        {
            SearchResultsViewController.EndSearching ();
        }

        #endregion


        #region Data Source & Delegate

        public override nint NumberOfSections (UITableView tableView)
        {
            return ContactGroups.Count;
        }

        public override nint RowsInSection (UITableView tableView, nint section)
        {
            var group = ContactGroups [(int)section];
            return group.Contacts.Count;
        }

        public override nfloat GetHeightForHeader (UITableView tableView, nint section)
        {
            return 32.0f;
        }

        public override UIView GetViewForHeader (UITableView tableView, nint section)
        {
            var headerView = tableView.DequeueReusableHeaderFooterView (ContactGroupHeaderReuseIdentifier) as ContactGroupTableViewHeaderView;
            var group = ContactGroups [(int)section];
            headerView.NameLabel.Text = group.Name;
            if (adoptedTheme != null) {
                headerView.AdoptTheme (adoptedTheme);
            }
            return headerView;
        }

        public override string [] SectionIndexTitles (UITableView tableView)
        {
            var titles = new List<string> ();
            foreach (var group in ContactGroups) {
                titles.Add (group.Name.Substring (0, 1));
            }
            return titles.ToArray ();
        }

        public override nint SectionFor (UITableView tableView, string title, nint atIndex)
        {
            return atIndex;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell (ContactCellReuseIdentifier, indexPath) as ContactCell;
            var group = ContactGroups [indexPath.Section];
            var contact = group.GetCachedContact (indexPath.Row);
            cell.SetContact (contact);
            return cell;
        }

        public override NSIndexPath WillSelectRow (UITableView tableView, NSIndexPath indexPath)
        {
            return base.WillSelectRow (tableView, indexPath);
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            var group = ContactGroups [indexPath.Section];
            var contact = group.GetCachedContact (indexPath.Row);
            ShowContact (contact);
        }

        public override List<SwipeTableRowAction> ActionsForSwipingLeftInRow (UITableView tableView, NSIndexPath indexPath)
        {
            // TODO: VIP/Not VIP
            return null;
        }

        public override List<SwipeTableRowAction> ActionsForSwipingRightInRow (UITableView tableView, NSIndexPath indexPath)
        {
            var group = ContactGroups [indexPath.Section];
            var contact = group.GetCachedContact (indexPath.Row);
            var actions = new List<SwipeTableRowAction> ();
            var hasEmail = contact.EmailAddresses.Count > 0;
            var hasPhone = contact.PhoneNumbers.Count > 0;
            if (hasEmail) {
                actions.Add (new SwipeTableRowAction ("Email (verb)", UIImage.FromBundle ("contacts-email-swipe"), UIColor.FromRGB (0x00, 0xC8, 0x9D), EmailContact));
            }
            if (hasPhone) {
                actions.Add (new SwipeTableRowAction ("Call (verb)", UIImage.FromBundle ("contacts-call-swipe"), UIColor.FromRGB (0xF5, 0x98, 0x27), CallContact));
            }
            return actions;
        }

        public override void WillDisplay (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {
            base.WillDisplay (tableView, cell, indexPath);
            var themed = cell as ThemeAdopter;
            if (themed != null && adoptedTheme != null) {
                themed.AdoptTheme (adoptedTheme);
            }
        }

        public override void WillBeginSwiping (UITableView tableView, NSIndexPath indexPath)
        {
            base.WillBeginSwiping (tableView, indexPath);
            UpdateNavigationItem ();
        }

        public override void DidEndSwiping (UITableView tableView, NSIndexPath indexPath)
        {
            base.DidEndSwiping (tableView, indexPath);
            UpdateNavigationItem ();
        }

        #endregion

        #region Private Helpers

        void ShowContact (McContact contact)
        {
            var contactViewController = new ContactDetailViewController ();
            contactViewController.contact = contact;
            NavigationController.PushViewController (contactViewController, true);
        }

        void NewContact ()
        {
            if (Account.CanAddContact ()) {
                NewContact (Account);
            } else {
                ShowAccountPicker ();
            }
        }

        void ShowAccountPicker ()
        {
            var accounts = McAccount.GetCanAddContactAccounts ();
            var actions = new List<NcAlertAction> ();
            foreach (var account in accounts) {
                actions.Add (CreateAddAccountAction (account));
            }
            actions.Add (new NcAlertAction (NSBundle.MainBundle.LocalizedString ("Cancel", ""), () => { }));
            NcActionSheet.Show (NewContactButton, this, NSBundle.MainBundle.LocalizedString ("Choose an account for the new Contact", "Title for contact account picker"), "", actions.ToArray ());
        }

        NcAlertAction CreateAddAccountAction (McAccount account)
        {
            var displayName = account.DisplayName + ": " + account.EmailAddr;
            return new NcAlertAction (displayName, () => {
                NewContact (account);
            });
        }

        void NewContact (McAccount account)
        {
            var destinationViewController = new ContactEditViewController ();
            destinationViewController.controllerType = ContactEditViewController.ControllerType.Add;
            destinationViewController.account = account;
            NavigationController.PushViewController (destinationViewController, true);
        }

        public void ComposeMessage (string address)
        {
            var account = NcApplication.Instance.DefaultEmailAccount;
            var message = McEmailMessage.MessageWithSubject (account, "");
            message.To = address;
            var composeViewController = new MessageComposeViewController (account);
            composeViewController.Composer.Message = message;
            composeViewController.Present ();
        }

        void SelectDefault (McContact contact, ContactDefaultSelectionViewController.DefaultSelectionType type)
        {
            var destinationController = new ContactDefaultSelectionViewController ();
            destinationController.SetContact (contact);
            destinationController.viewType = type;
            destinationController.owner = this;
            PresentViewController (destinationController, true, null);
        }

        protected void EndAllTableEdits ()
        {
            if (SwipingIndexPath != null) {
                EndSwiping ();
            }
        }

        void UpdateNavigationItem ()
        {
            bool isSwiping = SwipingIndexPath != null;
            if (isSwiping) {
                NavigationItem.LeftBarButtonItem = null;
                NavigationItem.RightBarButtonItem = DoneSwipingButton;
            } else {
                NavigationItem.LeftBarButtonItem = SearchButton;
                bool canCreateContact = McAccount.GetCanAddContactAccounts ().Count > 0;
                NavigationItem.RightBarButtonItem = canCreateContact ? NewContactButton : null;
            }
        }

        public void ContactDefaultSelectorComposeMessage (string address)
        {
            ComposeMessage (address);
        }

        #endregion

        #region System EventArgs

        bool IsListeningForStatusInd;

        void StartListeningForStatusInd ()
        {
            if (!IsListeningForStatusInd) {
                IsListeningForStatusInd = true;
                NcApplication.Instance.StatusIndEvent += StatusIndCallback;
            }
        }

        void StopListeningForStatusInd ()
        {
            if (IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent -= StatusIndCallback;
                IsListeningForStatusInd = false;
            }
        }

        void StatusIndCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_ContactSetChanged == s.Status.SubKind) {
                SetNeedsReload ();
            }
        }

        #endregion

        public override UIStatusBarStyle PreferredStatusBarStyle ()
        {
            return UIStatusBarStyle.LightContent;
        }
    }

    class ContactGroupTableViewHeaderView : UITableViewHeaderFooterView, ThemeAdopter
    {
        public UILabel NameLabel { get; private set; }
        CALayer BorderLayer;
        nfloat BorderWidth = 0.5f;

        public ContactGroupTableViewHeaderView (IntPtr handle) : base (handle)
        {
            NameLabel = new UILabel ();
            BorderLayer = new CALayer ();
            ContentView.AddSubview (NameLabel);
            ContentView.Layer.AddSublayer (BorderLayer);
        }

        public void AdoptTheme (Theme theme)
        {
            //BackgroundView.BackgroundColor = theme.TableViewGroupedBackgroundColor;
            ContentView.BackgroundColor = theme.TableViewGroupedBackgroundColor;
            NameLabel.BackgroundColor = theme.TableViewGroupedBackgroundColor;
            NameLabel.TextColor = theme.TableSectionHeaderTextColor;
            NameLabel.Font = theme.BoldDefaultFont.WithSize (17.0f);
            BorderLayer.BackgroundColor = theme.FilterbarBorderColor.CGColor;
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            nfloat leftPadding = 12.0f;
            NameLabel.Frame = new CGRect (leftPadding, 0.0f, ContentView.Bounds.Width - leftPadding, ContentView.Bounds.Height);
            BorderLayer.Frame = new CGRect (0.0f, ContentView.Bounds.Height - BorderWidth, ContentView.Bounds.Width, BorderWidth);
        }
    }

    public class ContactSearchResultsViewController : SearchResultsViewController
    {

        const string ContactCellIdentifier = "ContactCellIdentifier";
        ContactsGeneralSearch Searcher;
        List<McContactEmailAddressAttribute> Results;
        public event EventHandler<McContact> ContactSelected;

        public ContactSearchResultsViewController () : base ()
        {
            Searcher = new ContactsGeneralSearch (UpdateResults);
            Results = new List<McContactEmailAddressAttribute> ();
        }

        public override void Cleanup ()
        {
            Searcher = null;
            base.Cleanup ();
        }

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.RegisterClassForCellReuse (typeof (ContactCell), ContactCellIdentifier);
            TableView.BackgroundColor = UIColor.White;
            TableView.RowHeight = 64.0f;
        }

        public void PrepareForSearching ()
        {
            TableView.ReloadData ();
        }

        public void EndSearching ()
        {
        }

        public void SearchForText (string searchText)
        {
            Searcher.SearchFor (searchText);
        }

        void UpdateResults (string searchString, List<McContactEmailAddressAttribute> results)
        {
            Results = results;
            TableView.ReloadData ();
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override nint RowsInSection (UITableView tableView, nint section)
        {
            return Results.Count;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell (ContactCellIdentifier) as ContactCell;
            var emailAttribute = Results [indexPath.Row];
            var contact = emailAttribute.GetContact ();
            cell.SetContact (contact, alternateEmail: emailAttribute.Value);
            return cell;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            var emailAttribute = Results [indexPath.Row];
            var contact = emailAttribute.GetContact ();
            if (ContactSelected != null) {
                ContactSelected (this, contact);
            } else {
                ShowContact (contact);
            }
        }

        void ShowContact (McContact contact)
        {
            var contactViewController = new ContactDetailViewController ();
            contactViewController.contact = contact;
            NavigationController.PushViewController (contactViewController, true);
            NavigationController.SetNavigationBarHidden (false, true);
        }

    }
}
