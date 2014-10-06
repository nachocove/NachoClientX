//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using System.Collections.Generic;

using MonoTouch.UIKit;
using MonoTouch.CoreGraphics;
using MonoTouch.Foundation;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public enum SwipeActionViewTagType {
        SWIPE_ACTION_VIEW,
        SWIPE_ACTION_BUTTON,
        SWIPE_ACTION_SWIPING_VIEW
    };

    /// <summary>
    /// Swipe action descriptor is a configuration object used for configuring the appearance of a swipe action.
    /// Image and color are optional. If color is omitted, there will be a default sequence of left and
    /// right action colors assigned.
    /// </summary>
    public class SwipeActionDescriptor
    {
        public float WidthPercentage { get; protected set; }
        public UIImage Image { get; protected set; }
        public string Text { get; protected set; }
        public UIColor Color { get; protected set; }

        public SwipeActionDescriptor (float widthPercentage, UIImage image, string text, UIColor color = null)
        {
            NcAssert.True (0.25 >= widthPercentage); // cannot have button wider than 25% of the parent view
            WidthPercentage = widthPercentage;
            Image = image;
            Text = text;
            Color = color;
        }
    }

    public class SwipeActionButton : UIButton
    {
        public SwipeActionDescriptor Config { get; protected set; }

        public SwipeActionButton (SwipeActionDescriptor descriptor, UIView parentView)
        {
            Tag = (int)SwipeActionViewTagType.SWIPE_ACTION_BUTTON;
            Config = descriptor;
            if (null != Config.Image) {
                UIImage image = Config.Image;
                if (UIImageRenderingMode.AlwaysTemplate != image.RenderingMode) {
                    // Make sure it is a template so we can use tint to match the icon color to that of the title
                    image = image.ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate);
                }
                SetImage (image, UIControlState.Normal);
                ImageView.TintColor = UIColor.White;
            }
            SetTitle (Config.Text, UIControlState.Normal);
            BackgroundColor = Config.Color;
            Enabled = true;

            // Move the image on top of the title and center it
            SizeToFit ();
            float deltaX = Frame.Width - ImageView.Frame.X - ImageView.Frame.Width;
            float deltaY = (ImageView.Frame.Height / 2.0f) + 1.0f;
            ImageEdgeInsets = new UIEdgeInsets (-deltaY, -ImageView.Frame.X, deltaY, -deltaX);
            deltaX = Frame.Width - TitleLabel.Frame.X - TitleLabel.Frame.Width;
            deltaY = (TitleLabel.Frame.Height / 2.0f) + 1.0f;
            TitleEdgeInsets = new UIEdgeInsets (deltaY, -TitleLabel.Frame.X, -deltaY, -deltaX);

            ViewFramer.Create (this)
                .X (0)
                .Y (0)
                .Width ((float)Math.Round (parentView.Frame.Width * descriptor.WidthPercentage))
                .Height (parentView.Frame.Height);
        }
    }

    public delegate void SwipeActionButtonListCallback (SwipeActionButton actionButton);

    public class SwipeActionButtonList : List<SwipeActionButton>
    {
        public SwipeActionButtonList () : base ()
        {
        }

        public void IteratorForward (SwipeActionButtonListCallback callback, int startIndex, int stopIndex = -1)
        {
            if (-1 == stopIndex) {
                stopIndex = Count;
            }
            for (int i = startIndex; i < stopIndex; i++) {
                callback (this [i]);
            }
        }
    }

    /// <summary>
    /// Swipe action swiping view is used when user starts to swipe on the view.
    /// At that point, this view takes a screen shot of the swipe action view put it
    /// here 
    /// </summary>
    public class SwipeActionSwipingView : UIImageView
    {
        float[] leftOffsets;
        float[] rightOffsets;

        float maxLeftPercentage;
        float maxRightPercentage;

        SwipeActionButtonList leftActionButtons;
        SwipeActionButtonList rightActionButtons;
        UIView snapshotView;

        public float LastMovePercentage { get; protected set; }

        public float FullMovePercentage {
            get {
                return GetFullMovePercentage (LastMovePercentage);
            }
        }

        public SwipeActionSwipingView (UIView view,
            SwipeActionButtonList leftButtons, SwipeActionButtonList rightButtons) : base (view.Frame)
        {
            Tag = (int)SwipeActionViewTagType.SWIPE_ACTION_SWIPING_VIEW;

            ClipsToBounds = true;
            leftActionButtons = leftButtons;
            rightActionButtons = rightButtons;
            LastMovePercentage = 0.0f;

            // Take a snapshot of the original view
            snapshotView = view.SnapshotView (false);
            AddSubview (snapshotView);

            // Stack the buttons on top of the image view
            foreach (var button in leftActionButtons) {
                NcAssert.True (button.Frame.Height == view.Frame.Height);
                if (null != button.Superview) {
                    button.RemoveFromSuperview ();
                }
                ViewFramer.Create (button).X (-button.Frame.Width).Y (0);
                AddSubview (button);
            }
            foreach (var button in rightActionButtons) {
                NcAssert.True (button.Frame.Height == view.Frame.Height);
                if (null != button.Superview) {
                    button.RemoveFromSuperview ();
                }
                ViewFramer.Create (button).X (Frame.Width).Y (0);
                AddSubview (button);
            }

            // Compute the amount of shift required for each button
            maxRightPercentage = 0.0f;
            leftOffsets = new float[leftActionButtons.Count];
            float total = 0.0f;
            for (int i = leftActionButtons.Count - 1; i >= 0; i--) {
                total += leftActionButtons [i].Frame.Width;
                leftOffsets [i] = total;
                maxRightPercentage += leftActionButtons [i].Config.WidthPercentage;
            }

            maxLeftPercentage = 0.0f;
            rightOffsets = new float[rightActionButtons.Count];
            total = 0.0f;
            for (int i = rightActionButtons.Count - 1; i >= 0; i--) {
                total += rightActionButtons [i].Frame.Width;
                rightOffsets [i] = total;
                maxLeftPercentage += rightActionButtons[i].Config.WidthPercentage;
            }
        }

        /// <summary>
        /// Return a fraction from -1.0 to 1.0 that is the ratio of screen percentage to
        /// the maximum screen percentage. (Screen percentage is the ratio of horizontal screen
        /// translation in pixel to the screen width.)
        /// </summary>
        /// <returns>The full move percentage.</returns>
        /// <param name="screenPercentage">Screen percentage.</param>
        private float GetFullMovePercentage (float movePercentage)
        {
            if (0 > movePercentage) {
                return movePercentage / maxLeftPercentage;
            }
            if (0 < movePercentage) {
                return movePercentage / maxRightPercentage;
            }
            return 0.0f;
        }

        public void MoveTo (float percentage)
        {
            LastMovePercentage = percentage;
            percentage = GetFullMovePercentage (percentage);
            // If the full move percentage is greater than 100% in either directions,
            // we must clip it at 100% (+/-) and update because we c
            // we could be at -90% and then a quick swipe action causes the next update to be
            if (-1 > percentage) {
                percentage = -1;
            } else if (1 < percentage) {
                percentage = 1;
            }

            for (int i = 0; i < leftActionButtons.Count; i++) {
                var button = leftActionButtons [i];
                ViewFramer.Create (button).X (-button.Frame.Width + (percentage * leftOffsets [i]));
            }

            for (int i = 0; i < rightActionButtons.Count; i++) {
                var button = rightActionButtons [i];
                ViewFramer.Create (button).X (Frame.Width + (percentage * rightOffsets [i]));
            }

            if (0.0 == percentage) {
                ViewFramer.Create (snapshotView).X (0);
            } else if (0.0 > percentage) {
                ViewFramer.Create (snapshotView).X (percentage * rightOffsets [0]);
            } else {
                ViewFramer.Create (snapshotView).X (percentage * leftOffsets [0]);
            }

            //ViewHelper.DumpViews<SwipeActionViewTagType> (this); // debug
        }

        public void SnapBackToMiddle ()
        {
            UIView.Animate (0.4f, 0, UIViewAnimationOptions.CurveLinear, () => {
                MoveTo (0.0f);
                ViewHelper.DumpViews<SwipeActionViewTagType> (this); // debug
            }, () => {
            });
        }

        public void SnapToFullyExtended ()
        {
            UIView.Animate (0.4f, 0, UIViewAnimationOptions.CurveLinear, () => {
                MoveTo ((float)Math.Sign(LastMovePercentage) * 1.0f);
                ViewHelper.DumpViews<SwipeActionViewTagType> (this); // debug
            }, () => {
            });
        }
    }

    public enum SwipeSide
    {
        LEFT,
        RIGHT
    }

    /// <summary>
    /// Callback function for notifying the owner that a button has been clicked. On click
    /// the swiping view is automatically removed.
    /// </summary>
    public delegate void SwipeActionButtonCallback (SwipeSide side, int index);

    public delegate void SwipeActionSwipeCallback (bool isBeginning);

    /// <summary>
    /// Swipe view is meant for a base class for all table cells that require swipe actions.
    /// It tracks horizontal swipe action on the view and provides configureable slide out
    /// action  buttons. Swiping halfway across the element results in showing of the
    /// configured actions. Each action has a text and an image (along with a background color)
    /// 
    /// The user provides:
    /// 1. The number of left swipe actions
    /// 2. The number of right swipe actions
    /// 3. The color for each left / right swipe action
    /// 4. The text and image for each swipe action
    /// 5. A delegate for handling all touches
    /// 
    /// </summary>
    public class SwipeActionView : UIView
    {
        public float SnapOutThreshold = 0.5f;
        public float LastActionThreshold = 0.8f;

        public SwipeActionButtonCallback OnClick;
        public SwipeActionSwipeCallback OnSwipe;

        public SwipeActionButtonList LeftSwipeActionButtons { get; protected set; }
        public SwipeActionButtonList RightSwipeActionButtons { get; protected set; }

        protected UIPanGestureRecognizer swipeRecognizer;
        protected SwipeActionSwipingView swipingView;
        protected float startingXOffset;

        public SwipeActionView (RectangleF frame) : base (frame)
        {
            Tag = (int)SwipeActionViewTagType.SWIPE_ACTION_VIEW;

            BackgroundColor = UIColor.White;
            ClipsToBounds = true;

            swipeRecognizer = new UIPanGestureRecognizer (PanHandler);
            swipeRecognizer.ShouldRecognizeSimultaneously = delegate {
                return true;
            };
            swipeRecognizer.ShouldBegin = delegate(UIGestureRecognizer obj) {
                var recognizer = (UIPanGestureRecognizer)obj;
                var velocity = recognizer.VelocityInView (this);
                return Math.Abs (velocity.X) > Math.Abs (velocity.Y);
            };
            swipeRecognizer.MinimumNumberOfTouches = 1;
            swipeRecognizer.MaximumNumberOfTouches = 1;
            AddGestureRecognizer (swipeRecognizer);

            LeftSwipeActionButtons = new SwipeActionButtonList ();
            RightSwipeActionButtons = new SwipeActionButtonList ();
        }

        private void PanHandler (UIPanGestureRecognizer obj)
        {
            switch (obj.State) {
            case UIGestureRecognizerState.Began:
                {
                    OnSwipe (true);

                    // Create a swiping view
                    if (null == swipingView) {
                        swipingView = new SwipeActionSwipingView (this, LeftSwipeActionButtons, RightSwipeActionButtons);
                        AddSubview (swipingView);
                    }
                    var touch = obj.TranslationInView (this);
                    startingXOffset = touch.X;
                    break;
                }
            case UIGestureRecognizerState.Changed:
                {
                    // Move the swiping view
                    var touch = obj.TranslationInView (this);
                    Console.WriteLine (">>>>>> {0} {1}", touch.X, touch.Y);
                    float deltaX = touch.X - startingXOffset;
                    swipingView.MoveTo (deltaX / Frame.Width);
                    break;
                }
            default:
                {
                    NcAssert.True ((UIGestureRecognizerState.Ended == obj.State) ||
                    (UIGestureRecognizerState.Cancelled == obj.State));
                    if (!MayRemoveSwipingView ()) {
                        MayCompletePullOut ();
                    }
                    OnSwipe (false);
                    break;
                }
            }
        }

        /// <summary>
        /// This method allows user to add new swipe action and to modify or delete
        /// existing actions.
        /// 
        /// 1. Provide a valid descriptor, side and index of -1 to append to the end.
        /// 2. Provide a null descriptor, sdie and index of an existing action to delete.
        /// 3. Provide a new valid descriptor, side, and index of an existing action to
        ///    update.
        /// </summary>
        /// <param name="descriptor">Descriptor.</param>
        /// <param name="side">Side.</param>
        /// <param name="index">Index.</param>
        public void SetAction (SwipeActionDescriptor descriptor, SwipeSide side, int index = -1)
        {
            SwipeActionButtonList actionButtons;

            if (SwipeSide.LEFT == side) {
                actionButtons = LeftSwipeActionButtons;
            } else {
                NcAssert.True (SwipeSide.RIGHT == side);
                actionButtons = RightSwipeActionButtons;
            }

            NcAssert.True (actionButtons.Count > index);

            // If given a valid descriptor, we must be creating a new action view
            SwipeActionButton newButton = null;
            if (null != descriptor) {
                newButton = new SwipeActionButton (descriptor, this);
                newButton.TouchUpInside += (object sender, EventArgs e) => {
                    RemoveSwipingView ();
                    // If we don't have the index, use the length of the list
                    int realIndex = -1 == index ? actionButtons.Count : index;
                    OnClick (side, realIndex);
                };
            }

            if (-1 == index) {
                // Add a new action at the end
                NcAssert.True (null != newButton);
                actionButtons.Add (newButton);
                return;
            }

            // Modify or delete an existing action
            var view = actionButtons [index];
            view.RemoveFromSuperview ();

            if (null == descriptor) {
                // Delete
                actionButtons.RemoveAt (index);
            } else {
                // Modify
                actionButtons [index] = newButton;

                // Remove all subviews behind this
                actionButtons.IteratorForward ((SwipeActionButton button) => {
                    button.RemoveFromSuperview ();
                }, index + 1);

                // Add this subview and everything after back
                actionButtons.IteratorForward ((SwipeActionButton button) => {
                    AddSubview (button);
                }, index);
            }
        }

        protected bool MayRemoveSwipingView ()
        {
            if (SnapOutThreshold > Math.Abs(swipingView.FullMovePercentage)) {
                swipingView.SnapBackToMiddle ();
                RemoveSwipingView ();
                return true;
            }
            return false;
        }

        protected void MayCompletePullOut ()
        {
            if (SnapOutThreshold <= Math.Abs(swipingView.FullMovePercentage)) {
                swipingView.SnapToFullyExtended ();
            }
        }

        protected void RemoveSwipingView ()
        {
            NcAssert.True (null != swipingView);
            swipingView.RemoveFromSuperview ();
            swipingView = null;

            foreach (var button in LeftSwipeActionButtons) {
                button.RemoveFromSuperview ();
            }
            foreach (var button in RightSwipeActionButtons) {
                button.RemoveFromSuperview ();
            }
            SetNeedsDisplay ();
        }

        protected PointF SingleTouchInView (NSSet touches)
        {
            NcAssert.True (1 == touches.Count);
            UITouch touch = touches.AnyObject as UITouch;
            NcAssert.True (null != touch);
            return touch.LocationInView (this);
        }
    }
}

