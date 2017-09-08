//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

using Foundation;
using CoreGraphics;
using UIKit;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    public interface ContactAutocompleteMenuDelegate
    {
        void AutocompleteMenuDidAppear (ContactAutocompleteMenu menu);
        void AutocompleteMenuDidDisappear (ContactAutocompleteMenu menu);
        void AutocompleteMenuDidSelect (ContactAutocompleteMenu menu, McContactEmailAddressAttribute contactAddress);
    }

    public class ContactAutocompleteMenu : UIView, IUITableViewDelegate, IUITableViewDataSource, ThemeAdopter
    {

        private const string ContactCellIdentifier = "contact";

        UITableView TableView;
        private WeakReference<ContactAutocompleteMenuDelegate> WeakAutocompleteDelegate = new WeakReference<ContactAutocompleteMenuDelegate> (null);
        public ContactAutocompleteMenuDelegate AutocompleteDelegate {
            get {
                if (WeakAutocompleteDelegate.TryGetTarget (out var autocompleteDelegate)) {
                    return autocompleteDelegate;
                }
                return null;
            }
            set {
                WeakAutocompleteDelegate.SetTarget (value);
            }
        }

        EmailAutocompleteSearcher Searcher;
        EmailAutocompleteSearchResults Results;

        public ContactAutocompleteMenu () : base ()
        {
            TableView = new UITableView (Bounds, UITableViewStyle.Plain);
            TableView.WeakDelegate = this;
            TableView.WeakDataSource = this;
            TableView.RegisterClassForCellReuse (typeof (ContactCell), ContactCellIdentifier);
            TableView.SeparatorInset = new UIEdgeInsets (0.0f, 54.0f, 0.0f, 0.0f);
            AddSubview (TableView);

            Searcher = new EmailAutocompleteSearcher ();
            Searcher.ResultsFound += UpdateResults;

            Hidden = true;
        }

        Theme AdoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (theme != AdoptedTheme) {
                AdoptedTheme = theme;
                TableView.AdoptTheme (theme);
            }
        }

        public override void LayoutSubviews ()
        {
            TableView.Frame = Bounds;
        }

        public void Update (string text)
        {
            Search (text);
            if (string.IsNullOrEmpty (text)) {
                Hide ();
            }
        }

        public void Search (string text)
        {
            Searcher.Search (text);
        }

        public void UpdateResults (object sender, EmailAutocompleteSearchResults results)
        {
            Results = results;
            TableView.ReloadData ();
            if (Hidden && (Results?.EmailAttributes.Length ?? 0) > 0) {
                Hidden = false;
                AutocompleteDelegate?.AutocompleteMenuDidAppear (this);
            } else if (!Hidden && (Results?.EmailAttributes.Length ?? 0) == 0) {
                Hidden = true;
                AutocompleteDelegate?.AutocompleteMenuDidDisappear (this);
            }
        }

        public void Hide ()
        {
            Results = null;
            TableView.ReloadData ();
            Hidden = true;
            AutocompleteDelegate?.AutocompleteMenuDidDisappear (this);
        }

        [Export ("numberOfSectionsInTableView:")]
        public nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        [Foundation.Export ("tableView:numberOfRowsInSection:")]
        public nint RowsInSection (UITableView tableView, nint section)
        {
            return Results?.EmailAttributes.Length ?? 0;
        }

        [Foundation.Export ("tableView:cellForRowAtIndexPath:")]
        public UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            var contact = Results.EmailAttributes [indexPath.Row];
            var cell = tableView.DequeueReusableCell (ContactCellIdentifier) as ContactCell;
            cell.SetContact (contact);
            cell.AdoptTheme (AdoptedTheme);
            return cell;
        }

        [Foundation.Export ("tableView:didSelectRowAtIndexPath:")]
        public void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            var contactAddress = Results.EmailAttributes [indexPath.Row];
            tableView.DeselectRow (indexPath, true);
            AutocompleteDelegate?.AutocompleteMenuDidSelect (this, contactAddress);
        }

        class ContactCell : SwipeTableViewCell, ThemeAdopter
        {

            PortraitView PortraitView;
            nfloat PortraitSize = 30.0f;

            public ContactCell (IntPtr handle) : base (handle)
            {
                PortraitView = new PortraitView (new CGRect (0.0f, 0.0f, PortraitSize, PortraitSize));
                ContentView.AddSubview (PortraitView);
                HideDetailWhenEmpty = true;
            }

            public void SetContact (McContactEmailAddressAttribute contactAddress)
            {
                var displayName = contactAddress.CachedContact.GetDisplayName ();
                var emailAddress = contactAddress.CachedAddress.CanonicalEmailAddress;
                if (string.IsNullOrWhiteSpace (displayName) || string.Compare (displayName, emailAddress, StringComparison.OrdinalIgnoreCase) == 0) {
                    TextLabel.Text = emailAddress;
                    DetailTextLabel.Text = "";
                } else {
                    TextLabel.Text = displayName;
                    DetailTextLabel.Text = emailAddress;
                }
                PortraitView.SetPortrait (contactAddress.CachedContact.PortraitId, contactAddress.CachedContact.CircleColor, contactAddress.CachedContact.Initials);
            }

            public void AdoptTheme (Theme theme)
            {
                TextLabel.Font = theme.BoldDefaultFont.WithSize (14.0f);
                TextLabel.TextColor = theme.TableViewCellMainLabelTextColor;
                DetailTextLabel.Font = theme.DefaultFont.WithSize (14.0f);
                DetailTextLabel.TextColor = theme.TableViewCellDetailLabelTextColor;
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                PortraitView.Center = new CGPoint (SeparatorInset.Left / 2.0f, ContentView.Bounds.Height / 2.0f);
            }

        }

    }
}
