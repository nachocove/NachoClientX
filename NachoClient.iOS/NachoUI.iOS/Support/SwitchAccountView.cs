//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using Foundation;
using UIKit;
using CoreGraphics;
using CoreAnimation;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.iOS
{
    public class SwitchAccountView : UIView
    {

        UIView BackgroundView;
        UIView FakeNavigationBar;
        UIView NavigationBarExtension;
        UILabel TitleLabel;

        public Action Canceled;
        public Action<McAccount> AccountPicked;

        bool _IsCollapsed;
        bool IsCollapsed {
            get {
                return _IsCollapsed;
            }
            set {
                _IsCollapsed = value;
                SetNeedsLayout ();
            }
        }

        List<AccountView> AccountViews;

        int SelectedAccountId;
        int LeftAccountId;
        int RightAccountId;

        int ColumnsPerRow = 3;
        nfloat RowHeight = 64.0f;

        UITapGestureRecognizer BackgroundTapRecognizer;

        CGPoint SelectedAccountCenter;

        public SwitchAccountView (CGRect frame) : base (frame)
        {

            BackgroundView = new UIView (Bounds);
            BackgroundView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            BackgroundView.BackgroundColor = UIColor.Black.ColorWithAlpha (0.4f);

            FakeNavigationBar = new UIView (new CGRect (0.0f, 0.0f, Bounds.Width, 64.0f));
            FakeNavigationBar.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            FakeNavigationBar.BackgroundColor = A.Color_NachoGreen;

            NavigationBarExtension = new UIView (new CGRect (0.0f, 64.0f, Bounds.Width, 128.0f));
            NavigationBarExtension.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            NavigationBarExtension.BackgroundColor = A.Color_NachoGreen;

            TitleLabel = new UILabel (new CGRect (0.0f, 20.0f, Bounds.Width, 44.0f));
            TitleLabel.TextAlignment = UITextAlignment.Center;
            TitleLabel.Font = A.Font_AvenirNextDemiBold17;
            TitleLabel.TextColor = UIColor.White;
            TitleLabel.Text = "Switch Accounts";

            BackgroundTapRecognizer = new UITapGestureRecognizer (TapBackground);
            BackgroundView.AddGestureRecognizer (BackgroundTapRecognizer);

            AddSubview (BackgroundView);
            AddSubview (FakeNavigationBar);
            AddSubview (NavigationBarExtension);
            AddSubview (TitleLabel);

            AccountViews = new List<AccountView> ();

        }

        void SwitchToAccountView (AccountView accountView)
        {
            AccountPicked (accountView.AccountInfo.Account);
        }

        public void Cleanup ()
        {
            BackgroundView.RemoveGestureRecognizer (BackgroundTapRecognizer);
            BackgroundTapRecognizer = null;
            foreach (var accountView in AccountViews) {
                accountView.Cleanup ();
            }
            AccountViews.Clear ();
        }

        public void TapBackground ()
        {
            Canceled ();
        }

        nfloat InteractiveAnimationDuration = 0.25f;

        public void PrepareForInteractivePresentation ()
        {
            UpdateAccountViews ();
            IsCollapsed = true;
            LayoutIfNeeded ();
            UIView.BeginAnimations (null, IntPtr.Zero);
            UIView.SetAnimationDuration (InteractiveAnimationDuration);
            UIView.SetAnimationCurve (UIViewAnimationCurve.EaseInOut);
            CATransaction.Begin ();
            CATransaction.AnimationDuration = InteractiveAnimationDuration;
            CATransaction.AnimationTimingFunction = CAMediaTimingFunction.FromName(CAMediaTimingFunction.EaseInEaseOut);
            CATransaction.DisableActions = false;
            IsCollapsed = false;
            LayoutIfNeeded ();
            CATransaction.Commit ();
            UIView.CommitAnimations ();
            Layer.Speed = 0.0f;
            Layer.TimeOffset = 0.0;
        }

        public void SetVisibleAccounts (CGPoint selectedAccountCenter, int selectedAccountId, int leftAccountId, int rightAccountId)
        {
            SelectedAccountCenter = selectedAccountCenter;
            SelectedAccountId = selectedAccountId;
            LeftAccountId = leftAccountId;
            RightAccountId = rightAccountId;
        }

        void UpdateAccountViews ()
        {
            AccountView accountView;
            var reusableViews = new Queue<AccountView> ();
            foreach (var view in AccountViews) {
                reusableViews.Enqueue (view);
            }
            AccountViews.Clear ();
            foreach (var accountInfo in NcAccountMonitor.Instance.Accounts) {
                if (reusableViews.Count > 0) {
                    accountView = reusableViews.Dequeue ();
                } else {
                    accountView = new AccountView (new CGRect(0.0f, 0.0f, 40.0f, 40.0f));
                    accountView.SwitchView = new WeakReference<SwitchAccountView> (this);
                    AddSubview (accountView);
                    AccountViews.Add (accountView);
                }
                accountView.SetAccountInfo (accountInfo);
            }
            while (reusableViews.Count > 0){
                accountView = reusableViews.Dequeue ();
                accountView.Cleanup ();
                accountView.RemoveFromSuperview ();
            }
        }

        public void AnimateClosed (Action completionHandler)
        {
            UIView.Animate (0.25f, 0.0f, UIViewAnimationOptions.CurveEaseIn, () => {
                IsCollapsed = true;
                LayoutIfNeeded ();
            }, completionHandler);
        }

        public void SetPresentationPercentage (double percentage)
        {
            var animation = BackgroundView.Layer.AnimationForKey (BackgroundView.Layer.AnimationKeys [0]);
            Layer.TimeOffset = percentage * animation.Duration;
            if (percentage == 1.0) {
                // Not sure why, but setting Layer.TimeOffset to animation.Duration (when percentage == 1.0)
                // doesn't always result in a completed animation.  Setting the layer to run at normal speed
                // seems to help the animation system run all the way to completion.
                Layer.Speed = 1.0f;
            }
        }

        void LayoutExpanded ()
        {
            BackgroundView.Alpha = 1.0f;
            FakeNavigationBar.Alpha = 1.0f;
            TitleLabel.Alpha = 1.0f;
            TitleLabel.Transform = CGAffineTransform.MakeIdentity ();
            int itemCount = AccountViews.Count;
            int rows = itemCount / ColumnsPerRow + ((itemCount % ColumnsPerRow) > 0 ? 1 : 0);
            NavigationBarExtension.Frame = new CGRect (0.0f, FakeNavigationBar.Frame.Y + FakeNavigationBar.Frame.Height, Bounds.Width, rows * RowHeight);
            int row = 0;
            int col = 0;
            nfloat colWidth = Bounds.Width / (ColumnsPerRow + 1);
            var center0 = new CGPoint (colWidth, NavigationBarExtension.Frame.Y + RowHeight / 2.0f);
            foreach (var accountView in AccountViews) {
                accountView.Alpha = 1.0f;
                accountView.Transform = CGAffineTransform.MakeIdentity ();
                accountView.AccountImageView.Alpha = 1.0f;
                accountView.UnreadIndicator.Alpha = 1.0f;
                accountView.UnreadOnLeft = false;
                accountView.Frame = new CGRect (0.0f, 0.0f, 40.0f, 40.0f);
                accountView.Center = new CGPoint (center0.X + col * colWidth, center0.Y + row * RowHeight);
                accountView.LayoutIfNeeded ();
                col += 1;
                if (col == ColumnsPerRow) {
                    row += 1;
                    col = 0;
                }
            }
        }

        void LayoutCollapsed ()
        {
            BackgroundView.Alpha = 0.0f;
            FakeNavigationBar.Alpha = 0.0f;
            TitleLabel.Alpha = 0.0f;
            TitleLabel.Transform = CGAffineTransform.MakeTranslation (0.0f, -44.0f);
            NavigationBarExtension.Frame = new CGRect (0.0f, FakeNavigationBar.Frame.Y + FakeNavigationBar.Frame.Height, Bounds.Width, 0.0f);
            foreach (var accountView in AccountViews) {
                accountView.Frame = new CGRect(0.0f, 0.0f, 40.0f, 40.0f);
                accountView.Center = SelectedAccountCenter;
                accountView.UnreadOnLeft = false;
                if (accountView.AccountInfo.Account.Id == SelectedAccountId) {
                    accountView.Alpha = 1.0f;
                    accountView.UnreadIndicator.Alpha = 0.0f;
                } else if (accountView.AccountInfo.Account.Id == LeftAccountId) {
                    accountView.Frame = new CGRect (0.0f, 0.0f, 30.0f, 30.0f);
                    accountView.Center = SelectedAccountCenter;
                    accountView.Transform = CGAffineTransform.MakeTranslation (-accountView.Frame.Width, 0.0f);
                    accountView.Alpha = 1.0f;
                    accountView.AccountImageView.Alpha = 0.5f;
                    accountView.UnreadOnLeft = true;
                } else if (accountView.AccountInfo.Account.Id == RightAccountId) {
                    accountView.Frame = new CGRect (0.0f, 0.0f, 30.0f, 30.0f);
                    accountView.Center = SelectedAccountCenter;
                    accountView.Transform = CGAffineTransform.MakeTranslation (accountView.Frame.Width, 0.0f);
                    accountView.Alpha = 1.0f;
                    accountView.AccountImageView.Alpha = 0.5f;
                } else {
                    accountView.Alpha = 0.0f;
                }
                accountView.LayoutIfNeeded ();
            }
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            if (_IsCollapsed) {
                LayoutCollapsed ();
            } else {
                LayoutExpanded ();
            }
        }

        void OrderSubviews ()
        {
            AccountView selectedView = null;
            foreach (var accountView in AccountViews) {
                if (accountView.AccountInfo.Account.Id == SelectedAccountId) {
                    selectedView = accountView;
                    break;
                }
            }
            if (selectedView != null) {
                AddSubview (selectedView);
            }
        }

        private class AccountView : UIView
        {

            public readonly UIImageView AccountImageView;
            public readonly UILabel UnreadIndicator;
            public bool UnreadOnLeft;
            public NcAccountMonitor.AccountInfo AccountInfo { get; private set; }

            public nfloat IndicatorSize = 14.0f;
            public UIOffset IndicatorOffset = new UIOffset (-3.0f, -3.0f);

            PressGestureRecognizer PressRecognizer;
            public WeakReference<SwitchAccountView> SwitchView;

            public AccountView (CGRect frame) : base (frame)
            {
                AccountImageView = new UIImageView (Bounds);
                AccountImageView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
                AccountImageView.Layer.CornerRadius = AccountImageView.Frame.Width / 2.0f;
                AccountImageView.ClipsToBounds = true;

                UnreadIndicator = new UILabel (new CGRect(0.0f, 0.0f, IndicatorSize, IndicatorSize));
                UnreadIndicator.BackgroundColor = A.Color_NachoGreen;
                UnreadIndicator.ClipsToBounds = true;
                UnreadIndicator.Layer.BorderWidth = 1.0f;
                UnreadIndicator.Layer.BorderColor = A.Color_NachoBlue.CGColor;
                UnreadIndicator.Font = A.Font_AvenirNextRegular10.WithSize (9.0f);
                UnreadIndicator.TextAlignment = UITextAlignment.Center;
                UnreadIndicator.TextColor = A.Color_NachoBlue;
                UnreadIndicator.Layer.CornerRadius = IndicatorSize / 2.0f;

                PressRecognizer = new PressGestureRecognizer (Press);
                AddGestureRecognizer (PressRecognizer);

                AddSubview (AccountImageView);
                AddSubview (UnreadIndicator);
            }

            public void Cleanup ()
            {
                RemoveGestureRecognizer (PressRecognizer);
                PressRecognizer = null;
            }

            void Press ()
            {
                if (PressRecognizer.State == UIGestureRecognizerState.Began) {
                    AccountImageView.Alpha = 0.6f;
                } else if (PressRecognizer.State == UIGestureRecognizerState.Ended) {
                    SwitchAccountView switcher;
                    if (SwitchView.TryGetTarget (out switcher)) {
                        switcher.SwitchToAccountView (this);
                    }
                    AccountImageView.Alpha = 1.0f;
                }else if (PressRecognizer.State == UIGestureRecognizerState.Changed) {
                    AccountImageView.Alpha = PressRecognizer.IsInsideView ? 0.6f : 1.0f;
                } else if (PressRecognizer.State == UIGestureRecognizerState.Failed) {
                    AccountImageView.Alpha = 1.0f;
                } else if (PressRecognizer.State == UIGestureRecognizerState.Cancelled) {
                    AccountImageView.Alpha = 1.0f;
                }
            }

            public void SetAccountInfo (NcAccountMonitor.AccountInfo info)
            {
                AccountInfo = info;
                AccountImageView.Image = Util.ImageForAccount (info.Account);
                UnreadIndicator.Text = info.UnreadCount.ToString ();
                UnreadIndicator.Hidden = info.UnreadCount == 0;
                SetNeedsLayout ();
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                var animations = Layer.AnimationKeys;
                if (animations != null && animations.Length > 0) {
                    var animation = Layer.AnimationForKey (animations [0]);
                    var cornerAnimation = CABasicAnimation.FromKeyPath ("cornerRadius");
                    cornerAnimation.Duration = animation.Duration;
                    cornerAnimation.TimingFunction = animation.TimingFunction;
                    cornerAnimation.From = new NSNumber (AccountImageView.Layer.CornerRadius);
                    AccountImageView.Layer.CornerRadius = AccountImageView.Frame.Width / 2.0f;
                    cornerAnimation.To = new NSNumber (AccountImageView.Layer.CornerRadius);
                    AccountImageView.Layer.AddAnimation (cornerAnimation, "cornerRadius");
                } else {
                    AccountImageView.Layer.CornerRadius = AccountImageView.Frame.Width / 2.0f;
                }
                var size = UnreadIndicator.SizeThatFits (new CGSize (IndicatorSize, IndicatorSize));
                var frame = UnreadIndicator.Frame;
                frame.Width = (nfloat)Math.Max (IndicatorSize, size.Width);
                frame.X = UnreadOnLeft ? IndicatorOffset.Horizontal : Bounds.Width - IndicatorOffset.Horizontal - IndicatorSize;
                frame.Y = IndicatorOffset.Vertical;
                frame.Height = IndicatorSize;
                UnreadIndicator.Frame = frame;
            }

        }

    }
}

