//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;

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
        public float WidthDelta { get; protected set; }
        public UIImage Image { get; protected set; }
        public string Text { get; protected set; }
        public UIColor Color { get; protected set; }
        // This is not the tag for an UI object. Instead, it is for identifying the action during callback
        public int Tag { get; protected set; }

        public SwipeActionDescriptor (int tag, float widthDelta, UIImage image, string text, UIColor color = null)
        {
            NcAssert.True (0.3 >= widthDelta); // max width check
            WidthDelta = widthDelta;
            Image = image;
            Text = text;
            Color = color;
            Tag = tag;
        }
    }

    public class SwipeActionButton : UIButton
    {
        public SwipeActionDescriptor Config { get; protected set; }

        public SwipeActionButton (SwipeActionDescriptor descriptor, UIView parentView)
        {
            Tag = (int)SwipeActionViewTagType.SWIPE_ACTION_BUTTON;
            Config = descriptor;
            SetTitle (Config.Text, UIControlState.Normal);
            BackgroundColor = Config.Color;
            Enabled = true;
            UserInteractionEnabled = true;

            if (null != Config.Image) {
                UIImage image = Config.Image;
                if (UIImageRenderingMode.AlwaysTemplate != image.RenderingMode) {
                    // Make sure it is a template so we can use tint to match the icon color to that of the title
                    image = image.ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate);
                }
                SetImage (image, UIControlState.Normal);
                ImageView.TintColor = UIColor.White;

                // Move the image on top of the title and center it
                SizeToFit ();
                float deltaX = Frame.Width - ImageView.Frame.X - ImageView.Frame.Width;
                float deltaY = (ImageView.Frame.Height / 2.0f) + 1.0f;
                ImageEdgeInsets = new UIEdgeInsets (-deltaY, -ImageView.Frame.X, deltaY, -deltaX);
                deltaX = Frame.Width - TitleLabel.Frame.X - TitleLabel.Frame.Width;
                deltaY = (TitleLabel.Frame.Height / 2.0f) + 1.0f;
                TitleEdgeInsets = new UIEdgeInsets (deltaY, -TitleLabel.Frame.X, -deltaY, -deltaX);
            }

            ViewFramer.Create (this)
                .X (0)
                .Y (0)
                .Width ((float)Math.Round (parentView.Frame.Width * descriptor.WidthDelta))
                .Height (parentView.Frame.Height);
        }
    }

    /// <summary>
    /// Swipe last action view is a simple colored background used to create an extension
    /// of the last (outermost) action button with the same color. It is positioned 
    /// below the last action button and above all other views so that it blocks
    /// everything else except the last action button.
    /// </summary>
    public class SwipeLastActionView : UIView
    {
        public SwipeLastActionView (RectangleF frame, UIColor color) : base (frame)
        {
            BackgroundColor = color;
            UserInteractionEnabled = false;
        }
    }

    public class SwipeActionButtonList : List<SwipeActionButton>
    {
        public delegate void IterationCallback (SwipeActionButton actionButton);

        public SwipeActionButtonList () : base ()
        {
        }

        public void IteratorForward (IterationCallback callback, int startIndex, int stopIndex = -1)
        {
            if (-1 == stopIndex) {
                stopIndex = Count;
            }
            for (int i = startIndex; i < stopIndex; i++) {
                callback (this [i]);
            }
        }

        public bool IsLastIndex (int i)
        {
            return ((Count - 1) == i);
        }
    }

    /// <summary>
    /// Swipe action swiping view is used when user starts to swipe on the view.
    /// At that point, this view takes a screen shot of the swipe action view put it
    /// here 
    /// </summary>
    public class SwipeActionSwipingView : UIView
    {
        float[] leftOffsets;
        float[] rightOffsets;

        float maxLeftDelta;
        float maxRightDelta;

        SwipeActionButtonList leftActionButtons;
        SwipeActionButtonList rightActionButtons;
        UIView snapshotView;
        UIGestureRecognizer tapRecognizer;
        float lastActionThreshold;
        SwipeLastActionView leftLastActionView;
        SwipeLastActionView rightLastActionView;
        Action OnClear; // when user taps on the body part to clear all buttons

        public float LastScreenDelta { get; protected set; }

        public float BaseMovePercentage { get; protected set; }

        public float LastMovePercentage {
            get {
                return ClipMovePercentage(UnclippedLastMovePercentage);
            }
        }

        public float UnclippedLastMovePercentage {
            get {
                return GetMovePercentage (LastScreenDelta);
            }
        }

        public SwipeActionSwipingView (SwipeActionView view,
            SwipeActionButtonList leftButtons, SwipeActionButtonList rightButtons,
            Action onClear) : base (view.Frame)
        {
            Tag = (int)SwipeActionViewTagType.SWIPE_ACTION_SWIPING_VIEW;

            ClipsToBounds = true;
            leftActionButtons = leftButtons;
            rightActionButtons = rightButtons;
            LastScreenDelta = 0.0f;
            BaseMovePercentage = 0.0f;
            OnClear = onClear;

            lastActionThreshold = view.LastActionThreshold;

            // Take a snapshot of the original view
            snapshotView = view.SnapshotView (false);
            AddSubview (snapshotView);

            // Add two last action views
            if (0 < leftButtons.Count) {
                leftLastActionView = new SwipeLastActionView (Frame, leftButtons.Last ().Config.Color);
                ViewFramer.Create (leftLastActionView).X (-leftLastActionView.Frame.Width);
            }
            if (0 < rightButtons.Count) {
                rightLastActionView = new SwipeLastActionView (Frame, rightButtons.Last ().Config.Color);
                ViewFramer.Create (rightLastActionView).X (Frame.Width);
            }

            // Stack the buttons on top of the image view
            int k = 0;
            foreach (var button in leftActionButtons) {
                NcAssert.True (button.Frame.Height == view.Frame.Height);
                if (null != button.Superview) {
                    button.RemoveFromSuperview ();
                }
                ViewFramer.Create (button).X (-button.Frame.Width).Y (0);
                if (leftActionButtons.IsLastIndex(k)) {
                    AddSubview (leftLastActionView);
                }
                AddSubview (button);
                k++;
            }
            k = 0;
            foreach (var button in rightActionButtons) {
                NcAssert.True (button.Frame.Height == view.Frame.Height);
                if (null != button.Superview) {
                    button.RemoveFromSuperview ();
                }
                ViewFramer.Create (button).X (Frame.Width).Y (0);
                if (rightActionButtons.IsLastIndex (k)) {
                    AddSubview (rightLastActionView);
                }
                AddSubview (button);
                k++;
            }

            // Compute the amount of shift required for each button
            maxRightDelta = 0.0f;
            leftOffsets = new float[leftActionButtons.Count];
            float total = 0.0f;
            for (int i = leftActionButtons.Count - 1; i >= 0; i--) {
                total += leftActionButtons [i].Frame.Width;
                leftOffsets [i] = total;
                maxRightDelta += leftActionButtons [i].Config.WidthDelta;
            }

            maxLeftDelta = 0.0f;
            rightOffsets = new float[rightActionButtons.Count];
            total = 0.0f;
            for (int i = rightActionButtons.Count - 1; i >= 0; i--) {
                total += rightActionButtons [i].Frame.Width;
                rightOffsets [i] = total;
                maxLeftDelta += rightActionButtons[i].Config.WidthDelta;
            }

            // Add a tap gesture recognizer to retract pulled out buttons
            tapRecognizer = new UITapGestureRecognizer ((UITapGestureRecognizer obj) => {
                OnClear ();
            });
            AddGestureRecognizer (tapRecognizer);
        }

        /// <summary>
        /// Return the ratio of screen percentage to the maximum screen percentage.
        /// (Screen percentage is the ratio of horizontal screen translation in pixel
        /// to the screen width.)
        /// </summary>
        /// <returns>The full move percentage.</returns>
        /// <param name="screenPercentage">Screen percentage.</param>
        private float GetMovePercentage (float screenDelta)
        {
            float movePercentage = 0.0f;
            if (0 > screenDelta) {
                movePercentage = screenDelta / maxLeftDelta;
            }
            if (0 < screenDelta) {
                movePercentage = screenDelta / maxRightDelta;
            }
            movePercentage += BaseMovePercentage;
            return movePercentage;
        }

        private float ClipMovePercentage (float movePercentage)
        {
            // If the full move percentage is greater than 100% in either directions,
            // we must clip it at 100% (+/-) and update because we could be at -90%
            // and then a quick swipe action causes the next update to be over 100%.
            // If we don't clip, it will not reach the fully extended state
            if (-1.0f > movePercentage) {
                return -1.0f;
            }
            if (1.0f < movePercentage) {
                return 1.0f;
            }
            return movePercentage;
        }

        public void MoveByDelta (float delta, float x)
        {
            MoveByMovePercentage (GetMovePercentage (delta), x);
            LastScreenDelta = delta;
        }

        private bool ShouldStartLastActionAnimation (float movePercentage)
        {
            return ((lastActionThreshold <= movePercentage) || (-lastActionThreshold >= movePercentage));
        }

        public void MoveByMovePercentage (float movePercentage, float x)
        {
            SwipeActionButton button;
            float clippedPercentage = ClipMovePercentage (movePercentage);

            if (0.0 == movePercentage) {
                ViewFramer.Create (snapshotView).X (0);
            } else if (0.0 > movePercentage) {
                ViewFramer.Create (snapshotView).X (clippedPercentage * rightOffsets [0]);
            } else {
                ViewFramer.Create (snapshotView).X (clippedPercentage * leftOffsets [0]);
            }

            for (int i = 0; i < (leftActionButtons.Count - 1); i++) {
                button = leftActionButtons [i];
                ViewFramer.Create (button).X (-button.Frame.Width + (clippedPercentage * leftOffsets [i]));
            }

            for (int i = 0; i < (rightActionButtons.Count - 1); i++) {
                button = rightActionButtons [i];
                ViewFramer.Create (button).X (Frame.Width + (clippedPercentage * rightOffsets [i]));
            }

            // The last action is a special case. Between (+/1) 1.0 and 1.25, there is a dead zone
            // where it moves to 1.0. Beyond that 1.25, it should track the unclipped move percentage
            bool IsAnimatingLast = ShouldStartLastActionAnimation (movePercentage);
            // Intentionally not use LastMovePercentage property because it clips to +/-1.0.
            float lastMovePercentage = GetMovePercentage (LastScreenDelta);
            bool WasAnimatingLast = ShouldStartLastActionAnimation (lastMovePercentage);
            float duration = IsAnimatingLast != WasAnimatingLast ? 0.3f : 0.0f;

            UIView.Animate (duration, 0, UIViewAnimationOptions.CurveLinear, () => {
                if (IsAnimatingLast) {
                    NcAssert.True (0.0f != movePercentage);
                    if (0.0 < movePercentage) {
                        var lastButton = leftActionButtons.Last ();
                        // Touch points to the center of the button. Adjust to the left.
                        x -= lastButton.Config.WidthDelta / 2.0f;
                        if ((1.0f - lastButton.Config.WidthDelta) < x) {
                            x = 1.0f - lastButton.Config.WidthDelta; // don't let the button scroll off the right edge
                        }
                        ViewFramer.Create (lastButton).X (x * Frame.Width);

                        // Move the last action view
                        if (null != leftLastActionView) {
                            ViewFramer.Create (leftLastActionView).X ((x - 1.0f) * Frame.Width);
                        }
                    } else {
                        var lastButton = rightActionButtons.Last ();
                        // Touch points to the center of the button. Adjust to the left.
                        x -= lastButton.Config.WidthDelta / 2.0f;
                        if (0.0f > x) {
                            x = 0.0f; // don't let the button scroll off the left edge of the screen
                        }
                        ViewFramer.Create (lastButton).X (x * Frame.Width);

                        // Movve the last action view
                        if (null != rightLastActionView) {
                            ViewFramer.Create (rightLastActionView).X (x * Frame.Width);
                        }
                    }
                } else {
                    // Move last action views
                    if (null != leftLastActionView) {
                        ViewFramer.Create (leftLastActionView).X (-leftLastActionView.Frame.Width);
                    }
                    if (null != rightLastActionView) {
                        ViewFramer.Create (rightLastActionView).X (Frame.Width);
                    }

                    // Move all buttons
                    int i = leftActionButtons.Count - 1;
                    button = leftActionButtons [i];
                    ViewFramer.Create (button).X (-button.Frame.Width + (clippedPercentage * leftOffsets [i]));

                    i = rightActionButtons.Count - 1;
                    button = rightActionButtons [i];
                    ViewFramer.Create (button).X (Frame.Width + (clippedPercentage * rightOffsets [i]));
                }
            }, () => {
            });

            //ViewHelper.DumpViews<SwipeActionViewTagType> (this); // debug
        }

        private float MovePercentToDelta (float movePercentage)
        {
            if (0 < movePercentage) {
                return movePercentage * maxRightDelta;
            }
            if (0 > movePercentage) {
                return movePercentage * maxLeftDelta;
            }
            return 0.0f;
        }

        /// <summary>
        /// The amount of animation of pulling all buttons out and pushing all buttons back
        /// to the edges depends on how far the buttons need to travel. In order to
        /// maintain a constant speed, the duration must be computed based on the current
        /// position (of buttons) and the final position.
        ///
        /// The computation tries to be invariant to device side / resolution by normalized
        /// against the physical resolution of the device.
        /// </summary>
        /// <returns>The animate duration.</returns>
        /// <param name="finalMovePercentage">Final move percentage.</param>
        private float ComputeAnimationDuration (float finalMovePercentage)
        {
            float total = MovePercentToDelta (finalMovePercentage) - MovePercentToDelta (LastMovePercentage);
            float rate = 1.0f; // 1 sec to move across the entire screen
            return rate * Math.Abs (total);
        }

        private void SnapToPosition (float finalMovePercentage, float finalLocation, Action onComplete)
        {
            float duration = ComputeAnimationDuration (finalMovePercentage);
            UIView.Animate (duration, 0, UIViewAnimationOptions.CurveLinear, () => {
                MoveByMovePercentage (finalMovePercentage, finalLocation);
                //ViewHelper.DumpViews<SwipeActionViewTagType> (this); // debug
            }, () => {
                onComplete ();
            });
        }

        public void SnapToAllButtonsHidden (Action onComplete)
        {
            BaseMovePercentage = 0.0f;
            SnapToPosition (0.0f, 0.0f, onComplete);
        }

        public void SnapToAllButtonsShown (Action onComplete)
        {
            SnapToPosition ((float)Math.Sign (LastMovePercentage) * 1.0f, 0.0f, onComplete);
        }

        public bool SnapToLastButtonOnly (Action onComplete)
        {
            if (0.0 == LastMovePercentage) {
                return false;
            }

            float finalMovePercentage = 1.0f / (0.0 < LastMovePercentage ? maxLeftDelta : maxRightDelta);
            float finalLocation =
                (0.0f < LastMovePercentage ? 1.0f - leftActionButtons.Last ().Config.WidthDelta : 0.0f);
            SnapToPosition (finalMovePercentage, finalLocation, onComplete);
            return true;
        }

        public void EndSwipe ()
        {
            BaseMovePercentage = LastMovePercentage;
        }

        public bool PointOnActionButtons (PointF point)
        {
            foreach (var view in Subviews) {
                var actionButton = view as SwipeActionButton;
                if (null == actionButton) {
                    continue; // not a button ignore
                }
                if (view.Frame.Contains (point)) {
                    return true;
                }
            }
            return false;
        }
    }

    public enum SwipeSide
    {
        LEFT,
        RIGHT
    }

    /// <summary>
    /// Swipe view is meant for a base class for all views that require swipe actions.
    /// It tracks horizontal swipe action on the view and provides configureable slide out
    /// action buttons. Swiping halfway across the element results in showing of the
    /// configured actions. Each action has a text and an image (along with a background color)
    /// 
    /// To use swipe action view:
    ///   1. Create a SwipeActionView as you normally would for a UIView.
    ///   2. Call SetAction() to add buttons to either (or both) sides
    ///   3. Set up a callback for button clicked (OnClick).
    ///   4. Set up another callback for notification of swiping beginning and ending.
    ///   5. Add your content to SwipeActionView as normal.
    /// 
    /// Each action button has the following configurable attributes:
    ///   1. A text string on the button
    ///   2. A background color
    ///   3. An optional image shown on top of the text string
    ///   4. A width expressed as a fraction of the width of the view.
    /// 
    /// Action buttons are identified by (side, index) where side is either
    /// LEFT or RIGHT and index starts at 0. Index 0 is the most center button.
    /// Suppose we have 1 button on the left and 3 buttons on the right.
    /// The buttons are arranged like:
    /// 
    /// [L0][Content view][R0][R1][R2]
    /// 
    /// Swipe action view has two operating modes - normal and swiping mode.
    /// Swiping mode is when port of some action buttons are shown. Normal mode
    /// is when all buttons hidden.
    /// 
    /// There are two states when exiting swiping move - 1) all buttons shown, 2)
    /// all buttons hidden. If at least 50% of all buttons (on one side) are shown,
    /// it goes (i.e. with animation) to all (buttons) shown states. Otherwise, it
    /// goes to all hidden states. The 50% (0.5) can be adjusted via
    /// SnapAllShownThreshold.
    /// 
    /// Action buttons can be modified after the view is created. However, it cannot
    /// be done during swiping (when the buttons are shown). If you need to further
    /// customize each action button, you can use LeftSwipeActionButtons and
    /// RightSwipeActionButtons. Note that they are read-only. But you can modify its
    /// attributes. (To modify the button object, use SetAction().)
    /// 
    /// All buttons on each side cannot take up more than 90% of the screen width.
    /// 
    /// </summary>
    public class SwipeActionView : UIView
    {
        /// <summary>
        /// Callback function for notifying the owner that a button has been clicked. On click
        /// the swiping view is automatically removed. The parameters indicate which
        /// button is clicked.
        /// </summary>
        public delegate void ButtonCallback (int tag);

        public enum SwipeState {
            SWIPE_BEGIN,
            SWIPE_END_ALL_HIDDEN,
            SWIPE_END_ALL_SHOWN
        };

        /// <summary>
        /// Callback function for notifying when swiping is beginning or ending.
        /// The owner of the swipe view can, for example, disable scrolling or other
        /// gesture recognizer in the table view and restore those functionalities
        /// when swiping ends.
        /// </summary>
        public delegate void SwipeCallback (SwipeState state);

        public float SnapAllShownThreshold = 0.5f;
        public float LastActionThreshold = 1.25f;

        public ButtonCallback OnClick;
        public SwipeCallback OnSwipe;

        public SwipeActionButtonList LeftSwipeActionButtons { get; protected set; }
        public SwipeActionButtonList RightSwipeActionButtons { get; protected set; }

        protected UIPanGestureRecognizer swipeRecognizer;
        protected SwipeActionSwipingView swipingView;
        protected SwipeLastActionView leftLastActionView;
        protected SwipeLastActionView rightLastActionView;

        public SwipeActionView (RectangleF frame) : base (frame)
        {
            Tag = (int)SwipeActionViewTagType.SWIPE_ACTION_VIEW;

            BackgroundColor = UIColor.White;
            ClipsToBounds = true;

            // Add a pan gesture recognizer to handle the swiping
            swipeRecognizer = new UIPanGestureRecognizer (PanHandler);
            swipeRecognizer.ShouldRecognizeSimultaneously =
                (UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer) => {
                return true;
            };
            swipeRecognizer.ShouldReceiveTouch = (UIGestureRecognizer recognizer, UITouch touch) => {
                if (null == swipingView) {
                    return true;
                }
                // Check each of the subviews in the swiping view
                if (swipingView.PointOnActionButtons (touch.LocationInView (swipingView))) {
                    return false;
                }
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
                    OnSwipe (SwipeState.SWIPE_BEGIN);

                    // Create a swiping view
                    if (null == swipingView) {
                        swipingView = new SwipeActionSwipingView (this, LeftSwipeActionButtons,
                            RightSwipeActionButtons,
                            () => {
                                swipingView.SnapToAllButtonsHidden (() => {
                                    RemoveSwipingView ();
                                    OnSwipe (SwipeState.SWIPE_END_ALL_HIDDEN);
                                });
                            });
                        AddSubview (swipingView);
                    }
                    break;
                }
            case UIGestureRecognizerState.Changed:
                {
                    // Move the swiping view
                    float deltaPercentage = obj.TranslationInView (this).X / Frame.Width;
                    float locationPercentage = obj.LocationInView (this).X / Frame.Width;
                    swipingView.MoveByDelta (deltaPercentage, locationPercentage);
                    break;
                }
            default:
                {
                    NcAssert.True ((UIGestureRecognizerState.Ended == obj.State) ||
                    (UIGestureRecognizerState.Cancelled == obj.State));
                    if (!MayRemoveSwipingView () && !MayExecuteLastAction()) {
                        MayCompletePullOut ();
                    }
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

            if (-1 == index) {
                index = actionButtons.Count;
            } else {
                NcAssert.True (actionButtons.Count > index);
            }

            // If given a valid descriptor, we must be creating a new action view
            SwipeActionButton newButton = null;
            if (null != descriptor) {
                newButton = new SwipeActionButton (descriptor, this);
                newButton.TouchUpInside += (object sender, EventArgs e) => {
                    int tag = newButton.Config.Tag;
                    RemoveSwipingView ();
                    OnClick (tag);
                };
            }

            if (actionButtons.Count == index) {
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
            if (SnapAllShownThreshold > Math.Abs (swipingView.LastMovePercentage)) {
                swipingView.SnapToAllButtonsHidden (() => {
                    RemoveSwipingView ();
                    OnSwipe (SwipeState.SWIPE_END_ALL_HIDDEN);
                });
                return true;
            }
            return false;
        }

        protected bool MayExecuteLastAction ()
        {
            if (LastActionThreshold > Math.Abs (swipingView.UnclippedLastMovePercentage)) {
                return false;
            }
            swipingView.SnapToLastButtonOnly (() => {
                SwipeSide side = 0.0f < swipingView.LastScreenDelta ? SwipeSide.LEFT : SwipeSide.RIGHT;
                RemoveSwipingView();
                OnSwipe (SwipeState.SWIPE_END_ALL_HIDDEN);
                // Execute the last action
                if (SwipeSide.LEFT == side) {
                    OnClick (LeftSwipeActionButtons.Last ().Config.Tag);
                } else {
                    NcAssert.True (SwipeSide.RIGHT == side);
                    OnClick (RightSwipeActionButtons.Last ().Config.Tag);
                }
            });
            return true;
        }

        protected void MayCompletePullOut ()
        {
            if (SnapAllShownThreshold <= Math.Abs(swipingView.LastMovePercentage)) {
                swipingView.SnapToAllButtonsShown (() => {
                    swipingView.EndSwipe ();
                    OnSwipe (SwipeState.SWIPE_END_ALL_SHOWN);
                });
            }
        }

        protected void RemoveSwipingView ()
        {
            if (null == swipingView) {
                return;
            }
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

