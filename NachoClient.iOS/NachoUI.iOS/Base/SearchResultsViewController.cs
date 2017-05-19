//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using UIKit;

namespace NachoClient.iOS
{

    public class SearchResultsViewController : NachoTableViewController, ThemeAdopter
    {

        NSObject KeyboardWillShowNotificationToken;
        NSObject KeyboardWillHideNotificationToken;

        public SearchResultsViewController () : base (UITableViewStyle.Plain)
        {
        }

        Theme adoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (theme != adoptedTheme) {
                adoptedTheme = theme;
                TableView.TintColor = theme.TableViewTintColor;
                TableView.AdoptTheme (theme);
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (!NavigationController.NavigationBarHidden) {
                NavigationController.SetNavigationBarHidden (true, true);
            }
            KeyboardWillShowNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, KeyboardWillShow);
            KeyboardWillHideNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, KeyboardWillHide);
            AdoptTheme (Theme.Active);
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            if (NcKeyboardSpy.Instance.keyboardShowing) {
                AdjustInsetsForKeyboard ();
            }
        }

        public override void ViewDidDisappear (bool animated)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver (KeyboardWillShowNotificationToken);
            NSNotificationCenter.DefaultCenter.RemoveObserver (KeyboardWillHideNotificationToken);
            base.ViewDidDisappear (animated);
        }

        void AdjustInsetsForKeyboard ()
        {
            nfloat keyboardHeight = NcKeyboardSpy.Instance.KeyboardHeightInView (View);
            TableView.ContentInset = new UIEdgeInsets (TableView.ContentInset.Top, 0.0f, keyboardHeight, 0.0f);
            TableView.ScrollIndicatorInsets = new UIEdgeInsets (TableView.ScrollIndicatorInsets.Top, TableView.ScrollIndicatorInsets.Left, keyboardHeight, TableView.ScrollIndicatorInsets.Right);
        }

        void KeyboardWillShow (NSNotification notification)
        {
            if (IsViewLoaded && View.Window != null) {
                AdjustInsetsForKeyboard ();
            }
        }

        void KeyboardWillHide (NSNotification notification)
        {
            if (IsViewLoaded) {
                AdjustInsetsForKeyboard ();
            }
        }

        public override void WillDisplay (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {
            base.WillDisplay (tableView, cell, indexPath);
            var themed = cell as ThemeAdopter;
            if (themed != null && adoptedTheme != null) {
                themed.AdoptTheme (adoptedTheme);
            }
        }
    }
}
