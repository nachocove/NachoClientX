//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

using Foundation;
using UIKit;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    public interface ContactPickerViewControllerDelegate
    {
        void ContactPickerDidPickContact (ContactPickerViewController vc, McContact contact);
        void ContactPickerDidPickCancel (ContactPickerViewController vc);
    }

    public class ContactPickerViewController : NachoWrappedTableViewController, ThemeAdopter, NachoSearchControllerDelegate
    {

        public ContactPickerViewControllerDelegate PickerDelegate {
            get {
                ContactPickerViewControllerDelegate pickerDelegate;
                if (WeakPickerDelegate.TryGetTarget (out pickerDelegate)) {
                    return pickerDelegate;
                }
                return null;
            }
            set {
                WeakPickerDelegate.SetTarget (value);
            }
        }

        WeakReference<ContactPickerViewControllerDelegate> WeakPickerDelegate;

        private const string ContactCellReuseIdentifier = "ContactCell";
        private const string ContactGroupHeaderReuseIdentifier = "ContactGroupHeader";

        List<ContactGroup> ContactGroups;
        ContactCache Cache;

        UIBarButtonItem SearchButton;
        UIBarButtonItem CloseButton;

        ContactSearchResultsViewController SearchResultsViewController;
        NachoSearchController SearchController;

        public ContactPickerViewController () : base (UITableViewStyle.Plain)
        {
            ContactGroups = new List<ContactGroup> ();
            Cache = new ContactCache ();

            SearchButton = new NcUIBarButtonItem (UIBarButtonSystemItem.Search, ShowSearch);
            CloseButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (CloseButton, "icn-close");
            CloseButton.AccessibilityLabel = "Close";
            CloseButton.Clicked += Close;

            NavigationItem.LeftBarButtonItem = CloseButton;
            NavigationItem.RightBarButtonItem = SearchButton;

            WeakPickerDelegate = new WeakReference<ContactPickerViewControllerDelegate> (null);
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

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            Reload ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            AdoptTheme (Theme.Active);
        }

        public override void Cleanup ()
        {
            // Clean up nav bar
            SearchButton.Clicked -= ShowSearch;
            CloseButton.Clicked -= Close;

            // Clean up search
            if (SearchController != null) {
                SearchController.Delegate = null;
            }
            if (SearchResultsViewController != null) {
                SearchResultsViewController.ContactSelected -= SearchContactSelected;
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

        void Reload ()
        {
            NcTask.Run (() => {
                var contacts = McContact.AllEmailContactsSortedByName ();
                var contactGroups = ContactGroup.CreateGroups (contacts, Cache);
                InvokeOnMainThread (() => {
                    HandleReloadResults (contactGroups);
                });
            }, "ContactsViewController.Reload");
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
            if (SearchController == null) {
                SearchResultsViewController = new ContactSearchResultsViewController () { IsLongLived = true };
                SearchResultsViewController.ContactSelected += SearchContactSelected;
                SearchController = new NachoSearchController (SearchResultsViewController);
                SearchController.Delegate = this;
            }
            SearchResultsViewController.PrepareForSearching ();
            SearchController.PresentOverViewController (this);
        }

        void SearchContactSelected (object sender, McContact contact)
        {
            SelectContact (contact);
        }

        void Close (object sender, EventArgs e)
        {
            var pickerDelegate = PickerDelegate;
            if (pickerDelegate != null) {
                pickerDelegate.ContactPickerDidPickCancel (this);
            }
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

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            var group = ContactGroups [indexPath.Section];
            var contact = group.GetCachedContact (indexPath.Row);
            SelectContact (contact);
        }

        public override void WillDisplay (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {
            base.WillDisplay (tableView, cell, indexPath);
            var themed = cell as ThemeAdopter;
            if (themed != null && adoptedTheme != null) {
                themed.AdoptTheme (adoptedTheme);
            }
        }

        #endregion

        void SelectContact (McContact contact)
        {
            var pickerDelegate = PickerDelegate;
            if (pickerDelegate != null) {
                pickerDelegate.ContactPickerDidPickContact (this, contact);
            }
        }

        public override UIStatusBarStyle PreferredStatusBarStyle ()
        {
            return UIStatusBarStyle.LightContent;
        }

    }
}
