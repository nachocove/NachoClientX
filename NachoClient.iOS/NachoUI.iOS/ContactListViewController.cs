// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

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
        McAccount account;

        ContactsTableViewSource contactTableViewSource;

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

            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();

            swipeView = new SwipeView ();
            swipeView.Frame = new RectangleF (10, 0, View.Frame.Width - 20, 55);
            swipeView.BackgroundColor = UIColor.White;
            swipeView.PagingEnabled = false;
            swipeView.DecelerationRate = 0.1f;

            View.AddSubview (swipeView);
            swipeViewDateSource = new LettersSwipeViewDataSource (this);
            swipeView.DataSource = swipeViewDateSource;
            swipeView.Delegate = new LettersSwipeViewDelegate (this);

            var lineView = new UIView (new RectangleF (0, 55, View.Frame.Width, 1));
            lineView.BackgroundColor = A.Color_NachoBorderGray;
            View.AddSubview (lineView);

            TableView = new UITableView (new RectangleF (0, 56, View.Frame.Width, View.Frame.Height - 56), UITableViewStyle.Grouped);
            TableView.SeparatorColor = A.Color_NachoLightBorderGray;
            TableView.BackgroundColor = A.Color_NachoLightBorderGray;
            TableView.TableFooterView = new UIView (new RectangleF (0, 0, TableView.Frame.Width, 100));
            View.AddSubview (TableView);

            InitializeSearchDisplayController ();

            // Manages the search bar & auto-complete table.
            contactTableViewSource = new ContactsTableViewSource ();
            contactTableViewSource.SetOwner (this, true, SearchDisplayController);

            TableView.Source = contactTableViewSource;

            SearchDisplayController.SearchResultsTableView.Source = contactTableViewSource;
            SearchDisplayController.SearchResultsTableView.SeparatorColor = A.Color_NachoLightBorderGray;
            SearchDisplayController.SearchResultsTableView.BackgroundColor = A.Color_NachoLightBorderGray;

            var searchButton = new UIBarButtonItem (UIBarButtonSystemItem.Search);
            searchButton.TintColor = A.Color_NachoBlue;
            var addContactButton = new UIBarButtonItem (UIBarButtonSystemItem.Add);
            addContactButton.TintColor = A.Color_NachoBlue;

            NavigationItem.RightBarButtonItem = addContactButton;
            NavigationItem.LeftItemsSupplementBackButton = true;
            NavigationItem.LeftBarButtonItem = searchButton;

            NavigationController.NavigationBar.Translucent = false;
            NavigationItem.Title = "Contacts";

            addContactButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("ContactsToContactEdit", new SegueHolder (null));
            };

            searchButton.Clicked += (object sender, EventArgs e) => {
                SearchDisplayController.SearchBar.BecomeFirstResponder ();
            };
        }

        protected void InitializeSearchDisplayController ()
        {
            var sb = new UISearchBar ();

            // creating the controller set up its pointers
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

            LoadContacts ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
//            swipeViewDateSource.SelectButton (0);
            PermissionManager.DealWithContactsPermission ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_ContactSetChanged == s.Status.SubKind) {
                LoadContacts ();
            }
            if (NcResult.SubKindEnum.Info_SearchCommandSucceeded == s.Status.SubKind) {
                LoadContacts ();
                var sb = SearchDisplayController.SearchBar;
                contactTableViewSource.UpdateSearchResults (sb.SelectedScopeButtonIndex, sb.Text, false);
                SearchDisplayController.SearchResultsTableView.ReloadData ();
            }
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
                return;
            }
            if (segue.Identifier.Equals ("SegueToNachoNow")) {
                return;
            }
            if (segue.Identifier.Equals ("ContactsToQuickMessageCompose")) {
                var h = sender as SegueHolder;
                MessageComposeViewController mcvc = (MessageComposeViewController)segue.DestinationViewController;
                mcvc.SetEmailPresetFields (new NcEmailAddress (NcEmailAddress.Kind.To, (string)h.value));
                return;
            }
            if (segue.Identifier.Equals ("SegueToMessageCompose")) {
                var h = sender as SegueHolder;
                MessageComposeViewController mcvc = (MessageComposeViewController)segue.DestinationViewController;
                mcvc.SetEmailPresetFields (new NcEmailAddress (NcEmailAddress.Kind.To, (string)h.value));
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        protected void LoadContacts ()
        {
            NachoCore.Utils.NcAbate.HighPriority ("ContactListViewController LoadContacs");
            var recents = McContact.RicContactsSortedByRank (account.Id, 5);
            var contacts = McContact.AllContactsSortedByName ();
            contactTableViewSource.SetContacts (recents, contacts, true);
            TableView.ReloadData ();
            NachoCore.Utils.NcAbate.RegularPriority ("ContactListViewController LoadContacs");
        }

        /// IContactsTableViewSourceDelegate
        public void PerformSegueForDelegate (string identifier, NSObject sender)
        {
            PerformSegue (identifier, sender);
        }

        /// IContactsTableViewSourceDelegate
        public void ContactSelectedCallback (McContact contact)
        {
            PerformSegue ("ContactsToContactDetail", new SegueHolder (contact));
        }

        /// IContactsTableViewSourceDelegate
        public void EmailSwipeHandler (McContact contact)
        {
            Util.EmailContact ("SegueToContactDefaultSelection", contact, this);
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

        public void PerformSegueForContactDefaultSelector (string identifier, NSObject sender)
        {
            PerformSegue (identifier, sender);
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
                var view = new UIView (new RectangleF (0, 0, 50, 50));
                var title = new String (c, 1);
                var button = UIButton.FromType (UIButtonType.RoundedRect);
                button.Frame = new RectangleF (7, 7, 36, 36);
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
                button.Frame = new RectangleF (7, 7, 36, 36);
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
                var view = new UIView (new RectangleF (0, 0, 50, 50));
                view.AddSubview (button);
                return view;
            }
        }
    }
}
