﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using MonoTouch.CoreGraphics;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class ViewHelper
    {
        /// Return the size of a view to the bottom of the parent view.
        public static float FrameSizeToBottom (UIView view)
        {
            float parentHeight;
            if (null == view.Superview) {
                parentHeight = UIScreen.MainScreen.Bounds.Height;
            } else {
                parentHeight = view.Superview.Frame.Height;
            }
            return parentHeight - view.Frame.Y;
        }

        private static void DumpView<T> (UIView view, int indentation)
        {
            string msg = "";
            for (int n = 0; n < indentation; n++) {
                msg += " ";
            }
            T tag = (T)Enum.Parse (typeof(T), view.Tag.ToString ());
            string tagName = Enum.GetName (typeof (T), tag);
            if (String.IsNullOrEmpty (tagName)) {
                tagName = view.Tag.ToString ();
            }
            msg += String.Format ("{0} [{1}]: (X,Y)=({2}, {3})  (Width, Height)=({4}, {5})",
                view.GetType ().Name, tagName,
                view.Frame.X + view.Superview.Frame.X,
                view.Frame.Y + view.Superview.Frame.Y,
                view.Frame.Width, view.Frame.Height);
            Console.WriteLine (msg);
            for (int n = 0; n < view.Subviews.Length; n++) {
                var v = view.Subviews [n];
                if (v.Hidden) {
                    continue;
                }
                DumpView <T> (v, indentation + 2);
            }
        }

        public static void DumpViews<T> (UIView view)
        {
            DumpView <T> (view, 0);
        }
    }

    public class VerticalLayoutCursor
    {
        public delegate bool SubviewFilter (UIView subview);

        protected float YOffset;
        protected UIView ParentView;
        protected float _MaxSubViewWidth;

        public float MaxSubViewWidth {
            get {
                return _MaxSubViewWidth;
            }
        }

        public float MaxWidth {
            get {
                return Math.Max (_MaxSubViewWidth, ParentView.Frame.Width);
            }
        }

        public float TotalHeight {
            get {
                return YOffset;
            }
        }

        public VerticalLayoutCursor (UIView view)
        {
            YOffset = 0.0f;
            ParentView = view;
        }

        public void AddSpace (float gap)
        {
            YOffset += gap;
        }

        public void LayoutView (UIView subview)
        {
            NcAssert.True (subview.Superview == ParentView);
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
        protected RectangleF Frame;
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

        public ViewFramer X (float x)
        {
            Frame.X = x;
            return MayUpdate ();
        }

        public ViewFramer Y (float y)
        {
            Frame.Y = y;
            return MayUpdate ();
        }

        public ViewFramer Height (float height)
        {
            Frame.Height = height;
            return MayUpdate ();
        }

        public ViewFramer Width (float width)
        {
            Frame.Width = width;
            return MayUpdate ();
        }

        public ViewFramer AdjustX (float delta)
        {
            Frame.X += delta;
            return MayUpdate ();
        }

        public ViewFramer AdjustY (float delta)
        {
            Frame.Y += delta;
            return MayUpdate ();
        }

        public ViewFramer AdjustHeight (float delta)
        {
            Frame.Height += delta;
            return MayUpdate ();
        }

        public ViewFramer AdjustWidth (float delta)
        {
            Frame.Width += delta;
            return MayUpdate ();
        }

        public ViewFramer Update ()
        {
            View.Frame = Frame;
            return this;
        }
    }
}

