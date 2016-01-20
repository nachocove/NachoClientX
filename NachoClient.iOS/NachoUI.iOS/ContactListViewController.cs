// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using System.Collections.Generic;
using System.Linq;

using Foundation;
using UIKit;

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using SwipeViewBinding;

namespace NachoClient.iOS
{

    public partial class ContactListViewController : NcUIViewController, IContactsTableViewSourceDelegate, INachoContactDefaultSelector
    {
        SwipeView swipeView;
        LettersSwipeViewDataSource swipeViewDateSource;
        UITableView TableView;

        SwitchAccountButton switchAccountButton;
        NcUIBarButtonItem addContactButton;

        protected bool contactsNeedsRefresh;

        ContactsTableViewSource contactTableViewSource;
        ContactsGeneralSearch searcher;

        protected NcCapture ReloadCapture;
        private string ReloadCaptureName;

        public ContactListViewController (IntPtr handle) : base (handle)
        {
            // iOS 8 bug sez stack overflow
            //  var a = UILabel.AppearanceWhenContainedIn (typeof(UITableViewHeaderFooterView), typeof(ContactListViewController));
            // a.Font = A.Font_AvenirNextMedium24;
            // a.TextColor = A.Color_NachoDarkText;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            ReloadCaptureName = "ContactListViewController.Reload";
            NcCapture.AddKind (ReloadCaptureName);
            ReloadCapture = NcCapture.Create (ReloadCaptureName);

            swipeView = new SwipeView ();
            swipeView.Frame = new CGRect (10, 0, View.Frame.Width - 20, 55);
            swipeView.BackgroundColor = UIColor.White;
            swipeView.PagingEnabled = false;
            swipeView.DecelerationRate = 0.1f;

            View.AddSubview (swipeView);
            swipeViewDateSource = new LettersSwipeViewDataSource (this);
            swipeView.DataSource = swipeViewDateSource;
            swipeView.Delegate = new LettersSwipeViewDelegate (this);

            var lineView = new UIView (new CGRect (0, 55, View.Frame.Width, 1));
            lineView.BackgroundColor = A.Color_NachoBorderGray;
            View.AddSubview (lineView);

            TableView = new UITableView (new CGRect (0, 56, View.Frame.Width, View.Frame.Height - 56), UITableViewStyle.Grouped);
            TableView.SeparatorColor = A.Color_NachoBackgroundGray;
            TableView.BackgroundColor = A.Color_NachoBackgroundGray;
            TableView.TableFooterView = new UIView (new CGRect (0, 0, TableView.Frame.Width, 100));
            TableView.AccessibilityLabel = "Contact list";
            View.AddSubview (TableView);

            InitializeSearchDisplayController ();

            // Manages the search bar & auto-complete table.
            contactTableViewSource = new ContactsTableViewSource ();
            contactTableViewSource.SetOwner (this, NcApplication.Instance.Account, true, SearchDisplayController);

            TableView.Source = contactTableViewSource;

            SearchDisplayController.SearchResultsTableView.Source = contactTableViewSource;
            SearchDisplayController.SearchResultsTableView.SeparatorColor = A.Color_NachoBackgroundGray;
            SearchDisplayController.SearchResultsTableView.BackgroundColor = A.Color_NachoBackgroundGray;

            var searchButton = new NcUIBarButtonItem (UIBarButtonSystemItem.Search);
            searchButton.AccessibilityLabel = "Search";
            searchButton.TintColor = A.Color_NachoBlue;

            addContactButton = new NcUIBarButtonItem (UIBarButtonSystemItem.Add);
            addContactButton.AccessibilityLabel = "Add contact";
            addContactButton.TintColor = A.Color_NachoBlue;

            NavigationItem.RightBarButtonItem = addContactButton;
            NavigationItem.LeftItemsSupplementBackButton = true;
            NavigationItem.LeftBarButtonItem = searchButton;

            NavigationController.NavigationBar.Translucent = false;

            switchAccountButton = new SwitchAccountButton (SwitchAccountButtonPressed);
            NavigationItem.TitleView = switchAccountButton;

            // Adjust the icon; contacts covers all account
            SwitchToAccount (NcApplication.Instance.Account);

            addContactButton.Clicked += (object sender, EventArgs e) => {
                if (NcApplication.Instance.Account.CanAddContact ()) {
                    PerformSegue ("ContactsToContactEdit", new SegueHolder (NcApplication.Instance.Account));
                } else {
                    var canAddAccounts = McAccount.GetCanAddContactAccounts ();
                    var actions = new NcAlertAction[canAddAccounts.Count];
                    for (int n = 0; n < canAddAccounts.Count; n++) {
                        var account = canAddAccounts [n];
                        var displayName = account.DisplayName + ": " + account.EmailAddr;
                        actions [n] = new NcAlertAction (displayName, () => {
                            PerformSegue ("ContactsToContactEdit", new SegueHolder (account));
                        });
                    }
                    NcActionSheet.Show (this.View, this, null,
                        "Cannot add contacts to the current account. Select other account for the new contact.", actions);
                }
            };

            searchButton.Clicked += (object sender, EventArgs e) => {
                SearchDisplayController.SearchBar.BecomeFirstResponder ();
            };

            // Load when view becomes visible
            contactsNeedsRefresh = true;
        }

        protected void InitializeSearchDisplayController ()
        {
            var sb = new UISearchBar ();

            // creating the controller sets up its pointers
            new UISearchDisplayController (sb, this);

            TableView.TableHeaderView = sb;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            searcher = new ContactsGeneralSearch (UpdateSearchResultsUi);
            SearchDisplayController.Delegate = new ContactsSearchDisplayDelegate (searcher);
            switchAccountButton.SetAccountImage (NcApplication.Instance.Account);
            MaybeRefreshContacts ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            PermissionManager.DealWithContactsPermission ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            searcher.Dispose ();
            searcher = null;
            SearchDisplayController.Delegate = null;
        }

        void SwitchAccountButtonPressed ()
        {
            SwitchAccountViewController.ShowDropdown (this, SwitchToAccount);
        }

        void SwitchToAccount (McAccount account)
        {
            switchAccountButton.SetAccountImage (account);
            // If no account supports adding contacts, hide the button
            bool hide = (0 == McAccount.GetCanAddContactAccounts ().Count);
            NavigationItem.RightBarButtonItem = hide ? null : addContactButton;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_ContactSetChanged == s.Status.SubKind) {
                RefreshContactsIfVisible ();
            }
        }

        void UpdateSearchResultsUi (string searchString, List<McContactEmailAddressAttribute> results)
        {
            contactTableViewSource.SetSearchResults (results);
            SearchDisplayController.SearchResultsTableView.ReloadData ();
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
            if (segue.Identifier.Equals ("SegueToContactDefaultSelection")) {
                var h = sender as SegueHolder;
                var c = (McContact)h.value;
                var type = (ContactDefaultSelectionViewController.DefaultSelectionType)h.value2;
                ContactDefaultSelectionViewController destinationController = (ContactDefaultSelectionViewController)segue.DestinationViewController;
                destinationController.SetContact (c);
                destinationController.viewType = type;
                destinationController.owner = this;
                return;
            }
            if (segue.Identifier.Equals ("ContactsToContactEdit")) {
                var destinationViewController = (ContactEditViewController)segue.DestinationViewController;
                destinationViewController.controllerType = ContactEditViewController.ControllerType.Add;
                var h = sender as SegueHolder;
                var a = (McAccount)h.value;
                destinationViewController.account = a;
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        protected void RefreshContactsIfVisible ()
        {
            contactsNeedsRefresh = true;
            if (!this.IsVisible ()) {
                return;
            }
            if (SearchDisplayController.Active) {
                return;
            }
            MaybeRefreshContacts ();
        }

        protected void MaybeRefreshContacts ()
        {
            if (contactsNeedsRefresh) {
                contactsNeedsRefresh = false;
                ReloadCapture.Start ();
                NachoCore.Utils.NcAbate.HighPriority ("ContactListViewController LoadContacts");
                // RIC -- only highlight recents from the current account
                var recents = McContact.RicContactsSortedByRank (NcApplication.Instance.Account.Id, 5);
                var contacts = McContact.AllContactsSortedByName (true);
                contactTableViewSource.SetContacts (recents, contacts, true);
                TableView.ReloadData ();
                NachoCore.Utils.NcAbate.RegularPriority ("ContactListViewController LoadContacts");
                ReloadCapture.Stop ();
            } else {
                contactTableViewSource.ReconfigureVisibleCells (TableView);
            }
        }

        /// IContactsTableViewSourceDelegate
        public void ContactSelectedCallback (McContact contact)
        {
            PerformSegue ("ContactsToContactDetail", new SegueHolder (contact));
        }

        /// IContactsTableViewSourceDelegate
        public void EmailSwipeHandler (McContact contact)
        {
            if (contact == null) {
                Util.ComplainAbout ("No Email Address", "This contact does not have an email address.");
            } else {
                var address = Util.GetContactDefaultEmail (contact);
                if (address == null) {
                    if (contact.EmailAddresses.Count == 0) {
                        if (contact.CanUserEdit ()) {
                            PerformSegue ("SegueToContactDefaultSelection", new SegueHolder (contact, ContactDefaultSelectionViewController.DefaultSelectionType.EmailAdder));
                        } else {
                            Util.ComplainAbout ("No Email Address", "This contact does not have an email address, and we are unable to modify the contact.");
                        }
                    } else {
                        PerformSegue ("SegueToContactDefaultSelection", new SegueHolder (contact, ContactDefaultSelectionViewController.DefaultSelectionType.DefaultEmailSelector));
                    }
                } else {
                    ComposeMessage (address);
                }
            }
        }

        /// IContactsTableViewSourceDelegate
        public void CallSwipeHandler (McContact contact)
        {
            Util.CallContact ("SegueToContactDefaultSelection", contact, this);
        }

        public void SelectSectionIncludingRecent (int index)
        {
            contactTableViewSource.ScrollToSectionIncludingRecent (TableView, index);
        }

        public void ContactDefaultSelectorComposeMessage (string address)
        {
            ComposeMessage (address);
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

        public class LettersSwipeViewDelegate : SwipeViewDelegate
        {
            ContactListViewController owner;

            public LettersSwipeViewDelegate (ContactListViewController owner) : base ()
            {
                this.owner = owner;
            }

            public override void DidSelectItemAtIndex (SwipeView swipeView, int index)
            {
                if (null != owner) {
                    owner.SelectSectionIncludingRecent (index);
                }
            }
        }

        public class LettersSwipeViewDataSource : SwipeViewDataSource
        {
            UIView[] viewList;

            ContactListViewController owner;

            // Recent, A..Z, #
            public override int NumberOfItemsInSwipeView (SwipeView swipeView)
            {
                return 28;
            }

            public LettersSwipeViewDataSource (ContactListViewController owner) : base ()
            {
                this.owner = owner;
                viewList = new UIView[28];
                viewList [0] = CreateImageView (0);
                const string letters = "!ABCDEFGHIJKLMNOPQRSTUVWXYZ#";
                for (int i = 1; i < 28; i++) {
                    viewList [i] = CreateLetterView (i, letters [i]);
                }
            }

            public override UIView ViewForItemAtIndex (SwipeView swipeView, int index, UIView view)
            {
                return viewList [index];
            }

            protected void SelectButton (UIButton button)
            {
                foreach (var v in viewList) {
                    var b = (UIButton)v.Subviews [0];
                    if (b.Selected) {
                        b.Selected = false;
                        b.BackgroundColor = UIColor.Clear;
                    }
                }
                button.Selected = true;
                button.BackgroundColor = A.Color_NachoGreen;
            }

            public void SelectButton (int section)
            {
                var n = section;
                var view = viewList [n];
                var button = (UIButton)view.Subviews [0];
                SelectButton (button);
            }

            protected UIView CreateLetterView (int index, char c)
            {
                var view = new UIView (new CGRect (0, 0, 50, 50));
                var title = new String (c, 1);
                var button = UIButton.FromType (UIButtonType.RoundedRect);
                button.Frame = new CGRect (7, 7, 36, 36);
                button.Layer.CornerRadius = 18;
                button.Layer.MasksToBounds = true;
                button.TintColor = UIColor.Clear;
                button.BackgroundColor = UIColor.Clear;
                button.HorizontalAlignment = UIControlContentHorizontalAlignment.Center;
                button.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
                button.Layer.BorderWidth = 1f;
                button.SetTitle (title, UIControlState.Normal);
                button.SetTitle (title, UIControlState.Selected);
                button.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
                button.SetTitleColor (UIColor.White, UIControlState.Selected);
                button.AccessibilityLabel = title;
                button.Font = A.Font_AvenirNextDemiBold17;
                button.TouchUpInside += (object sender, EventArgs e) => {
                    SelectButton ((UIButton)sender);
                    if (null != owner) {
                        owner.SelectSectionIncludingRecent (index);
                    }
                };
                view.AddSubview (button);
                return view;
            }

            protected UIView CreateImageView (int index)
            {
                var button = UIButton.FromType (UIButtonType.Custom);
                button.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
                button.Layer.BorderWidth = 1.0f;
                button.Frame = new CGRect (7, 7, 36, 36);
                button.Layer.CornerRadius = 18;
                button.Layer.MasksToBounds = true;
                using (var image = UIImage.FromBundle ("contacts-recent")) {
                    button.SetImage (image, UIControlState.Normal);
                }
                using (var image = UIImage.FromBundle ("contacts-recent-active")) {
                    button.SetImage (image, UIControlState.Selected);
                }
                button.TouchUpInside += (object sender, EventArgs e) => {
                    SelectButton ((UIButton)sender);
                    if (null != owner) {
                        owner.SelectSectionIncludingRecent (index);
                    }
                };
                var view = new UIView (new CGRect (0, 0, 50, 50));
                view.AddSubview (button);
                return view;
            }
        }
    }
}
