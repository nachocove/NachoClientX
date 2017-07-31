//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{

    public interface NachoSearchControllerDelegate {
        
        void DidChangeSearchText (NachoSearchController searchController, string text);
        void DidSelectSearch (NachoSearchController searchController);
        void DidEndSearch (NachoSearchController searchController);

    }

    public class NachoSearchController : NSObject, IUISearchBarDelegate
    {

        public NachoSearchControllerDelegate Delegate;
        public readonly UISearchBar SearchBar;
        public readonly UIViewController SearchResultsController;
        UIView View;
        UIView BackgroundView;
        UINavigationBar NavigationBar;
        nfloat SearchBarInset = 7.0f;
        UITapGestureRecognizer BackgroundTapRecognizer;

        public NachoSearchController (UIViewController searchResultsController)
        {
            SearchResultsController = searchResultsController;
            View = new UIView (new CGRect (0.0f, 0.0f, 320.0f, 320.0f));
            View.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            BackgroundView = new UIView (View.Bounds);
            BackgroundView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            BackgroundView.BackgroundColor = UIColor.Black.ColorWithAlpha (0.4f);
            View.AddSubview (BackgroundView);

            NavigationBar = new UINavigationBar ();
            NavigationBar.Frame = new CGRect (0.0f, 0.0f, View.Bounds.Width, 64.0f);
            NavigationBar.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            NavigationBar.Translucent = false;
            View.AddSubview (NavigationBar);
            SearchBar = new UISearchBar ();
            SearchBar.WeakDelegate = this;
            SearchBar.ShowsCancelButton = true;
            SearchBar.Frame = new CGRect (SearchBarInset, 20.0f, NavigationBar.Bounds.Width - SearchBarInset, 44.0f);
            SearchBar.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            SearchBar.Placeholder = NSBundle.MainBundle.LocalizedString ("Search", "");
            NavigationBar.AddSubview (SearchBar);

            BackgroundTapRecognizer = new UITapGestureRecognizer (ViewTap);
            BackgroundView.AddGestureRecognizer (BackgroundTapRecognizer);
        }

        public void Cleanup ()
        {
            BackgroundView.RemoveGestureRecognizer (BackgroundTapRecognizer);
            BackgroundTapRecognizer = null;
            SearchBar.WeakDelegate = null;
        }

        void ViewTap ()
        {
            Dismiss ();
        }

        public void PresentOverViewController (UIViewController parentViewController)
        {
            var parentNavController = parentViewController.NavigationController;
            View.Frame = parentNavController.View.Bounds;
            View.Alpha = 0.0f;
            parentNavController.View.AddSubview(View);
            SearchBar.BecomeFirstResponder ();
            UIView.Animate (0.25f, () => {
                View.Alpha = 1.0f;
            }, () => {
                AdjustViewForBarShown (parentNavController.TopViewController.View);
                SearchResultsController.RemoveFromParentViewController ();
                parentViewController.View.AddSubview(View);
                parentViewController.AddChildViewController(SearchResultsController);
                View.Frame = parentViewController.View.Bounds;
                parentNavController.SetNavigationBarHidden(true, false);
            });
        }

        void AdjustViewForBarShown (UIView view)
        {
            var scrollView = view as UIScrollView;
            var barHeight = NavigationBar.Frame.Y + NavigationBar.Frame.Height;
            if (scrollView != null) {
                scrollView.ContentInset = new UIEdgeInsets (barHeight, 0.0f, 0.0f, scrollView.ContentInset.Bottom);
            }else{
                foreach (var subview in view.Subviews) {
                    subview.Center = new CGPoint (subview.Center.X, subview.Center.Y + barHeight);
                }
            }
        }

        void AdjustViewForBarHidden (UIView view)
        {
            var scrollView = view as UIScrollView;
            var barHeight = NavigationBar.Frame.Y + NavigationBar.Frame.Height;
            if (scrollView != null) {
                scrollView.ContentInset = new UIEdgeInsets (0.0f, 0.0f, 0.0f, scrollView.ContentInset.Bottom);
            }else{
                foreach (var subview in view.Subviews) {
                    subview.Center = new CGPoint (subview.Center.X, subview.Center.Y - barHeight);
                }
            }
        }

        void Dismiss ()
        {
            var navController = SearchResultsController.NavigationController;
            SearchResultsController.RemoveFromParentViewController ();
            navController.View.AddSubview(View);
            View.Frame = navController.View.Bounds;
            AdjustViewForBarHidden (navController.TopViewController.View);
            navController.SetNavigationBarHidden(false, false);
            UIView.Transition (View.Superview, 0.25f, UIViewAnimationOptions.TransitionCrossDissolve, () => {
                View.RemoveFromSuperview ();
            }, () => {
                SearchBar.Text = "";
                if (SearchResultsController.View.Superview != null) {
                    SearchResultsController.View.RemoveFromSuperview ();
                }
                Delegate.DidEndSearch (this);
                SearchResultsController.RemoveFromParentViewController ();
            });
        }

        [Export ("searchBar:textDidChange:")]
        public void TextChanged (UIKit.UISearchBar searchBar, string searchText)
        {
            if (String.IsNullOrEmpty (searchText)) {
                if (SearchResultsController.View.Superview != null) {
                    SearchResultsController.View.RemoveFromSuperview ();
                }
            } else {
                if (SearchResultsController.View.Superview == null) {
                    SearchResultsController.View.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
                    SearchResultsController.View.Frame = new CGRect (0.0f, NavigationBar.Frame.Height, View.Bounds.Width, View.Bounds.Height - NavigationBar.Frame.Height);
                    View.AddSubview (SearchResultsController.View);
                }
            }
            Delegate.DidChangeSearchText (this, searchText);
        }

        [Export ("searchBarCancelButtonClicked:")]
        public void CancelButtonClicked (UIKit.UISearchBar searchBar)
        {
            Dismiss ();
        }

        [Export ("searchBarSearchButtonClicked:")]
        public void SearchButtonClicked (UISearchBar searchBar)
        {
            Delegate.DidSelectSearch (this);
        }

    }
}

