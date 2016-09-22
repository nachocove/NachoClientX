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
    public class SwitchAccountView : UIView, ThemeAdopter
    {

        UIView BackgroundView;
        UIView FakeNavigationBar;
        UIView NavigationBarExtension;
        UILabel TitleLabel;
        AccountActionButton AddButton;

        public Action Canceled;
        public Action AddAccount;
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
        AccountView SelectedAccountView;

        int SelectedAccountId;
        int LeftAccountId;
        int RightAccountId;

        int ColumnsPerRow = 3;

        UITapGestureRecognizer BackgroundTapRecognizer;

        CGPoint SelectedAccountCenter;

        public SwitchAccountView (CGRect frame) : base (frame)
        {

            BackgroundView = new UIView (Bounds);
            BackgroundView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            BackgroundView.BackgroundColor = UIColor.Black.ColorWithAlpha (0.4f);

            FakeNavigationBar = new UIView (new CGRect (0.0f, 0.0f, Bounds.Width, 64.0f));
            FakeNavigationBar.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;

            NavigationBarExtension = new UIView (new CGRect (0.0f, 64.0f, Bounds.Width, 128.0f));
            NavigationBarExtension.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;

            TitleLabel = new UILabel (new CGRect (0.0f, 20.0f, Bounds.Width, 44.0f));
            TitleLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            TitleLabel.TextAlignment = UITextAlignment.Center;
            TitleLabel.Text = "Switch Accounts";

            AddButton = new AccountActionButton (new CGRect (0.0f, 0.0f, 40.0f, 40.0f));
            AddButton.ImageView.Image = UIImage.FromBundle ("add-account").ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate);
            AddButton.TitleLabel.Text = "Add Account";
            AddButton.Action = HandleAddAccount;

            BackgroundTapRecognizer = new UITapGestureRecognizer (TapBackground);
            BackgroundView.AddGestureRecognizer (BackgroundTapRecognizer);

            AddSubview (BackgroundView);
            AddSubview (FakeNavigationBar);
            AddSubview (NavigationBarExtension);
            AddSubview (TitleLabel);
            AddSubview (AddButton);

            AccountViews = new List<AccountView> ();

        }

        #region

        Theme adoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (theme != adoptedTheme) {
                adoptedTheme = theme;
                TitleLabel.Font = theme.BoldDefaultFont.WithSize (17.0f);
                TitleLabel.TextColor = theme.NavigationBarTitleColor;
                FakeNavigationBar.BackgroundColor = theme.NavigationBarBackgroundColor;
                NavigationBarExtension.BackgroundColor = theme.NavigationBarBackgroundColor;
                AddButton.AdoptTheme (theme);
                foreach (var view in AccountViews) {
                    view.AdoptTheme (theme);
                }
            }
        }

        #endregion

        void SwitchToAccountView (AccountView accountView)
        {
            AccountPicked (accountView.AccountInfo.Account);
        }

        void HandleAddAccount ()
        {
            if (AddAccount != null) {
                AddAccount ();
            }
        }

        public void Cleanup ()
        {
            BackgroundView.RemoveGestureRecognizer (BackgroundTapRecognizer);
            BackgroundTapRecognizer = null;
            foreach (var accountView in AccountViews) {
                accountView.Cleanup ();
            }
            SelectedAccountView = null;
            AccountViews.Clear ();
            AddButton.Action = null;
            AddButton.Cleanup ();
        }

        public void TapBackground ()
        {
            Canceled ();
        }

        nfloat InteractiveAnimationDuration = 0.25f;

        public void PrepareForInteractivePresentation ()
        {
            UpdateAccountViews ();
            OrderSubviews ();
            DisableUserInteraction ();
            IsCollapsed = true;
            LayoutIfNeeded ();
            UIView.Animate (InteractiveAnimationDuration, 0.0f, UIViewAnimationOptions.CurveEaseInOut, () => {
                IsCollapsed = false;
                LayoutIfNeeded ();
            }, null);
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
            var accounts = new List<NcAccountMonitor.AccountInfo> ();
            AccountViews.Clear ();
            if (NcAccountMonitor.Instance.Accounts.Count > 1) {
                accounts.Add (new NcAccountMonitor.AccountInfo () {
                    Account = McAccount.GetUnifiedAccount ()
                });
            }
            accounts.AddRange (NcAccountMonitor.Instance.Accounts);
            foreach (var accountInfo in accounts) {
                if (reusableViews.Count > 0) {
                    accountView = reusableViews.Dequeue ();
                } else {
                    accountView = new AccountView (new CGRect(0.0f, 0.0f, 40.0f, 40.0f));
                    accountView.SwitchView = new WeakReference<SwitchAccountView> (this);
                    AddSubview (accountView);
                    AccountViews.Add (accountView);
                }
                if (adoptedTheme != null) {
                    accountView.AdoptTheme (adoptedTheme);
                }
                if (accountInfo.Account.Id == SelectedAccountId) {
                    SelectedAccountView = accountView;
                }
                accountView.SetHighlighted (false);
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
            DisableUserInteraction ();
            UIView.Animate (0.25f, 0.0f, UIViewAnimationOptions.CurveEaseIn, () => {
                IsCollapsed = true;
                LayoutIfNeeded ();
            }, completionHandler);
        }

        public void Close ()
        {
            IsCollapsed = true;
            LayoutIfNeeded ();
        }

        void EnableUserInteraction ()
        {
            foreach (var view in AccountViews) {
                view.UserInteractionEnabled = true;
            }
            AddButton.UserInteractionEnabled = true;
        }

        void DisableUserInteraction ()
        {
            foreach (var view in AccountViews) {
                view.UserInteractionEnabled = false;
            }
            AddButton.UserInteractionEnabled = false;
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
                EnableUserInteraction ();
            }
        }

        void LayoutExpanded ()
        {
            BackgroundView.Alpha = 1.0f;
            FakeNavigationBar.Alpha = 1.0f;
            TitleLabel.Alpha = 1.0f;
            TitleLabel.Transform = CGAffineTransform.MakeIdentity ();
            int itemCount = AccountViews.Count + 1;
            int rows = itemCount / ColumnsPerRow + ((itemCount % ColumnsPerRow) > 0 ? 1 : 0);
            int row = 0;
            int col = 0;
            nfloat expandedAccountViewHeight = 40.0f + adoptedTheme.DefaultFont.WithSize (14.0f).RoundedLineHeight (1.0f) * 1.5f;
            nfloat rowSpacing = 30.0f;
            nfloat rowHeight = expandedAccountViewHeight + rowSpacing;
            nfloat colWidth = Bounds.Width / (ColumnsPerRow + 1);
            var center0 = new CGPoint (colWidth, NavigationBarExtension.Frame.Y + rowSpacing + expandedAccountViewHeight / 2.0f);
            NavigationBarExtension.Frame = new CGRect (0.0f, FakeNavigationBar.Frame.Y + FakeNavigationBar.Frame.Height, Bounds.Width, rowSpacing + rows * rowHeight);
            foreach (var accountView in AccountViews) {
                accountView.Alpha = 1.0f;
                accountView.Transform = CGAffineTransform.MakeIdentity ();
                accountView.AccountImageView.Alpha = 1.0f;
                accountView.UnreadIndicator.Alpha = 1.0f;
                accountView.UnreadOnLeft = false;
                accountView.ImageSize = 40.0f;
                accountView.NameLabel.Alpha = 1.0f;
                accountView.RecentUnreadIndicator.Alpha = 0.0f;
                accountView.UnreadIndicator.Alpha = 1.0f;
                accountView.HighlightView.Alpha = 1.0f;
                DelayLabelOpacityAnimation (accountView.NameLabel.Layer);
                accountView.Frame = new CGRect (0.0f, 0.0f, colWidth, expandedAccountViewHeight);
                accountView.Center = new CGPoint (center0.X + col * colWidth, center0.Y + row * rowHeight);
                accountView.LayoutIfNeeded ();
                col += 1;
                if (col == ColumnsPerRow) {
                    row += 1;
                    col = 0;
                }
            }
            AddButton.ImageView.Alpha = 1.0f;
            AddButton.TitleLabel.Alpha = 1.0f;
            DelayLabelOpacityAnimation (AddButton.TitleLabel.Layer);
            AddButton.Frame = new CGRect (0.0f, 0.0f, colWidth, expandedAccountViewHeight);
            AddButton.Center = new CGPoint (center0.X + col * colWidth, center0.Y + row * rowHeight);
            AddButton.LayoutIfNeeded ();
        }

        static void DelayLabelOpacityAnimation (CALayer layer)
        {
            var animation = layer.AnimationForKey ("opacity");
            if (animation != null) {
                var adjustedAnimation = CAKeyFrameAnimation.FromKeyPath ("opacity");
                adjustedAnimation.Duration = animation.Duration;
                adjustedAnimation.Values = new NSObject[] {
                    new NSNumber(0.0f),
                    new NSNumber(0.0f),
                    new NSNumber(1.0f),
                };
                adjustedAnimation.KeyTimes = new NSNumber[] {
                    new NSNumber (0.0f),
                    new NSNumber (0.5f),
                    new NSNumber (1.0f)
                };
                layer.RemoveAnimation ("opacity");
                layer.AddAnimation (adjustedAnimation, "opacity");
            }
        }

        static void SpeedLabelOpacityAnimation (CALayer layer)
        {
            var animation = layer.AnimationForKey ("opacity");
            if (animation != null) {
                var adjustedAnimation = CAKeyFrameAnimation.FromKeyPath ("opacity");
                adjustedAnimation.Duration = animation.Duration;
                adjustedAnimation.Values = new NSObject[] {
                    new NSNumber(1.0f),
                    new NSNumber(0.0f),
                    new NSNumber(0.0f),
                };
                adjustedAnimation.KeyTimes = new NSNumber[] {
                    new NSNumber (0.0f),
                    new NSNumber (0.5f),
                    new NSNumber (1.0f)
                };
                layer.RemoveAnimation ("opacity");
                layer.AddAnimation (adjustedAnimation, "opacity");
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
                accountView.ImageSize = 40.0f;
                accountView.UnreadOnLeft = false;
                accountView.UnreadIndicator.Alpha = 0.0f;
                accountView.HighlightView.Alpha = 0.0f;
                if (accountView.AccountInfo.Account.Id == SelectedAccountId) {
                    accountView.Alpha = 1.0f;
                    accountView.RecentUnreadIndicator.Alpha = 0.0f;
                    accountView.Transform = CGAffineTransform.MakeIdentity ();
                } else if (accountView.AccountInfo.Account.Id == LeftAccountId) {
                    accountView.ImageSize = 30.0f;
                    accountView.RecentUnreadIndicator.Alpha = 1.0f;
                    accountView.Transform = CGAffineTransform.MakeTranslation (-accountView.ImageSize, 0.0f);
                    accountView.Alpha = 1.0f;
                    accountView.AccountImageView.Alpha = 0.5f;
                    accountView.UnreadOnLeft = true;
                } else if (accountView.AccountInfo.Account.Id == RightAccountId) {
                    accountView.ImageSize = 30.0f;
                    accountView.RecentUnreadIndicator.Alpha = 1.0f;
                    accountView.Transform = CGAffineTransform.MakeTranslation (accountView.ImageSize, 0.0f);
                    accountView.Alpha = 1.0f;
                    accountView.AccountImageView.Alpha = 0.5f;
                } else {
                    accountView.Alpha = 0.0f;
                    accountView.RecentUnreadIndicator.Alpha = 0.0f;
                    accountView.Transform = CGAffineTransform.MakeIdentity ();
                }
                accountView.NameLabel.Alpha = 0.0f;
                SpeedLabelOpacityAnimation (accountView.NameLabel.Layer);
                accountView.Frame = new CGRect(0.0f, 0.0f, accountView.ImageSize, accountView.ImageSize);
                accountView.Center = SelectedAccountCenter;
                accountView.LayoutIfNeeded ();
            }
            AddButton.ImageView.Alpha = 0.0f;
            AddButton.TitleLabel.Alpha = 0.0f;
            SpeedLabelOpacityAnimation (AddButton.TitleLabel.Layer);
            AddButton.Frame = new CGRect (0.0f, 0.0f, 40.0f, 40.0f);
            AddButton.Center = SelectedAccountCenter;
            AddButton.LayoutIfNeeded ();
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
            if (SelectedAccountView != null) {
                AddSubview (SelectedAccountView);
            }
        }

        void HighlightView (AccountView view)
        {
            foreach (var accountView in AccountViews) {
                if (view == accountView) {
                    accountView.SetHighlighted (true);
                    accountView.SetPressed (true);
                } else {
                    accountView.SetHighlighted (false);
                    accountView.SetPressed (false);
                }
            }
        }

        AccountView TouchedAccountView (NSSet touches, UIEvent evt)
        {
            var touch = touches.AnyObject as UITouch;
            var location = touch.LocationInView (this);
            AccountView touchtedView = null;
            foreach (var view in AccountViews) {
                if (view.PointInside (view.ConvertPointFromView (location, this), evt)) {
                    touchtedView = view;
                    break;
                }
            }
            return touchtedView;
        }

        public override void TouchesBegan (NSSet touches, UIEvent evt)
        {
            var touchedView = TouchedAccountView (touches, evt);
            HighlightView (touchedView);
        }

        public override void TouchesMoved (NSSet touches, UIEvent evt)
        {
            var touchedView = TouchedAccountView (touches, evt);
            HighlightView (touchedView);
        }

        public override void TouchesEnded (NSSet touches, UIEvent evt)
        {
            var touchedView = TouchedAccountView (touches, evt);
            if (touchedView != null) {
                SwitchToAccountView (touchedView);
            }
        }

        private class AccountActionButton : UIView, ThemeAdopter
        {

            public readonly UIImageView ImageView;
            public readonly UILabel TitleLabel;

            public Action Action;

            PressGestureRecognizer PressRecognizer;

            nfloat ImageSize = 40.0f;

            public AccountActionButton (CGRect frame) : base (frame)
            {
                ImageView = new UIImageView (new CGRect(0.0f, 0.0f, ImageSize, ImageSize));

                TitleLabel = new UILabel ();
                TitleLabel.TextAlignment = UITextAlignment.Center;
                TitleLabel.LineBreakMode = UILineBreakMode.Clip;

                PressRecognizer = new PressGestureRecognizer (Press);
                AddGestureRecognizer (PressRecognizer);

                AddSubview (ImageView);
                AddSubview (TitleLabel);
            }

            public void AdoptTheme (Theme theme)
            {
                ImageView.TintColor = theme.NavigationBarTintColor;
                TitleLabel.Font = theme.DefaultFont.WithSize (14.0f);
                TitleLabel.TextColor = theme.AccountSwitcherTextColor;
                SetNeedsLayout ();
            }

            public void Cleanup ()
            {
                RemoveGestureRecognizer (PressRecognizer);
                PressRecognizer = null;
            }

            void Press ()
            {
                if (PressRecognizer.State == UIGestureRecognizerState.Began) {
                    SetPressed (true);
                } else if (PressRecognizer.State == UIGestureRecognizerState.Ended) {
                    Action ();
                    SetPressed (false);
                }else if (PressRecognizer.State == UIGestureRecognizerState.Changed) {
                    SetPressed (PressRecognizer.IsInsideView);
                } else if (PressRecognizer.State == UIGestureRecognizerState.Failed) {
                    SetPressed (false);
                } else if (PressRecognizer.State == UIGestureRecognizerState.Cancelled) {
                    SetPressed (false);
                }
            }

            public void SetPressed (bool pressed)
            {
                if (pressed) {
                    TitleLabel.Alpha = 0.5f;
                    ImageView.Alpha = 0.5f;
                } else {
                    TitleLabel.Alpha = 1.0f;
                    ImageView.Alpha = 1.0f;
                }
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                ImageView.Frame = new CGRect ((Bounds.Width - ImageSize) / 2.0f, 0.0f, ImageSize, ImageSize);

                var titleHeight = TitleLabel.Font.RoundedLineHeight (1.0f) * 1.5f;
                var titleSize = TitleLabel.SizeThatFits (new CGSize (0.0f, 0.0f));
                TitleLabel.Frame = new CGRect ((Bounds.Width - titleSize.Width) / 2.0f, Bounds.Height - titleHeight, titleSize.Width, titleHeight);
            }
        }

        private class AccountView : UIView, ThemeAdopter
        {

            public readonly UIImageView AccountImageView;
            public readonly UILabel RecentUnreadIndicator;
            public readonly UILabel UnreadIndicator;
            public readonly UILabel NameLabel;
            public readonly UIView HighlightView;
            public bool UnreadOnLeft;
            public NcAccountMonitor.AccountInfo AccountInfo { get; private set; }

            public nfloat ImageSize = 40.0f;
            public nfloat RecentIndicatorSize = 14.0f;
            public nfloat IndicatorSize = 18.0f;
            public UIOffset IndicatorOffset = new UIOffset (-6.0f, -6.0f);
            public UIOffset RecentIndicatorOffset = new UIOffset (-3.0f, -3.0f);

            public WeakReference<SwitchAccountView> SwitchView;

            public AccountView (CGRect frame) : base (frame)
            {
                AccountImageView = new UIImageView (new CGRect(0.0f, 0.0f, ImageSize, ImageSize));
                AccountImageView.Layer.CornerRadius = AccountImageView.Frame.Width / 2.0f;
                AccountImageView.ClipsToBounds = true;

                HighlightView = new UIView (new CGRect(0.0f, 0.0f, ImageSize + 3.0f, ImageSize + 3.0f));
                HighlightView.Layer.BorderWidth = 2.0f;
                HighlightView.Layer.CornerRadius = AccountImageView.Frame.Width / 2.0f;
                HighlightView.Hidden = true;

                UnreadIndicator = new UILabel (new CGRect(0.0f, 0.0f, IndicatorSize, IndicatorSize));
                UnreadIndicator.ClipsToBounds = true;
                UnreadIndicator.Layer.BorderWidth = 1.0f;
                UnreadIndicator.TextAlignment = UITextAlignment.Center;
                UnreadIndicator.Layer.CornerRadius = IndicatorSize / 2.0f;

                RecentUnreadIndicator = new UILabel (new CGRect(0.0f, 0.0f, RecentIndicatorSize, RecentIndicatorSize));
                RecentUnreadIndicator.ClipsToBounds = true;
                RecentUnreadIndicator.Layer.BorderWidth = 1.0f;
                RecentUnreadIndicator.TextAlignment = UITextAlignment.Center;
                RecentUnreadIndicator.Layer.CornerRadius = RecentIndicatorSize / 2.0f;

                NameLabel = new UILabel ();
                NameLabel.TextAlignment = UITextAlignment.Center;
                NameLabel.LineBreakMode = UILineBreakMode.Clip;

                AddSubview (NameLabel);
                AddSubview (HighlightView);
                AddSubview (AccountImageView);
                AddSubview (UnreadIndicator);
                AddSubview (RecentUnreadIndicator);
            }

            public void Cleanup ()
            {
            }

            public void AdoptTheme (Theme theme)
            {
                NameLabel.Font = theme.DefaultFont.WithSize (14.0f);
                NameLabel.TextColor = theme.AccountSwitcherTextColor;
                HighlightView.BackgroundColor = theme.NavigationBarBackgroundColor;
                HighlightView.Layer.BorderColor = theme.NavigationBarTintColor.CGColor;
                UnreadIndicator.BackgroundColor = theme.NavigationBarBackgroundColor;
                UnreadIndicator.Layer.BorderColor = theme.NavigationBarTintColor.CGColor;
                UnreadIndicator.TextColor = theme.NavigationBarTintColor;
                UnreadIndicator.Font = theme.DefaultFont.WithSize (12.0f);
                RecentUnreadIndicator.BackgroundColor = theme.NavigationBarBackgroundColor;
                RecentUnreadIndicator.Layer.BorderColor = theme.NavigationBarTintColor.CGColor;
                RecentUnreadIndicator.Font = theme.DefaultFont.WithSize (9.0f);
                RecentUnreadIndicator.TextColor = theme.NavigationBarTintColor;
                SetNeedsLayout ();
            }

            public void SetHighlighted (bool highlighted)
            {
                if (highlighted) {
                    HighlightView.Hidden = false;
                } else {
                    HighlightView.Hidden = true;
                }
            }

            public void SetPressed (bool pressed)
            {
                if (pressed) {
                    NameLabel.Alpha = 0.5f;
                    AccountImageView.Alpha = 0.5f;
                } else {
                    NameLabel.Alpha = 1.0f;
                    AccountImageView.Alpha = 1.0f;
                }
            }

            public void SetAccountInfo (NcAccountMonitor.AccountInfo info)
            {
                AccountInfo = info;
                AccountImageView.Image = Util.ImageForAccount (info.Account);
                UnreadIndicator.Text = Pretty.LimitedBadgeCount (info.UnreadCount);
                UnreadIndicator.Hidden = info.UnreadCount == 0;
                RecentUnreadIndicator.Text = Pretty.LimitedBadgeCount (info.RecentUnreadCount);
                RecentUnreadIndicator.Hidden = info.RecentUnreadCount == 0;
                NameLabel.Text = AccountInfo.Account.DisplayName;
                SetNeedsLayout ();
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();

                AccountImageView.Frame = new CGRect ((Bounds.Width - ImageSize) / 2.0f, 0.0f, ImageSize, ImageSize);
                var hightlightSize = HighlightView.Layer.BorderWidth + 1.0f;
                HighlightView.Frame = AccountImageView.Frame.Inset (-hightlightSize, -hightlightSize);
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

                    cornerAnimation = CABasicAnimation.FromKeyPath ("cornerRadius");
                    cornerAnimation.Duration = animation.Duration;
                    cornerAnimation.TimingFunction = animation.TimingFunction;
                    cornerAnimation.From = new NSNumber (HighlightView.Layer.CornerRadius);
                    HighlightView.Layer.CornerRadius = HighlightView.Frame.Width / 2.0f;
                    cornerAnimation.To = new NSNumber (HighlightView.Layer.CornerRadius);
                    HighlightView.Layer.AddAnimation (cornerAnimation, "cornerRadius");
                } else {
                    AccountImageView.Layer.CornerRadius = AccountImageView.Frame.Width / 2.0f;
                    HighlightView.Layer.CornerRadius = HighlightView.Frame.Width / 2.0f;
                }

                var size = UnreadIndicator.SizeThatFits (new CGSize (IndicatorSize, IndicatorSize));
                var frame = UnreadIndicator.Frame;
                frame.Width = (nfloat)Math.Max (IndicatorSize, size.Width + IndicatorSize / 3.0f);
                frame.X = UnreadOnLeft ? AccountImageView.Frame.X + IndicatorOffset.Horizontal : AccountImageView.Frame.X + AccountImageView.Frame.Width - IndicatorOffset.Horizontal - frame.Width;
                frame.Y = IndicatorOffset.Vertical;
                frame.Height = IndicatorSize;
                UnreadIndicator.Frame = frame;

                size = RecentUnreadIndicator.SizeThatFits (new CGSize (RecentIndicatorSize, RecentIndicatorSize));
                frame = RecentUnreadIndicator.Frame;
                frame.Width = (nfloat)Math.Max (RecentIndicatorSize, size.Width + IndicatorSize / 3.0f);
                frame.X = UnreadOnLeft ? AccountImageView.Frame.X + RecentIndicatorOffset.Horizontal : AccountImageView.Frame.X + AccountImageView.Frame.Width - RecentIndicatorOffset.Horizontal - frame.Width;
                frame.Y = RecentIndicatorOffset.Vertical;
                frame.Height = RecentIndicatorSize;
                RecentUnreadIndicator.Frame = frame;

                var nameHeight = NameLabel.Font.RoundedLineHeight (1.0f) * 1.5f;
                var nameSize = NameLabel.SizeThatFits (new CGSize (0.0f, 0.0f));
                NameLabel.Frame = new CGRect ((Bounds.Width - nameSize.Width) / 2.0f, Bounds.Height - nameHeight, nameSize.Width, nameHeight);
            }

        }

    }
}

