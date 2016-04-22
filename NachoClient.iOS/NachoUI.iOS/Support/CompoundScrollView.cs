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

            UIView ZoomingView;
//            CGPoint ZoomingViewOffsetAtZoomStart;
//            CGPoint ScrollViewOffsetAtZoomStart;
//            int ZoomingViewLevel;
            List<UIView> CompoundViews;
            bool IsLayingOutSubviews;

            public CompoundScrollViewContainerView (CGRect frame) : base(frame)
            {
                ZoomingView = null;
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
                // We keep track of the inner views separately from the Subviews list because
                // sometimes we'll modify the order of Subviews without wanting to adjust the order
                // in which the inner views are itereated.  Our own list maintains the correct iteration order.
                if (CompoundViews == null){
                    CompoundViews = new List<UIView> (5);
                }
                CompoundViews.Add (view);
                AddSubview (view);
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
                view.RemoveFromSuperview ();
                SetNeedsLayout ();
            }

            public void DetermineContentSize ()
            {
                var size = new CGSize (Bounds.Width, 0.0f);
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
                        if (subview != ZoomingView){
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
                            // Special case for when we're zooming an inner view, make it take up the entire bounds
                            // because otherwise we'd need to adjust its position/contentOffset as it zooms, and that
                            // always ends up being a very rough transition.  Better to make it the max size and not adjust
                            // it while zooming
                            subview.Frame = Bounds;
                        }
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

            [Foundation.Export ("scrollViewWillBeginZooming:withView:")]
            public void ZoomingStarted (UIKit.UIScrollView scrollView, UIKit.UIView view)
            {
                // FIXME: adding the Scrolled method above possibly broke this zooming code...needs testing;
                //        likely solution is to ignore scrolling while zooming
                // The idea here is to position the zooming view to the maximum size (our bounds),
                // at the start of zooming so we don't have reposition/resize it while we're zooming because
                // that causing a lot of jumpiness.
                //
                // We'll also record where we started offset-wise in the inner view, so we can keep track
                // of how much it changed on its own, and update the outer offset accordingly so all the inner views
                // appear to move in the right way.
                //
                // Note that at least with WKWebView there is a jump at the start of zooming if the web view isn't already
                // at our bounds.  The jump doesn't appear to be from this code, but rather from the first move after this
                // code runs.
//                UIView zoomingView = scrollView;
//                while (zoomingView.Superview != this){
//                    zoomingView = zoomingView.Superview;
//                }
//                ZoomingView = zoomingView;
//                ZoomingViewLevel = 0;
//                for (int i = 0; i < Subviews.Length; ++i) {
//                    if (Subviews [i] == ZoomingView) {
//                        ZoomingViewLevel = i;
//                        break;
//                    }
//                }
//                SendSubviewToBack (ZoomingView);
//                nfloat yDiff = ZoomingView.Frame.Y - Bounds.Y;
//                if (yDiff > 0){
//                    ZoomingView.Frame = Bounds;
//                    scrollView.ContentOffset = new CGPoint(scrollView.ContentOffset.X, scrollView.ContentOffset.Y - yDiff);
//                }
//                ZoomingViewOffsetAtZoomStart = scrollView.ContentOffset;
//                ScrollViewOffsetAtZoomStart = ScrollView.ContentOffset;
            }

            [Foundation.Export ("scrollViewDidZoom:")]
            public void DidZoom (UIKit.UIScrollView scrollView)
            {
                // While an inner view is zooming, its contentOffset is changing.  We need to adjust the outer content offset
                // by the same amount so we don't get out of sync and so the other inner views can move accordingly.
//                CGPoint diff = new CGPoint(scrollView.ContentOffset.X - ZoomingViewOffsetAtZoomStart.X, scrollView.ContentOffset.Y - ZoomingViewOffsetAtZoomStart.Y);
//                CGPoint offset = ScrollViewOffsetAtZoomStart;
//                offset.X += diff.X;
//                offset.Y += diff.Y;
//                ScrollView.ContentOffset = offset;
//                ScrollView.LayoutIfNeeded();
//                LayoutIfNeeded ();
            }

            [Foundation.Export ("scrollViewDidEndZooming:withView:atScale:")]
            public void ZoomingEnded (UIKit.UIScrollView theScrollView, UIKit.UIView withView, System.nfloat atScale)
            {
                // Put the zooming view back at the proper level
//                InsertSubview (ZoomingView, ZoomingViewLevel);
//                ZoomingView = null;
//                SetNeedsLayout ();
//                // Update our content size now that a view has zoomed and takes up more/less space in both directions
//                CGSize contentSize = new CGSize(Bounds.Width, 0);
//                foreach (UIView subview in CompoundViews){
//                    UIScrollView scrollView = ScrollViewForCompoundScrollView (subview);
//                    if (scrollView != null){
//                        if (scrollView.ContentSize.Width > contentSize.Width){
//                            contentSize.Width = scrollView.ContentSize.Width;
//                        }
//                        contentSize.Height += scrollView.ContentSize.Height;
//                    }else{
//                        if (subview.Frame.Width > contentSize.Width){
//                            contentSize.Width = subview.Frame.Width;
//                        }
//                        contentSize.Height += subview.Frame.Height;
//                    }
//                }
//                ScrollView.ContentSize = contentSize;
            }

        }


        CompoundScrollViewContainerView CompoundContainerView;

        public CompoundScrollView (CGRect frame) : base(frame)
        {
        }

        public void AddCompoundView (UIView view)
        {
            if (CompoundContainerView == null){
                CompoundContainerView = new CompoundScrollViewContainerView (Bounds);
                AddSubview (CompoundContainerView);
            }
            CompoundContainerView.AddCompoundView (view);
        }

        public void DetermineContentSize ()
        {
            CompoundContainerView.DetermineContentSize ();
        }

        public void RemoveCompoundView (UIView view)
        {
            if (CompoundContainerView != null) {
                CompoundContainerView.RemoveCompoundView (view);
            }
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

