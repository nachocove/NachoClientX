//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using UIKit;
using CoreGraphics;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    // A compound scroll view is a scroll view that contains other scroll views
    // 
    // In the mail app there are several places where we want a scrolling view that includes something
    // static like headers, and then includes a UIWebView.  We want the headers and the webview to all scroll
    // together.  But of course UIWebView alread is a UIScrollView, so it takes some trickery to disable the
    // scrolling of UIWebView while also allowing UIWebView to optimize its rendering such that only things on
    // screen are drawn.
    //
    // This class is a generic implementation that supports positioning and adjusting inner scroll views according to
    // their position in the set of views and the scroll position of the outer scroll view.
    public class CompoundScrollView : UIScrollView
    {

        // We'll use a private view to collect all of the inner views
        // It will mange the inner view layout and provides an extra layer of grouping so
        // other views can be added as independent direct subviews of the outer scroll view
        //
        // Most of the work is in this private container view.
        private class CompoundScrollViewContainerView : UIView, IUIScrollViewDelegate
        {

            CompoundScrollView ScrollView {
                get {
                    // Our parent is always a CompoundScrollView, so this property is a convenience to do the cast
                    return Superview as CompoundScrollView;
                }
            }

            List<UIView> CompoundViews;
            bool IsLayingOutSubviews;

            public CompoundScrollViewContainerView (CGRect frame) : base(frame)
            {
                // We keep track of the inner views separately from the Subviews list because
                // sometimes we'll modify the order of Subviews without wanting to adjust the order
                // in which the inner views are itereated.  Our own list maintains the correct iteration order.
                CompoundViews = new List<UIView> (5);
            }

            public static UIScrollView ScrollViewForCompoundScrollView (UIView view)
            {
                // The inner views we add maybe be scroll views...
                if (view is UIScrollView) {
                    return view as UIScrollView;
                }
                // ... or they may be like WKWebView and have a scrollView property
                var propInfo = view.GetType ().GetProperty ("ScrollView");
                if (propInfo != null) {
                    return propInfo.GetValue (view) as UIScrollView;
                }
                // ... or they may not be scrolling views at all
                return null;
            }

            public void AddCompoundView (UIView view)
            {
                CompoundViews.Add (view);
                AddSubview (view);
                PrepareCompoundView (view);
            }

            public void InsertCompoundViewBelow (UIView view, UIView sibling)
            {
                var index = CompoundViews.IndexOf (sibling);
                CompoundViews.Insert (index, view);
                InsertSubviewBelow (view, sibling);
                PrepareCompoundView (view);
            }

            void PrepareCompoundView (UIView view)
            {
                UIScrollView scrollView = ScrollViewForCompoundScrollView (view);
                if (scrollView != null){
                    // Turn off scrolling if the inner view is a scrolling view
                    scrollView.ScrollEnabled = false;
                    // Also become its delegate, which we use for zooming purposes
                    scrollView.Delegate = this;
                }
            }

            public void RemoveCompoundView (UIView view)
            {
                if (CompoundViews != null){
                    CompoundViews.Remove (view);
                }
                UIScrollView scrollView = ScrollViewForCompoundScrollView (view);
                if (scrollView != null) {
                    // reverse the changes made when adding the view
                    scrollView.Delegate = null;
                    scrollView.ScrollEnabled = true;
                }
                view.RemoveFromSuperview ();
                SetNeedsLayout ();
            }

            public void DetermineContentSize ()
            {
                var size = new CGSize (0.0f, 0.0f);
                foreach (var subview in CompoundViews) {
                    if (!subview.Hidden) {
                        var scrollView = ScrollViewForCompoundScrollView (subview);
                        if (scrollView != null) {
                            size.Height += scrollView.ContentSize.Height;
                            if (scrollView.ContentSize.Width > size.Width) {
                                size.Width = scrollView.ContentSize.Width;
                            }
                        } else {
                            size.Height += subview.Frame.Height;
                            if (subview.Frame.Width > size.Width) {
                                size.Width = subview.Frame.Width;
                            }
                        }
                    }
                }
                ScrollView.ContentSize = size;
            }

            public override void LayoutSubviews ()
            {
                // The basic idea here is to loop through our inner views and lay them out in a vertical stack,
                // taking care to size scrolling views at maximum the same size as us and at minimum 0.
                // Sizing scrolling views is critical to allow them to efficiently manage their contents.
                // For example, UITableView only shows the rows that fit within its size, UIWebView only draws
                // the content within its size, etc.
                nfloat contentHeight;
                nfloat y = 0.0f;
                int i = 0;
                CGRect frame;
                IsLayingOutSubviews = true;
                foreach (UIView subview in CompoundViews){
                    UIScrollView scrollView = ScrollViewForCompoundScrollView (subview);
                    if (subview.Hidden) {
                        contentHeight = 0;
                    }else if (scrollView != null){
                        contentHeight = scrollView.ContentSize.Height;
                        // Start with a default frame that assumes the inner view is out of bounds and therefore gets 0 height
                        frame = new CGRect(Math.Min(Math.Max(0, Bounds.X), Math.Max(0, scrollView.ContentSize.Width - Bounds.Width)), y, Bounds.Width, 0);
                        if (y + contentHeight > Bounds.Y && y < Bounds.Y + Bounds.Height){
                            // If we're in bounds, cap the frame's top at the top boundary
                            if (y < Bounds.Y){
                                frame.Y = Bounds.Y;
                            }else{
                                frame.Y = y;
                            }
                            // Cap the frame's bottom at the bottom boundary, and make sure the final view extends there regardless
                            nfloat minBoundsY = 0.0f;
                            nfloat maxBoundsY = (nfloat)Math.Max (Bounds.Height, y + contentHeight) - Bounds.Height;
                            nfloat boundsYWithoutBounce = (nfloat)Math.Max(minBoundsY, Math.Min(maxBoundsY, Bounds.Y));
                            nfloat availableHeight = boundsYWithoutBounce + Bounds.Height - frame.Y;
                            if (i == CompoundViews.Count - 1){
                                frame.Height = availableHeight;
                            }else{
                                frame.Height = (nfloat)Math.Min(contentHeight, availableHeight);
                            }
                            // Adjust the inner scroll view's offset so it lines up with where it should be
                            CGPoint p = new CGPoint(frame.X, Math.Max(0, Bounds.Y - y));
                            scrollView.ContentOffset = p;
                        }
                        subview.Frame = frame;
                    }else{
                        // If we aren't dealing with a scrolling view, don't change its size or X position, just make sure
                        // it is stacked at the proper Y position.
                        subview.Frame = new CGRect(subview.Frame.X, y, subview.Frame.Width, subview.Frame.Height);
                        contentHeight = subview.Frame.Height;
                    }
                    y += contentHeight;
                    ++i;
                }
                IsLayingOutSubviews = false;
            }

            [Foundation.Export("scrollViewDidScroll:")]
            public void Scrolled (UIScrollView scrollView)
            {
                if (!IsLayingOutSubviews) {
                    // If we see a scroll from one of our subview, and we didn't cause it,
                    // then we need to adjust the outer view accordingly.
                    // In general, the subviews should not be scrollable.  In fact, we disable scrolling.
                    // However, when we have an editiable webview, say, text insertion/deletion/selection
                    // can all adjust the view's contentOffset.
                    // The basic strategy used here to is to figure out where the we expect the view's offset to be,
                    // and compare to where it is.  If there's a difference, we adjust the outer view by the same
                    // amount (which, in turn, will cause us to re-layout).
                    if (scrollView.ContentOffset.Y >= 0) {
                        UIView compoundView = scrollView;
                        while (compoundView.Superview != this) {
                            compoundView = compoundView.Superview;
                        }
                        nfloat y = 0.0f;
                        foreach (var subview in CompoundViews) {
                            if (subview == compoundView) {
                                break;
                            }
                            var subviewScrollView = ScrollViewForCompoundScrollView (subview);
                            if (subviewScrollView != null) {
                                y += subviewScrollView.ContentSize.Height;
                            } else {
                                y += subview.Frame.Height;
                            }
                        }
                        CGPoint expectedOffset = new CGPoint(compoundView.Frame.X, Math.Max(0, Bounds.Y - y));
                        CGPoint diff = new CGPoint (scrollView.ContentOffset.X - expectedOffset.X, scrollView.ContentOffset.Y - expectedOffset.Y);
                        if (diff.X != 0.0f || diff.Y != 0.0f) {
                            DetermineContentSize ();
                            CGPoint newOuterOffset = new CGPoint (ScrollView.ContentOffset.X + diff.X, ScrollView.ContentOffset.Y + diff.Y);
                            ScrollView.ContentOffset = newOuterOffset;
                            ScrollView.SetNeedsLayout ();
                            ScrollView.LayoutIfNeeded ();
                        }
                    }
                }
            }

            [Foundation.Export ("scrollViewDidEndZooming:withView:atScale:")]
            public void ZoomingEnded (UIKit.UIScrollView theScrollView, UIKit.UIView withView, System.nfloat atScale)
            {
                DetermineContentSize ();
                ScrollView.SetNeedsLayout ();
                ScrollView.LayoutIfNeeded ();
            }

        }


        CompoundScrollViewContainerView _CompoundContainerView;
        CompoundScrollViewContainerView CompoundContainerView {
            get {
                if (_CompoundContainerView == null) {
                    _CompoundContainerView = new CompoundScrollViewContainerView (Bounds);
                    AddSubview (CompoundContainerView);
                }
                return _CompoundContainerView;
            }
        }

        public CompoundScrollView (CGRect frame) : base(frame)
        {
        }

        public void AddCompoundView (UIView view)
        {
            CompoundContainerView.AddCompoundView (view);
        }

        public void InsertCompoundViewBelow (UIView view, UIView sibling)
        {
            CompoundContainerView.InsertCompoundViewBelow (view, sibling);
        }

        public void DetermineContentSize ()
        {
            CompoundContainerView.DetermineContentSize ();
        }

        public void RemoveCompoundView (UIView view)
        {
            CompoundContainerView.RemoveCompoundView (view);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            CompoundContainerView.Frame = Bounds;
            CompoundContainerView.Bounds = new CGRect(ContentOffset.X, ContentOffset.Y, Bounds.Width, Bounds.Height);
            CompoundContainerView.SetNeedsLayout ();
            CompoundContainerView.LayoutIfNeeded ();
        }


    }
}

