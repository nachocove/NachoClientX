//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using System.Collections.Generic;
using System.Linq;

using UIKit;
using Foundation;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public enum SwipeActionViewTagType
    {
        SWIPE_ACTION_VIEW,
        SWIPE_ACTION_BUTTON,
        SWIPE_ACTION_SWIPING_VIEW,

    };

    /// <summary>
    /// Swipe action descriptor is a configuration object used for configuring the appearance of a swipe action.
    /// Image and color are optional. If color is omitted, there will be a default sequence of left and
    /// right action colors assigned.
    /// </summary>
    public class SwipeActionDescriptor
    {
        public nfloat WidthDelta { get; protected set; }

        public UIImage Image { get; protected set; }

        public string TextKey { get; protected set; }

        public UIColor Color { get; protected set; }
        // This is not the tag for an UI object. Instead, it is for identifying the action during callback
        public int Tag { get; protected set; }

        public SwipeActionDescriptor (int tag, nfloat widthDelta, UIImage image, string textKey, UIColor color = null)
        {
            NcAssert.True (0.9 >= widthDelta); // FIXME: max width check, dependent on # of items per side 
            WidthDelta = widthDelta;
            Image = image;
            TextKey = textKey;
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
            SetTitle (NSBundle.MainBundle.LocalizedString (Config.TextKey, ""), UIControlState.Normal);
            Font = A.Font_AvenirNextDemiBold14;
            BackgroundColor = Config.Color;
            Enabled = true;
            UserInteractionEnabled = true;
            AccessibilityLabel = NSBundle.MainBundle.LocalizedString (Config.TextKey, "");

            if (null != Config.Image) {
                UIImage image = Config.Image;
                if (UIImageRenderingMode.AlwaysTemplate != image.RenderingMode) {
                    // Make sure it is a template so we can use tint to match the icon color to that of the title
                    using (var templateImage = image.ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate)) {
                        SetImage (templateImage, UIControlState.Normal);
                    }
                } else {
                    SetImage (image, UIControlState.Normal);
                }
                ImageView.TintColor = UIColor.White;

                // Move the image on top of the title and center it
                SizeToFit ();
                nfloat deltaX = Frame.Width - ImageView.Frame.X - ImageView.Frame.Width;
                nfloat deltaY = (ImageView.Frame.Height / 2.0f) + 1.0f;
                ImageEdgeInsets = new UIEdgeInsets (-deltaY, -ImageView.Frame.X, deltaY, -deltaX);
                deltaX = Frame.Width - TitleLabel.Frame.X - TitleLabel.Frame.Width;
                deltaY = (TitleLabel.Frame.Height / 2.0f) + 1.0f;
                TitleEdgeInsets = new UIEdgeInsets (deltaY, -TitleLabel.Frame.X, -deltaY, -deltaX);
            }

            ViewFramer.Create (this)
                .X (0)
                .Y (0)
                .Width (NMath.Round (parentView.Frame.Width * descriptor.WidthDelta))
                .Height (parentView.Frame.Height);
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

        public new void Clear ()
        {
            NcAssert.CaseError ("invalid operation; call ClearActions instead");
        }

    }

    /// <summary>
    /// Swipe action swiping view is used when user starts to swipe on the view.
    /// At that point, this view takes a screen shot of the swipe action view put it
    /// here 
    /// </summary>
    public class SwipeActionSwipingView : UIView
    {
        nfloat [] leftOffsets;
        nfloat [] rightOffsets;

        nfloat maxLeftDelta;
        nfloat maxRightDelta;

        SwipeActionButtonList leftActionButtons;
        SwipeActionButtonList rightActionButtons;
        UIView snapshotView;
        UIGestureRecognizer tapRecognizer;
        Action OnClear;
        // when user taps on the body part to clear all buttons

        public nfloat LastScreenDelta { get; protected set; }

        public nfloat BaseMovePercentage { get; protected set; }

        public nfloat LastMovePercentage {
            get {
                return ClipMovePercentage (UnclippedLastMovePercentage);
            }
        }

        public nfloat UnclippedLastMovePercentage {
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

            // Take a snapshot of the original view
            snapshotView = view.SnapshotView (false);
            snapshotView.BackgroundColor = Util.FindSolidBackgroundColor (view);

            AddSubview (snapshotView);

            // Stack the buttons on top of the image view
            int k = 0;
            foreach (var button in leftActionButtons) {
                if (null != button.Superview) {
                    button.RemoveFromSuperview ();
                }
                ViewFramer.Create (button).X (-button.Frame.Width).Y (0).Height (view.Frame.Height);
                AddSubview (button);
                k++;
            }
            k = 0;
            foreach (var button in rightActionButtons) {
                if (null != button.Superview) {
                    button.RemoveFromSuperview ();
                }
                ViewFramer.Create (button).X (Frame.Width).Y (0).Height (view.Frame.Height);
                AddSubview (button);
                k++;
            }

            // Compute the amount of shift required for each button
            maxRightDelta = 0.0f;
            leftOffsets = new nfloat [leftActionButtons.Count];
            nfloat total = 0.0f;
            for (int i = leftActionButtons.Count - 1; i >= 0; i--) {
                total += leftActionButtons [i].Frame.Width;
                leftOffsets [i] = total;
                maxRightDelta += leftActionButtons [i].Config.WidthDelta;
            }

            maxLeftDelta = 0.0f;
            rightOffsets = new nfloat [rightActionButtons.Count];
            total = 0.0f;
            for (int i = rightActionButtons.Count - 1; i >= 0; i--) {
                total += rightActionButtons [i].Frame.Width;
                rightOffsets [i] = total;
                maxLeftDelta += rightActionButtons [i].Config.WidthDelta;
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
        private nfloat GetMovePercentage (nfloat screenDelta)
        {
            nfloat movePercentage = 0.0f;
            if (0 > screenDelta) {
                movePercentage = 0 != maxLeftDelta ? screenDelta / maxLeftDelta : 0.0f;
            }
            if (0 < screenDelta) {
                movePercentage = 0 != maxRightDelta ? screenDelta / maxRightDelta : 0.0f;
            }
            movePercentage += BaseMovePercentage;
            return movePercentage;
        }

        private nfloat ClipMovePercentage (nfloat movePercentage)
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

        public void MoveByDelta (nfloat delta, nfloat x)
        {
            MoveByMovePercentage (GetMovePercentage (delta), x);
            LastScreenDelta = delta;
        }

        public void MoveByMovePercentage (nfloat movePercentage, nfloat x)
        {
            SwipeActionButton button;
            nfloat clippedPercentage = ClipMovePercentage (movePercentage);

            if (0.0 == movePercentage) {
                ViewFramer.Create (snapshotView).X (0);
            } else if (0.0 > movePercentage) {
                if (rightOffsets.Length == 0) {
                    Log.Error (Log.LOG_UI, "SwipeAction MoveByMovePercentage has no rightOffsets");
                } else {
                    ViewFramer.Create (snapshotView).X (clippedPercentage * rightOffsets [0]);
                }
            } else {
                if (leftOffsets.Length == 0) {
                    Log.Error (Log.LOG_UI, "SwipeAction MoveByMovePercentage has no leftOffsets");
                } else {
                    ViewFramer.Create (snapshotView).X (clippedPercentage * leftOffsets [0]);
                }
            }

            for (int i = 0; i < leftActionButtons.Count; i++) {
                button = leftActionButtons [i];
                if (leftOffsets.Length <= i) {
                    Log.Error (Log.LOG_UI, "SwipeAction MoveByMovePercentage has no leftOffset for button index {0}", i);
                } else {
                    ViewFramer.Create (button).X (-button.Frame.Width + (clippedPercentage * leftOffsets [i]));
                }
            }

            for (int i = 0; i < rightActionButtons.Count; i++) {
                button = rightActionButtons [i];
                if (rightOffsets.Length <= i) {
                    Log.Error (Log.LOG_UI, "SwipeAction MoveByMovePercentage has no rightOffset for button index {0}", i);
                } else {
                    ViewFramer.Create (button).X (Frame.Width + (clippedPercentage * rightOffsets [i]));
                }
            }

            //ViewHelper.DumpViews<SwipeActionViewTagType> (this); // debug
        }

        private nfloat MovePercentToDelta (nfloat movePercentage)
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
        private nfloat ComputeAnimationDuration (nfloat finalMovePercentage)
        {
            nfloat total = MovePercentToDelta (finalMovePercentage) - MovePercentToDelta (LastMovePercentage);
            nfloat rate = 1.0f; // 1 sec to move across the entire screen
            return rate * NMath.Abs (total);
        }

        private void SnapToPosition (nfloat finalMovePercentage, nfloat finalLocation, Action onComplete)
        {
            nfloat duration = ComputeAnimationDuration (finalMovePercentage);
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
            SnapToPosition (NMath.Sign (LastMovePercentage) * 1.0f, 0.0f, onComplete);
        }

        public bool SnapToLastButtonOnly (Action onComplete)
        {
            if (0.0 == LastMovePercentage) {
                return false;
            }

            nfloat finalMovePercentage = 1.0f / (0.0 < LastMovePercentage ? maxLeftDelta : maxRightDelta);
            nfloat finalLocation =
                (0.0f < LastMovePercentage ? 1.0f - leftActionButtons.Last ().Config.WidthDelta : 0.0f);
            SnapToPosition (finalMovePercentage, finalLocation, onComplete);
            return true;
        }

        public void EndSwipe ()
        {
            BaseMovePercentage = LastMovePercentage;
        }

        public bool PointOnActionButtons (CGPoint point)
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

        public enum SwipeState
        {
            SWIPE_BEGIN,
            SWIPE_END_ALL_HIDDEN,
            SWIPE_END_ALL_SHOWN,
        };

        /// <summary>
        /// Callback function for notifying when swiping is beginning or ending.
        /// The owner of the swipe view can, for example, disable scrolling or other
        /// gesture recognizer in the table view and restore those functionalities
        /// when swiping ends.
        /// </summary>
        public delegate void SwipeCallback (SwipeActionView activeView, SwipeState state);

        public delegate bool ShouldSwipeCallback ();

        public nfloat SnapAllShownThreshold = 0.5f;

        public ButtonCallback OnClick;
        public SwipeCallback OnSwipe;
        public ShouldSwipeCallback ShouldSwipe;

        public SwipeActionButtonList LeftSwipeActionButtons { get; protected set; }

        public SwipeActionButtonList RightSwipeActionButtons { get; protected set; }

        protected UITapGestureRecognizer coverRecognizer;
        protected UIPanGestureRecognizer swipeRecognizer;
        protected SwipeActionSwipingView swipingView;
        protected UIView coverView;
        private bool isPanning;

        public void TryOnClick (int tag)
        {
            if (null != OnClick) {
                OnClick (tag);
            }
        }

        public void TryOnSwipe (SwipeActionView activeView, SwipeState state)
        {
            if (null != OnSwipe) {
                OnSwipe (activeView, state);
            }
        }

        public SwipeActionView (CGRect frame) : base (frame)
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
            swipeRecognizer.ShouldBegin = delegate (UIGestureRecognizer obj) {
                if (ShouldSwipe != null && !ShouldSwipe ()) {
                    return false;
                }
                var recognizer = (UIPanGestureRecognizer)obj;
                var velocity = recognizer.VelocityInView (this);
                return NMath.Abs (velocity.X) > NMath.Abs (velocity.Y);
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
            case UIGestureRecognizerState.Began: {
                    isPanning = true;
                    TryOnSwipe (this, SwipeState.SWIPE_BEGIN);

                    // Create a swiping view
                    if (null == swipingView) {
                        swipingView = new SwipeActionSwipingView (this, LeftSwipeActionButtons,
                            RightSwipeActionButtons,
                            () => {
                                if (!isPanning) {
                                    if (null != swipingView) {
                                        swipingView.SnapToAllButtonsHidden (() => {
                                            RemoveSwipingView ();
                                            TryOnSwipe (this, SwipeState.SWIPE_END_ALL_HIDDEN);
                                        });
                                    }
                                }
                            });
                        // Where is swiping view on the  screen?
                        var topView = Util.FindOutermostView (this);
                        NcAssert.NotNull (topView, "PanHandler: topView is null");
                        var screenLocation = this.ConvertPointToView (this.Frame.Location, null);
                        var adjustedFrame = new CGRect (screenLocation.X, screenLocation.Y, this.Frame.Width, this.Frame.Height);
                        coverView = new CoverView (topView, adjustedFrame);
                        topView.AddSubview (coverView);
                        this.AddSubview (swipingView);
                        coverRecognizer = new UITapGestureRecognizer ((UITapGestureRecognizer tap) => {
                            if (!isPanning) {
                                if (null != swipingView) {
                                    swipingView.SnapToAllButtonsHidden (() => {
                                        RemoveSwipingView ();
                                        TryOnSwipe (this, SwipeState.SWIPE_END_ALL_HIDDEN);
                                    });
                                }
                            }
                        });
                        coverView.AddGestureRecognizer (coverRecognizer);
                    }
                    break;
                }
            case UIGestureRecognizerState.Changed: {
                    // Move the swiping view
                    nfloat deltaPercentage = obj.TranslationInView (this).X / Frame.Width;
                    nfloat locationPercentage = obj.LocationInView (this).X / Frame.Width;
                    swipingView.MoveByDelta (deltaPercentage, locationPercentage);
                    break;
                }
            default: {
                    NcAssert.True ((UIGestureRecognizerState.Ended == obj.State) ||
                    (UIGestureRecognizerState.Cancelled == obj.State));
                    isPanning = false;
                    if (!MayRemoveSwipingView ()) {
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
                    if (!isPanning) {
                        int tag = newButton.Config.Tag;
                        RemoveSwipingView ();
                        TryOnSwipe (this, SwipeState.SWIPE_END_ALL_HIDDEN);
                        TryOnClick (tag);
                    }
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
            if (null == swipingView) {
                // Disabled before view created
                return false;
            }
            if (SnapAllShownThreshold > NMath.Abs (swipingView.LastMovePercentage)) {
                swipingView.SnapToAllButtonsHidden (() => {
                    RemoveSwipingView ();
                    TryOnSwipe (this, SwipeState.SWIPE_END_ALL_HIDDEN);
                });
                return true;
            }
            return false;
        }

        public void EnableSwipe (bool enabled)
        {
            swipeRecognizer.Enabled = enabled;
        }

        public void EnableSwipe ()
        {
            swipeRecognizer.Enabled = true;
        }

        public void DisableSwipe ()
        {
            swipeRecognizer.Enabled = false;
        }

        public bool IsSwipeEnabled ()
        {
            if (null == swipeRecognizer) {
                return false;
            }
            return swipeRecognizer.Enabled;
        }

        public void ClearActions (SwipeSide whatSide)
        {
            int numCurrentActions = 0;
            if (SwipeSide.LEFT == whatSide) {
                numCurrentActions = LeftSwipeActionButtons.Count;
            } else {
                numCurrentActions = RightSwipeActionButtons.Count;
            }
            for (int i = 0; i < numCurrentActions; i++) {
                SetAction (null, whatSide, 0);
            }
        }

        protected void MayCompletePullOut ()
        {
            if (SnapAllShownThreshold <= NMath.Abs (swipingView.LastMovePercentage)) {
                swipingView.SnapToAllButtonsShown (() => {
                    if (null == swipingView) {
                        return;
                    }
                    swipingView.EndSwipe ();
                    TryOnSwipe (this, SwipeState.SWIPE_END_ALL_SHOWN);
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

            coverView.RemoveFromSuperview ();
            coverView = null;

            foreach (var button in LeftSwipeActionButtons) {
                button.RemoveFromSuperview ();
            }
            foreach (var button in RightSwipeActionButtons) {
                button.RemoveFromSuperview ();
            }
            SetNeedsDisplay ();
        }

        protected CGPoint SingleTouchInView (NSSet touches)
        {
            NcAssert.True (1 == touches.Count);
            UITouch touch = touches.AnyObject as UITouch;
            NcAssert.True (null != touch);
            return touch.LocationInView (this);
        }
    }
}

