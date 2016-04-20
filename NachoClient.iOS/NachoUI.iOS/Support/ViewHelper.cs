//#define UI_DEBUG

//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using System.Collections.Generic;
using UIKit;
using Foundation;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class ViewHelper
    {
        /// Return the size of a view to the bottom of the parent view.
        public static nfloat FrameSizeToBottom (UIView view)
        {
            nfloat parentHeight;
            if (null == view.Superview) {
                parentHeight = UIScreen.MainScreen.Bounds.Height;
            } else {
                parentHeight = view.Superview.Frame.Height;
            }
            return parentHeight - view.Frame.Y;
        }

        public enum InsetMode {
            TOPLEFT,
            BOTTOMRIGHT,
            BOTH
        };

        public static CGRect InnerFrameWithInset (CGRect outerFrame, nfloat inset,
            InsetMode xMode, InsetMode yMode)
        {
            nfloat x = 0.0f, y = 0.0f;
            nfloat width = outerFrame.Width;
            switch (xMode) {
            case InsetMode.TOPLEFT:
                x = inset;
                width -= inset;
                break;
            case InsetMode.BOTTOMRIGHT:
                x = 0;
                width -= inset;
                break;
            case InsetMode.BOTH:
                x = inset;
                width -= 2 * inset;
                break;
            }

            nfloat height = outerFrame.Height;
            switch (yMode) {
            case InsetMode.TOPLEFT:
                y = inset;
                height -= inset;
                break;
            case InsetMode.BOTTOMRIGHT:
                y = 0;
                height -= inset;
                break;
            case InsetMode.BOTH:
                y = inset;
                height -= 2 * inset;
                break;
            }
            return new CGRect (x, y, NMath.Max (0.0f, width), NMath.Max (0.0f, height));
        }

        public static CGRect InnerFrameWithInset (CGRect outerFrame, nfloat inset)
        {
            return InnerFrameWithInset (outerFrame, inset, InsetMode.BOTH, InsetMode.BOTH);
        }

        public static bool IsZoomed (UIView view)
        {
            if (view.Transform.IsIdentity) {
                return false;
            }
            var tx = view.Transform;
            if (0 != tx.xy || 0 != tx.x0 || 0 != tx.yx || 0 != tx.y0) {
                // A transformation other than zooming.
                return false;
            }
            nfloat ratio = tx.xx / tx.yy;
            if (0.98f > ratio || 1.02f < ratio) {
                // The x-scale and y-scale are not the same.
                return false;
            }
            return true;
        }

        public static nfloat ZoomScale (UIView view)
        {
            if (!IsZoomed (view)) {
                return 1.0f;
            }
            return view.Transform.xx;
        }

        public static string ViewInfo (UIView view, string tagName)
        {
            string result = view.GetType ().Name;
            if (!string.IsNullOrEmpty (tagName)) {
                result += string.Format (" [{0}]", tagName);
            }
            result += string.Format (": Origin {0} Size {1} Tag {2}",
                Util.PrettyPointF (view.Frame.Location), Util.PrettySizeF (view.Frame.Size), view.Tag);
            if (null != view.AccessibilityLabel) {
                result += string.Format (" Label '{0}'", view.AccessibilityLabel);
            }
            if (view is UIScrollView) {
                var scroll = (UIScrollView)view;
                result += string.Format (" Scrollable content: Offset {0} Size {1} ZoomScale {2}",
                    Util.PrettyPointF (scroll.ContentOffset), Util.PrettySizeF (scroll.ContentSize), scroll.ZoomScale);
            }
            if (!view.Transform.IsIdentity) {
                result += string.Format (" Transform {0}", view.Transform.ToString ());
            }
            string text = null;
            if (view is UITextView) {
                text = ((UITextView)view).Text;
            }
            if (view is UILabel) {
                text = ((UILabel)view).Text;
            }
            if (null != text) {
                bool truncated = 10 < text.Length;
                if (truncated) {
                    text = text.Substring (0, 10);
                }
                result += string.Format (" Text '{0}'{1}", text, truncated ? "..." : "");
            }
            return result;
        }

        private static void DumpViews<T> (UIView view, int indentation)
        {
            string tagName = null;
            if (0 != view.Tag) {
                T tag = (T)Enum.Parse (typeof(T), view.Tag.ToString ());
                tagName = Enum.GetName (typeof(T), tag);
                if (string.IsNullOrEmpty (tagName)) {
                    tagName = view.Tag.ToString ();
                }
            }
            string viewInfo = string.Format ("{0}{1}", indentation.ToString ().PadRight (2 + (indentation * 2)), ViewInfo (view, tagName));
            Console.WriteLine (viewInfo);
            foreach (var subview in view.Subviews) {
                if (!subview.Hidden) {
                    DumpViews<T> (subview, indentation + 1);
                }
            }
        }

        public static void DumpViews<T> (UIView view)
        {
            DumpViews <T> (view, 0);
        }

        private static void DumpViewHierarchy (UIView view, int indent)
        {
            string viewInfo = string.Format ("{0}{1}", indent.ToString ().PadRight (2 + (indent * 2)), ViewInfo (view, null));
            Console.WriteLine (viewInfo);
            foreach (var subview in view.Subviews) {
                if (!subview.Hidden) {
                    DumpViewHierarchy (subview, indent + 1);
                }
            }
        }

        public static void DumpViewHierarchy (UIView view)
        {
            DumpViewHierarchy (view, 0);
        }

        public static void DumpViewControllerHierarchy (UIViewController vc)
        {
            List<string> vcList = new List<string> ();
            while (null != vc) {
                vcList.Insert (0, vc.GetType ().Name);
                vc = vc.ParentViewController;
            }
            string output = "\n";
            int indent = 0;
            foreach (var vcName in vcList) {
                for (int n = 0; n < indent; n++) {
                    output += " ";
                }
                output += vcName + "\n";
                indent += 2;
            }
            Console.WriteLine (output);
        }

        public static void SetDebugBorder (UIView view, UIColor color)
        {
            #if (UI_DEBUG)
            view.Layer.BorderColor = color.CGColor;
            view.Layer.BorderWidth = 1.0f;
            #endif
        }

        public static CGSize ScaleSizeF (nfloat scale, CGSize size)
        {
            return new CGSize (scale * size.Width, scale * size.Height);
        }

        public static void DisposeViewHierarchy (UIView view)
        {
            // This code comes from a StackOverflow question, and was provided by
            // Herman Schoenfeld.  I have made some slight modifications.
            // http://stackoverflow.com/questions/25532870/xamarin-ios-memory-leaks-everywhere
            try {
                if (null == view || IntPtr.Zero == view.Handle) {
                    return;
                }
                bool skipDispose = false;
                if (null != view.Subviews) {
                    foreach (var subview in view.Subviews) {
                        try {
                            DisposeViewHierarchy (subview);
                        } catch (Exception e) {
                            Log.Error(Log.LOG_UI, "Exception while disposing of view hierarchy: {0}", e.ToString());
                        }
                    }
                }
                if (view is UIActivityIndicatorView) {
                    var indicatorView = view as UIActivityIndicatorView;
                    if (indicatorView.IsAnimating) {
                        indicatorView.StopAnimating();
                    }
                } else if (view is UITableView) {
                    var tableView = view as UITableView;
                    if (null != tableView.DataSource) {
                        tableView.DataSource.Dispose();
                    }
                    tableView.Source = null;
                    tableView.Delegate = null;
                    tableView.DataSource = null;
                    tableView.WeakDelegate = null;
                    tableView.WeakDataSource = null;
                    if (null != tableView.VisibleCells) {
                        foreach (var cell in tableView.VisibleCells) {
                            DisposeViewHierarchy(cell);
                        }
                    }
                } else if (view is UICollectionView) {
                    // UICollectionViewController with throw if its view is disposed before the controller.
                    skipDispose = true;
                    var collectionView = view as UICollectionView;
                    if (null != collectionView.DataSource) {
                        collectionView.DataSource.Dispose();
                    }
                    collectionView.Source = null;
                    collectionView.Delegate = null;
                    collectionView.DataSource = null;
                    collectionView.WeakDelegate = null;
                    collectionView.WeakDataSource = null;
                    if (null != collectionView.VisibleCells) {
                        foreach (var cell in collectionView.VisibleCells) {
                            DisposeViewHierarchy(cell);
                        }
                    }
                } else if (view is BodyWebView) {
                    (view as BodyWebView).EnqueueAsReusable ();
                    skipDispose = true;
                } else if (view is UIWebView) {
                    var webView = view as UIWebView;
                    if (webView.IsLoading) {
                        webView.StopLoading();
                    }
                    webView.LoadHtmlString(string.Empty, null);
                    webView.Delegate = null;
                    webView.WeakDelegate = null;
                }
                if (null != view.Layer) {
                    view.Layer.RemoveAllAnimations();
                }
                if (!skipDispose) {
                    view.Dispose();
                }
            } catch (Exception e) {
                Log.Error (Log.LOG_UI, "Exception while disposing of view hierarchy: {0}", e.ToString ());
            }
        }
    }

    public class VerticalLayoutCursor
    {
        public delegate bool SubviewFilter (UIView subview);

        protected nfloat YOffset;
        protected UIView ParentView;
        protected nfloat _MaxSubViewWidth;

        public nfloat MaxSubViewWidth {
            get {
                return _MaxSubViewWidth;
            }
        }

        public nfloat MaxWidth {
            get {
                return NMath.Max (_MaxSubViewWidth, ParentView.Frame.Width);
            }
        }

        public nfloat TotalHeight {
            get {
                return YOffset;
            }
        }

        public VerticalLayoutCursor (UIView view)
        {
            YOffset = 0.0f;
            ParentView = view;
        }

        public void AddSpace (nfloat gap)
        {
            YOffset += gap;
        }

        public void LayoutView (UIView subview)
        {
            NcAssert.True (subview.Superview == ParentView);
            subview.SizeToFit ();
            ViewFramer.Create (subview).Y (YOffset);
            YOffset += subview.Frame.Height;
            if (subview.Frame.Width > _MaxSubViewWidth) {
                _MaxSubViewWidth = subview.Frame.Width;
            }
        }

        public void IteratorSubviewsWithFilter (SubviewFilter filter = null)
        {
            for (int n = 0; n < ParentView.Subviews.Length; n++) {
                var v = ParentView.Subviews [n];
                if ((null != filter) && (!filter (v))) {
                    continue;
                }
                LayoutView (v);
            }
            ViewFramer.Create (ParentView)
                .Width (MaxWidth)
                .Height (TotalHeight);
        }
    }

    public class ViewFramer
    {
        protected CGRect Frame;
        protected UIView View;
        protected bool AutoUpdate;

        public static ViewFramer Create (UIView view, bool autoUpdate = true)
        {
            return new ViewFramer (view, autoUpdate);
        }

        protected ViewFramer MayUpdate ()
        {
            if (AutoUpdate) {
                Update ();
            }
            return this;
        }

        public ViewFramer (UIView view, bool autoUpdate = true)
        {
            View = view;
            Frame = View.Frame;
            AutoUpdate = autoUpdate;
        }

        public ViewFramer X (nfloat x)
        {
            Frame.X = x;
            return MayUpdate ();
        }

        public ViewFramer Y (nfloat y)
        {
            Frame.Y = y;
            return MayUpdate ();
        }

        public ViewFramer Height (nfloat height)
        {
            Frame.Height = height;
            return MayUpdate ();
        }

        public ViewFramer Width (nfloat width)
        {
            Frame.Width = width;
            return MayUpdate ();
        }

        public ViewFramer AdjustX (nfloat delta)
        {
            Frame.X += delta;
            return MayUpdate ();
        }

        public ViewFramer AdjustY (nfloat delta)
        {
            Frame.Y += delta;
            return MayUpdate ();
        }

        public ViewFramer AdjustHeight (nfloat delta)
        {
            Frame.Height += delta;
            return MayUpdate ();
        }

        public ViewFramer AdjustWidth (nfloat delta)
        {
            Frame.Width += delta;
            return MayUpdate ();
        }

        public ViewFramer CenterX(nfloat x, nfloat sectionWidth)
        {
            Frame.X = x + (sectionWidth / 2) - (Frame.Width / 2);
            return MayUpdate ();
        }

        public ViewFramer CenterY(nfloat y, nfloat sectionHeight)
        {
            Frame.Y = y + (sectionHeight / 2) - (Frame.Height / 2);
            return MayUpdate ();
        }

        public ViewFramer RightAlignX(nfloat sectionWidth)
        {
            return X (sectionWidth - Frame.Width);
        }

        public ViewFramer Center(nfloat x, nfloat y)
        {
            Frame = new CGRect (x - (Frame.Width / 2), y - (Frame.Height / 2), Frame.Width, Frame.Height);
            return MayUpdate ();
        }

        public ViewFramer Square()
        {
            var length = NMath.Max (Frame.Width, Frame.Height);
            Frame.Width = length;
            Frame.Height = length;
            return MayUpdate ();
        }

        public ViewFramer Size (CGSize size)
        {
            Frame.Width = size.Width;
            Frame.Height = size.Height;
            return MayUpdate ();
        }

        public ViewFramer Update ()
        {
            View.Frame = Frame;
            return this;
        }
    }
}

