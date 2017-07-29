using System;
using System.Collections.Generic;
using Foundation;
using UIKit;
using CoreGraphics;
using CoreAnimation;

namespace NachoClient.iOS
{

    public class SwipeAction
    {

        public readonly UIImage Image;
        public readonly string TitleKey;
        public readonly UIColor Color;

        public SwipeAction (string title, UIImage image, UIColor color)
        {
            TitleKey = title;
            Image = image;
            Color = color;
        }
    }

    public class BasicSwipeAction : SwipeAction
    {

        public readonly Action Action;

        public BasicSwipeAction (string title, UIImage image, UIColor color, Action action) : base (title, image, color)
        {
            Action = action;
        }

    }

    public interface SwipeActionsViewDelegate
    {
        List<SwipeAction> ActionsForViewSwipingRight (SwipeActionsView view);

        List<SwipeAction> ActionsForViewSwipingLeft (SwipeActionsView view);

        void SwipeViewWillBeginShowingActions (SwipeActionsView view);

        void SwipeViewDidEndShowingActions (SwipeActionsView view);

        void SwipeViewDidSelectAction (SwipeActionsView view, SwipeAction action);
    }

    public class SwipeActionsView : UIView, IUIGestureRecognizerDelegate
    {

        #region Enums

        private enum SwipeActionViewState
        {
            Normal,
            ShowingSwipeLeftActions,
            ShowingSwipeRightActions
        }

        ;

        #endregion

        #region Properties

        public SwipeActionsViewDelegate Delegate;
        public readonly UIView ContentView;
        private SwipeActionViewState State;

        private UITapGestureRecognizer TapGestureRecognizer;
        public UIPanGestureRecognizer PanGestureRecognizer { get; private set; }

        private List<ActionView> ActionViews;
        private nfloat PreferredActionWidth = 64.0f;

        private nfloat ActionRevealPercentage = 0.0f;
        private nfloat TranslationAdjustment = 0.0f;

        public bool Enabled;

        #endregion

        #region Lazy Init Properties

        private ActionsContainerView _ActionsView;

        private ActionsContainerView ActionsView {
            get {
                if (_ActionsView == null) {
                    _ActionsView = new ActionsContainerView (new CGRect (0.0f, 0.0f, 0.0f, Bounds.Height));
                    _ActionsView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight;
                    InsertSubviewBelow (_ActionsView, ContentView);
                }
                return _ActionsView;
            }
        }

        #endregion

        #region Constructors

        public SwipeActionsView (CGRect frame) : base (frame)
        {
            BackgroundColor = UIColor.FromRGB (0xE0, 0xE0, 0xE0);

            ContentView = new UIView (Bounds);
            ContentView.BackgroundColor = UIColor.White;
            ContentView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            AddSubview (ContentView);

            TapGestureRecognizer = new UITapGestureRecognizer (DidTap);
            TapGestureRecognizer.WeakDelegate = this;
            ContentView.AddGestureRecognizer (TapGestureRecognizer);

            PanGestureRecognizer = new UIPanGestureRecognizer (DidPan);
            PanGestureRecognizer.WeakDelegate = this;
            AddGestureRecognizer (PanGestureRecognizer);

            ActionViews = new List<ActionView> ();

            State = SwipeActionViewState.Normal;
            Enabled = true;

        }

        #endregion

        #region Public API

        public bool IsEditing ()
        {
            return State != SwipeActionViewState.Normal;
        }

        public void EndEditing (bool animated = true)
        {
            if (State != SwipeActionViewState.Normal) {
                HideActions (animated);
            }
        }

        public void Cleanup ()
        {
            RemoveGestureRecognizer (PanGestureRecognizer);
            PanGestureRecognizer.WeakDelegate = null;
            PanGestureRecognizer = null;
            ContentView.RemoveGestureRecognizer (TapGestureRecognizer);
            TapGestureRecognizer.WeakDelegate = null;
            TapGestureRecognizer = null;

            foreach (var actionView in ActionViews) {
                actionView.SwipeView = null;
                actionView.Cleanup ();
            }
        }

        #endregion

        #region API for ActionViews

        public void ActionTapped (SwipeAction action)
        {
            if (State != SwipeActionViewState.Normal) {
                HideActions ();
                Delegate.SwipeViewDidSelectAction (this, action);
            }
        }

        #endregion

        #region Gestures

        [Export ("gestureRecognizerShouldBegin:")]
        public bool ShouldBegin (UIGestureRecognizer recognizer)
        {
            if (recognizer == TapGestureRecognizer) {
                return ShouldTap ();
            } else if (recognizer == PanGestureRecognizer) {
                return ShouldPan ();
            }
            return false;
        }

        bool ShouldTap ()
        {
            return State != SwipeActionViewState.Normal;
        }

        void DidTap ()
        {
            HideActions ();
        }

        bool ShouldPan ()
        {
            if (Enabled) {
                if (State == SwipeActionViewState.Normal) {
                    var translation = PanGestureRecognizer.TranslationInView (this);
                    if (Math.Abs (translation.X) > Math.Abs (translation.Y)) {
                        if (translation.X > 0.0f) {
                            var actions = Delegate.ActionsForViewSwipingRight (this);
                            if (actions != null && actions.Count > 0) {
                                ConfigureActions (actions, SwipeActionViewState.ShowingSwipeRightActions);
                                return true;
                            }
                        } else if (translation.X < 0.0f) {
                            var actions = Delegate.ActionsForViewSwipingLeft (this);
                            if (actions != null && actions.Count > 0) {
                                ConfigureActions (actions, SwipeActionViewState.ShowingSwipeLeftActions);
                                return true;
                            }
                        }
                    }
                } else {
                    return true;
                }
            }
            return false;
        }

        void DidPan ()
        {
            if (PanGestureRecognizer.State == UIGestureRecognizerState.Began) {
                if (State == SwipeActionViewState.ShowingSwipeRightActions) {
                    TranslationAdjustment = _ActionsView.Frame.Width;
                } else if (State == SwipeActionViewState.ShowingSwipeLeftActions) {
                    TranslationAdjustment = -_ActionsView.Frame.Width;
                } else {
                    TranslationAdjustment = 0.0f;
                }
            } else if (PanGestureRecognizer.State == UIGestureRecognizerState.Changed) {
                if (State == SwipeActionViewState.Normal) {
                    var translation = PanGestureRecognizer.TranslationInView (this);
                    if (translation.X > 0) {
                        State = SwipeActionViewState.ShowingSwipeRightActions;
                        Delegate.SwipeViewWillBeginShowingActions (this);
                    } else if (translation.X < 0) {
                        State = SwipeActionViewState.ShowingSwipeLeftActions;
                        Delegate.SwipeViewWillBeginShowingActions (this);
                    }
                }
                ContinuePan ();
            } else if (PanGestureRecognizer.State == UIGestureRecognizerState.Ended) {
                if (State != SwipeActionViewState.Normal) {
                    EndPan ();
                }
            } else if (PanGestureRecognizer.State == UIGestureRecognizerState.Cancelled) {
                if (State != SwipeActionViewState.Normal) {
                    EndPan ();
                }
            }
        }

        private nfloat GetFullActionWidth ()
        {
            return (nfloat)Math.Min (Bounds.Width / 2.0f, ActionViews.Count * PreferredActionWidth);
        }

        private void ContinuePan ()
        {
            var translation = PanGestureRecognizer.TranslationInView (this);
            var direction = State == SwipeActionViewState.ShowingSwipeLeftActions ? -1 : 1;
            var fullWidth = GetFullActionWidth ();
            var x = translation.X + TranslationAdjustment;
            x *= direction;
            var over = x - fullWidth;
            var under = -x;
            if (over > 0.0f) {
                var dampener = fullWidth;
                x = fullWidth + (dampener * over) / (over + dampener);
            } else if (under > 0.0f) {
                var dampener = PreferredActionWidth;
                x = 0.0f - (dampener * under) / (under + dampener);
            }
            ActionRevealPercentage = x / fullWidth;
            SetNeedsLayout ();
        }

        private void EndPan ()
        {
            nfloat maxDuration = 0.25f;
            nfloat minDuration = 1.0f / 60.0f;
            nfloat bounceDuration = 0.14f;
            var velocity = PanGestureRecognizer.VelocityInView (this);
            var fullActionWidth = GetFullActionWidth ();
            var stateAfterAnimation = State;
            var direction = State == SwipeActionViewState.ShowingSwipeLeftActions ? -1 : 1;
            var directionAdjustedVelocity = velocity.X * direction;
            nfloat friction = -2000.0f;
            if (directionAdjustedVelocity < 0.0f) {
                friction = -friction;
            }

            var animations = new List<AnimationBlock> ();

            nfloat targetRevealPercentage;
            bool bounce = true;

            if (directionAdjustedVelocity < 0.0f) {
                // we're swiping towards closed
                if (ActionRevealPercentage > 1.0f) {
                    // but we're still past fully open, so land at open
                    targetRevealPercentage = 1.0f;
                } else {
                    // otherwise, land at closed
                    stateAfterAnimation = SwipeActionViewState.Normal;
                    targetRevealPercentage = 0.0f;
                }
            } else if (directionAdjustedVelocity > 0.0f) {
                // we're swiping toward open
                if (ActionRevealPercentage < 0.0f) {
                    // but we're behind the closed point, so land at closed
                    stateAfterAnimation = SwipeActionViewState.Normal;
                    targetRevealPercentage = 0.0f;
                } else {
                    // otherwise, land at open
                    targetRevealPercentage = 1.0f;
                }
            } else {
                // we're not swiping either way, so land at the nearest point
                bounce = false;
                if (ActionRevealPercentage >= 0.5f) {
                    targetRevealPercentage = 1.0f;
                } else {
                    stateAfterAnimation = SwipeActionViewState.Normal;
                    targetRevealPercentage = 0.0f;
                }
            }

            nfloat distanceToTarget = (targetRevealPercentage - ActionRevealPercentage) * fullActionWidth;
            nfloat timeToVelocity0 = -directionAdjustedVelocity / friction;
            //nfloat distanceToVelocity0 = directionAdjustedVelocity * timeToVelocity0 + 0.5f * friction * timeToVelocity0 * timeToVelocity0;
            nfloat timeToTarget = maxDuration;
            if (directionAdjustedVelocity != 0.0f) {
                // if we have a non-zero velocity, we can figure out how long it'll take to get to our target
                var root = directionAdjustedVelocity * directionAdjustedVelocity + 2.0f * friction * distanceToTarget;
                if (root >= 0) {
                    var timeToTargetPlus = -(directionAdjustedVelocity + (nfloat)Math.Sqrt (root)) / friction;
                    var timeToTargetMinus = -(directionAdjustedVelocity - (nfloat)Math.Sqrt (root)) / friction;
                    timeToTarget = (nfloat)Math.Min (timeToTargetPlus, timeToTargetMinus);
                }
                //timeToTarget = distanceToTarget / directionAdjustedVelocity;
                if (bounce && timeToTarget >= maxDuration) {
                    // if we're supposed to bounce, but our velocity is low and we'll take a while to get
                    // to the landing point, just take the max duration to land without a bounce
                    bounce = false;
                    timeToTarget = maxDuration;
                }
            }
            if (bounce) {
                CAMediaTimingFunction timingFunction;
                if (timeToTarget > minDuration) {
                    // We're not yet to the target point, so we'll animate there before doing going past and bouncing back.
                    // Note that we won't bother with this animation if we're moving so fast that timeToTarget is less than
                    // the minimum duration, even if technically we're not yet past the target
                    var timingFunctionFraction = 1.0f - timeToTarget / timeToVelocity0;
                    timingFunction = CAMediaTimingFunction.FromControlPoints (0.0f, 0.0f, 0.44f + (0.56f * (float)timingFunctionFraction), 1.0f);
                    animations.Add (new AnimationBlock (timeToTarget, timingFunction, () => {
                        ActionRevealPercentage = targetRevealPercentage;
                        SetNeedsLayout ();
                        LayoutIfNeeded ();
                    }));
                }
                var duration = bounceDuration;
                nfloat dampener = targetRevealPercentage == 0.0f ? PreferredActionWidth : fullActionWidth;
                nfloat velocityAtBounceStart = timeToTarget > 0.0f ? directionAdjustedVelocity + friction * timeToTarget : directionAdjustedVelocity;
                nfloat distanceInDuration = velocityAtBounceStart * bounceDuration;
                nfloat dampenedDistance;
                if (distanceInDuration >= 0.0f) {
                    dampenedDistance = (distanceInDuration * dampener) / (distanceInDuration + dampener);
                } else {
                    dampenedDistance = (distanceInDuration * dampener) / (dampener - distanceInDuration);
                }
                nfloat apexRevealPercentage = targetRevealPercentage + (dampenedDistance / fullActionWidth);
                if (timeToTarget < 0.0f) {
                    // We're already past the target, but we still need to animate to the apex.  We'll cut short animation
                    // since we don't have to travel the full distance to the apex
                    if (ActionRevealPercentage <= 0.0f) {
                        duration = bounceDuration * (ActionRevealPercentage - apexRevealPercentage);
                    } else {
                        duration = bounceDuration * (apexRevealPercentage - ActionRevealPercentage);
                    }
                }
                if (duration > minDuration) {
                    // Animate to the apex, provided there duration is more than the minumum duration (if it's not,
                    // then we must be at or very near the apex and we only need to animate the snap back)
                    timingFunction = CAMediaTimingFunction.FromControlPoints (0.0f, 0.0f, 0.44f, 1.0f);
                    animations.Add (new AnimationBlock (bounceDuration, timingFunction, () => {
                        ActionRevealPercentage = apexRevealPercentage;
                        SetNeedsLayout ();
                        LayoutIfNeeded ();
                    }));
                }
                // Animate back to the target position
                timingFunction = CAMediaTimingFunction.FromControlPoints (0.56f, 0.0f, 1.0f, 1.0f);
                animations.Add (new AnimationBlock (bounceDuration, timingFunction, () => {
                    ActionRevealPercentage = targetRevealPercentage;
                    SetNeedsLayout ();
                    LayoutIfNeeded ();
                }));
            } else {
                if (timeToTarget < minDuration) {
                    timeToTarget = minDuration;
                }
                animations.Add (new AnimationBlock (timeToTarget, () => {
                    ActionRevealPercentage = targetRevealPercentage;
                    SetNeedsLayout ();
                    LayoutIfNeeded ();
                }));
            }

            Action completion = () => {
                PanGestureRecognizer.Enabled = true;
                TapGestureRecognizer.Enabled = true;
                State = stateAfterAnimation;
                if (State == SwipeActionViewState.Normal) {
                    Delegate.SwipeViewDidEndShowingActions (this);
                }
            };

            Action runRemainingAnimations = null;
            runRemainingAnimations = () => {
                var animation = animations [0];
                animations.RemoveAt (0);
                UIView.BeginAnimations (null, System.IntPtr.Zero);
                UIView.SetAnimationDuration (animation.Duration);
                CATransaction.Begin ();
                CATransaction.AnimationDuration = animation.Duration;
                CATransaction.AnimationTimingFunction = animation.TimingFunction;
                if (animations.Count > 0) {
                    CATransaction.CompletionBlock = runRemainingAnimations;
                } else {
                    CATransaction.CompletionBlock = completion;
                }
                animation.Animations ();
                CATransaction.Commit ();
                UIView.CommitAnimations ();
            };

            PanGestureRecognizer.Enabled = false;
            TapGestureRecognizer.Enabled = false;

            runRemainingAnimations ();
        }

        #endregion

        #region Managing Actions

        void HideActions (bool animated = true)
        {
            ActionRevealPercentage = 0.0f;
            SetNeedsLayout ();
            if (animated) {
                UIView.Animate (0.25f, () => {
                    LayoutIfNeeded ();
                }, () => {
                    State = SwipeActionViewState.Normal;
                    Delegate.SwipeViewDidEndShowingActions (this);
                    PanGestureRecognizer.Enabled = true;
                    TapGestureRecognizer.Enabled = true;
                });
                PanGestureRecognizer.Enabled = false;
                TapGestureRecognizer.Enabled = false;
            } else {
                State = SwipeActionViewState.Normal;
                Delegate.SwipeViewDidEndShowingActions (this);
            }
        }

        void ConfigureActions (List<SwipeAction> actions, SwipeActionViewState forState)
        {
            Queue<ActionView> reusableViews = new Queue<ActionView> ();
            foreach (var view in ActionViews) {
                view.RemoveFromSuperview ();
                reusableViews.Enqueue (view);
            }
            ActionViews.Clear ();
            ActionView actionView;
            foreach (var action in actions) {
                if (reusableViews.Count > 0) {
                    actionView = reusableViews.Dequeue ();
                } else {
                    actionView = new ActionView (new CGRect (0.0f, 0.0f, PreferredActionWidth, ActionsView.Bounds.Height));
                    actionView.SwipeView = this;
                }
                actionView.SetAction (action);
                ActionsView.InsertSubview (actionView, 0);
                ActionViews.Add (actionView);
            }
            foreach (var view in reusableViews) {
                view.SwipeView = null;
                view.Cleanup ();
            }
            ActionsView.State = forState;
            ActionsView.PreferredWidth = GetFullActionWidth ();
            ActionsView.SetNeedsLayout ();
        }

        #endregion

        #region Layout

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            CGRect frame;
            var fullActionWidth = GetFullActionWidth ();
            var scale = Window != null ? Window.Screen.Scale : 1.0f;
            var distance = (nfloat)Math.Floor (fullActionWidth * ActionRevealPercentage * scale) / scale;
            if (_ActionsView != null) {
                frame = _ActionsView.Frame;
                frame.Width = (nfloat)Math.Max (0.0f, distance);
                if (State == SwipeActionViewState.ShowingSwipeLeftActions) {
                    frame.X = Bounds.Width - frame.Width;
                } else {
                    frame.X = 0.0f;
                }
                _ActionsView.Frame = frame;
            }
            frame = ContentView.Frame;
            if (State == SwipeActionViewState.ShowingSwipeLeftActions) {
                frame.X = -distance;
            } else {
                frame.X = distance;
            }
            ContentView.Frame = frame;
        }

        #endregion

        #region Private Helper Classes

        private class AnimationBlock
        {

            public Action Animations;
            public nfloat Duration;
            public CAMediaTimingFunction TimingFunction;

            public AnimationBlock (nfloat duration, CAMediaTimingFunction timingFunction, Action animations)
            {
                Duration = duration;
                TimingFunction = timingFunction;
                Animations = animations;
            }

            public AnimationBlock (nfloat duration, Action animations) : this (duration, CAMediaTimingFunction.FromName (CAMediaTimingFunction.Linear), animations)
            {
            }
        }

        private class ActionsContainerView : UIView
        {

            public nfloat PreferredWidth;
            public SwipeActionViewState State;

            public ActionsContainerView (CGRect frame) : base (frame)
            {
                ClipsToBounds = true;
                PreferredWidth = frame.Width;
            }

            public override void LayoutSubviews ()
            {
                var scale = Window != null ? Window.Screen.Scale : 1.0f;
                var availableWidth = PreferredWidth;
                if (Bounds.Width > PreferredWidth) {
                    availableWidth += (nfloat)Math.Floor ((Bounds.Width - PreferredWidth) / 4.0f * scale) / scale;
                }
                nfloat actionWidth = (nfloat)Math.Floor (availableWidth / Subviews.Length * scale) / scale;
                nfloat remainder = availableWidth - actionWidth * Subviews.Length;
                nfloat pct = (nfloat)Math.Min (1.0f, Bounds.Width / PreferredWidth);
                if (State == SwipeActionViewState.ShowingSwipeLeftActions) {
                    nfloat x = 0.0f;
                    if (Bounds.Width > PreferredWidth) {
                        x += Bounds.Width - actionWidth * Subviews.Length - remainder;
                    }
                    foreach (var subview in Subviews) {
                        subview.Frame = new CGRect ((nfloat)Math.Floor (x * pct * scale) / scale, 0.0f, actionWidth, Bounds.Height);
                        x += actionWidth;
                    }
                    if (Subviews.Length > 0) {
                        var subview = Subviews [Subviews.Length - 1];
                        var frame = subview.Frame;
                        frame.Width += remainder;
                        subview.Frame = frame;
                    }
                } else {
                    nfloat x0 = -actionWidth;
                    nfloat x = actionWidth * (Subviews.Length - 1) + remainder;
                    foreach (var subview in Subviews) {
                        subview.Frame = new CGRect ((nfloat)Math.Floor ((x0 + (x - x0) * pct) * scale) / scale, 0.0f, actionWidth, Bounds.Height);
                        x -= actionWidth;
                    }
                    if (Subviews.Length > 0) {
                        var subview = Subviews [Subviews.Length - 1];
                        var frame = subview.Frame;
                        frame.X -= remainder;
                        frame.Width += remainder;
                        subview.Frame = frame;
                    }
                }
            }
        }

        private class ActionView : UIView
        {

            private UIImageView ImageView;
            private UILabel Label;
            private PressGestureRecognizer PressGestureRecognizer;
            public SwipeActionsView SwipeView;
            public SwipeAction Action;
            bool IsPressing;
            nfloat ImageSize = 24.0f;
            nfloat LabelSpacing = 7.0f;
            nfloat LabelHeight = 14.0f;

            public ActionView (CGRect frame) : base (frame)
            {
                Opaque = true;
                ClipsToBounds = true;
                ImageView = new UIImageView (new CGRect (0.0f, 0.0f, ImageSize, ImageSize));
                ImageView.ContentMode = UIViewContentMode.ScaleAspectFit;
                ImageView.Layer.ShouldRasterize = true;
                Label = new UILabel ();
                Label.Lines = 1;
                Label.Font = UIFont.SystemFontOfSize (UIFont.SmallSystemFontSize);
                AddSubview (ImageView);
                AddSubview (Label);

                PressGestureRecognizer = new PressGestureRecognizer (Tap);
                AddGestureRecognizer (PressGestureRecognizer);
            }

            public void SetAction (SwipeAction action)
            {
                Action = action;
                Update ();
            }

            public void Cleanup ()
            {
                RemoveGestureRecognizer (PressGestureRecognizer);
                PressGestureRecognizer = null;
                Action = null;
            }

            void Update ()
            {
                ImageView.Image = Action.Image.ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate);
                Label.Text = NSBundle.MainBundle.LocalizedString (Action.TitleKey, "");
                BackgroundColor = Action.Color;
                ImageView.TintColor = UIColor.White;
                ImageView.BackgroundColor = Action.Color;
                ImageView.Opaque = true;
                Label.TextColor = UIColor.White;
                Label.SizeToFit ();
                Label.BackgroundColor = Action.Color;
                Label.Opaque = true;
                LabelHeight = (nfloat)Math.Ceiling (Label.Font.LineHeight);
                LabelSpacing = (nfloat)Math.Floor (LabelHeight / 2.0f);
                SetNeedsLayout ();
            }

            void Tap ()
            {
                if (SwipeView != null) {
                    if (PressGestureRecognizer.State == UIGestureRecognizerState.Began) {
                        SetIsPressing (true);
                    } else if (PressGestureRecognizer.State == UIGestureRecognizerState.Ended) {
                        SwipeView.ActionTapped (Action);
                        SetIsPressing (false);
                    } else if (PressGestureRecognizer.State == UIGestureRecognizerState.Changed) {
                        SetIsPressing (PressGestureRecognizer.IsInsideView);
                    } else {
                        SetIsPressing (false);
                    }
                }
            }

            void SetIsPressing (bool isPressing)
            {
                if (IsPressing && !isPressing) {
                    Label.BackgroundColor = ImageView.BackgroundColor = BackgroundColor = Action.Color;
                } else if (!IsPressing && isPressing) {
                    Label.BackgroundColor = ImageView.BackgroundColor = BackgroundColor = Action.Color.ColorDarkenedByAmount (0.2f);
                }
                IsPressing = isPressing;
            }

            public override void LayoutSubviews ()
            {
                nfloat preferredWidth = Bounds.Width;
                var containerView = Superview as ActionsContainerView;
                nfloat offset = 0.0f;
                if (containerView != null) {
                    preferredWidth = containerView.PreferredWidth / containerView.Subviews.Length;
                    if (containerView.State == SwipeActionViewState.ShowingSwipeRightActions) {
                        offset = Bounds.Width - preferredWidth;
                    }
                }
                base.LayoutSubviews ();
                nfloat contentHeight = ImageSize + LabelSpacing + Label.Font.LineHeight;
                if (contentHeight < Bounds.Height) {
                    ImageView.Hidden = false;
                    ImageView.Frame = new CGRect (offset + (preferredWidth - ImageSize) / 2.0f, (Bounds.Height - contentHeight) / 2.0f, ImageSize, ImageSize);
                    Label.Frame = new CGRect (offset + (preferredWidth - Label.Frame.Width) / 2.0f, ImageView.Frame.Y + ImageView.Frame.Height + LabelSpacing, Label.Frame.Width, LabelHeight);
                } else {
                    ImageView.Hidden = true;
                    Label.Frame = new CGRect (offset + (preferredWidth - Label.Frame.Width) / 2.0f, (Bounds.Height - Label.Font.LineHeight) / 2.0f, Label.Frame.Width, LabelHeight);
                }
            }

        }

        #endregion

    }
}

