//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using UIKit;
using CoreGraphics;
using Foundation;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    public class SwitchAccountControl : UIView
    {

        UIView BackgroundView;
        UIImageView SelectedAccountView;
        ChangedAccountView ChangedAccountViewRight;
        ChangedAccountView ChangedAccountViewLeft;
        nfloat BorderWidth = 2.0f;
        public Action<McAccount> AccountSwitched;

        enum ControlState {
            Collapsed,
            Expanded,
            Animating
        }

        ControlState State;

        public SwitchAccountControl () : base (new CGRect(0.0f, 0.0f, 44.0f, 44.0f))
        {
            State = ControlState.Collapsed;

            BackgroundView = new UIView (Bounds);
            BackgroundView.Layer.CornerRadius = Bounds.Width / 2.0f;
            BackgroundView.BackgroundColor = A.Color_NachoGreen;

            SelectedAccountView = new UIImageView (Bounds.Inset (BorderWidth, BorderWidth));
            SelectedAccountView.Layer.CornerRadius = SelectedAccountView.Frame.Width / 2.0f;
            SelectedAccountView.ClipsToBounds = true;

            AddSubview (BackgroundView);
            AddSubview (SelectedAccountView);

            Update ();

            NcAccountMonitor.Instance.AccountSetChanged += HandleAccountSetChanged;
            NcAccountMonitor.Instance.AccountSwitched += HandleAccountSwitched;
        }

        void SwitchToChangedAccountView (ChangedAccountView changedView)
        {
            var account = changedView.AccountInfo.Account;
            LoginHelpers.SetSwitchAwayTime (NcApplication.Instance.Account.Id);
            LoginHelpers.SetMostRecentAccount (account.Id);
            UIView.Transition (UIApplication.SharedApplication.Delegate.GetWindow (), 0.5f, UIViewAnimationOptions.TransitionFlipFromRight, () => {
                NcAccountMonitor.Instance.ChangeAccount (account);
                if (AccountSwitched != null) {
                    AccountSwitched (account);
                }
            }, null);
        }

        void HandleAccountSwitched (object sender, EventArgs e)
        {
            Update ();
        }

        void HandleAccountSetChanged (object sender, EventArgs e)
        {
            Update ();
        }

        void Update ()
        {
            var selectedAccount = NcApplication.Instance.Account;
            SelectedAccountView.Image = Util.ImageForAccount (selectedAccount);
            var accountsWithChanges = new List<NcAccountMonitor.AccountInfo> ();

            if (selectedAccount.AccountType != McAccount.AccountTypeEnum.Unified) {
                foreach (var accountInfo in NcAccountMonitor.Instance.Accounts) {
                    if (accountInfo.Account.Id != selectedAccount.Id) {
                        if (accountInfo.RecentUnreadCount > 0) {
                            accountsWithChanges.Add (accountInfo);
                        }
                    }
                }
                accountsWithChanges.Sort ((NcAccountMonitor.AccountInfo x, NcAccountMonitor.AccountInfo y) => {
                    return x.RecentUnreadCount - y.RecentUnreadCount;
                });
            }

            if (accountsWithChanges.Count > 0) {
                if (ChangedAccountViewRight == null) {
                    ChangedAccountViewRight = new ChangedAccountView (SelectedAccountView.Frame.Inset (5.0f, 5.0f));
                    ChangedAccountViewRight.Transform = CGAffineTransform.MakeTranslation (ChangedAccountViewRight.Frame.Width, 0.0f);
                    ChangedAccountViewRight.SwitchControl = new WeakReference<SwitchAccountControl> (this);
                    InsertSubviewBelow (ChangedAccountViewRight, BackgroundView);
                }
                ChangedAccountViewRight.SetAccountInfo (accountsWithChanges [0]);
            } else {
                if (ChangedAccountViewRight != null) {
                    ChangedAccountViewRight.RemoveFromSuperview ();
                    ChangedAccountViewRight.Cleanup ();
                    ChangedAccountViewRight = null;
                }
            }

            if (accountsWithChanges.Count > 1) {
                if (ChangedAccountViewLeft == null) {
                    ChangedAccountViewLeft = new ChangedAccountView (SelectedAccountView.Frame.Inset (5.0f, 5.0f));
                    ChangedAccountViewLeft.UnreadOnLeft = true;
                    ChangedAccountViewLeft.Transform = CGAffineTransform.MakeTranslation (-ChangedAccountViewLeft.Frame.Width, 0.0f);
                    ChangedAccountViewLeft.SwitchControl = new WeakReference<SwitchAccountControl> (this);
                    InsertSubviewBelow (ChangedAccountViewLeft, BackgroundView);
                }
                ChangedAccountViewLeft.SetAccountInfo (accountsWithChanges [1]);
            } else {
                if (ChangedAccountViewLeft != null) {
                    ChangedAccountViewLeft.RemoveFromSuperview ();
                    ChangedAccountViewLeft.Cleanup ();
                    ChangedAccountViewLeft = null;
                }
            }


            if (State == ControlState.Collapsed) {
            } else if (State == ControlState.Expanded) {
            } else {
            }
        }

        public override UIView HitTest (CGPoint point, UIEvent uievent)
        {
            var view = base.HitTest (point, uievent);
            if (view == null && ChangedAccountViewRight != null && ChangedAccountViewRight.PointInside(ConvertPointToView(point, ChangedAccountViewRight), uievent)) {
                return ChangedAccountViewRight;
            }
            if (view == null && ChangedAccountViewLeft != null && ChangedAccountViewLeft.PointInside(ConvertPointToView(point, ChangedAccountViewLeft), uievent)) {
                return ChangedAccountViewLeft;
            }
            return view;
        }

        bool IsCustomHidden;

        public void SetHidden (bool hidden, IUIViewControllerTransitionCoordinator animationCoordinator = null)
        {
            if (hidden != IsCustomHidden) {
                IsCustomHidden = hidden;
                if (animationCoordinator != null) {
                    Hidden = false;
                    if (IsCustomHidden) {
                        Unhide ();
                    } else {
                        Hide ();
                    }
                    animationCoordinator.AnimateAlongsideTransition ((IUIViewControllerTransitionCoordinatorContext context) => {
                        if (IsCustomHidden) {
                            Hide ();
                        } else {
                            Unhide ();
                        }
                    }, (IUIViewControllerTransitionCoordinatorContext context) => {
                        Hidden = IsCustomHidden;
                    });
                } else {
                    Hidden = IsCustomHidden;
                    if (IsCustomHidden) {
                        Hide ();
                    } else {
                        Unhide ();
                    }
                }
            }
        }

        void Hide ()
        {
            HideByShrinking ();
        }

        void Unhide ()
        {
            UnhideByShrinking ();
        }

        void HideByFading ()
        {
            Alpha = 0.0f;
        }

        void UnhideByFading ()
        {
            Alpha = 1.0f;
        }

        void HideByPushing ()
        {
            Alpha = 0.0f;
            Transform = CGAffineTransform.MakeTranslation (-Frame.X * 0.75f, 0.0f);
        }

        void UnhideByPushing ()
        {
            Alpha = 1.0f;
            Transform = CGAffineTransform.MakeIdentity ();
        }

        void HideByShrinking ()
        {
            Alpha = 0.0f;
            Transform = CGAffineTransform.MakeScale (0.01f, 0.01f);
            if (ChangedAccountViewRight != null) {
                ChangedAccountViewRight.Transform = CGAffineTransform.MakeIdentity ();
            }
            if (ChangedAccountViewLeft != null) {
                ChangedAccountViewLeft.Transform = CGAffineTransform.MakeIdentity ();
            }
        }

        void UnhideByShrinking ()
        {
            Alpha = 1.0f;
            Transform = CGAffineTransform.MakeIdentity ();
            if (ChangedAccountViewRight != null) {
                ChangedAccountViewRight.Transform = CGAffineTransform.MakeTranslation (ChangedAccountViewRight.Frame.Width, 0.0f);
            }
            if (ChangedAccountViewLeft != null) {
                ChangedAccountViewLeft.Transform = CGAffineTransform.MakeTranslation (-ChangedAccountViewLeft.Frame.Width, 0.0f);
            }
        }

        private class ChangedAccountView : UIView
        {

            UIImageView AccountImageView;
            UILabel UnreadIndicator;
            public bool UnreadOnLeft;
            public NcAccountMonitor.AccountInfo AccountInfo { get; private set; }

            nfloat IndicatorSize = 14.0f;
            UIOffset IndicatorOffset = new UIOffset (-3.0f, -3.0f);

            PressGestureRecognizer PressRecognizer;
            public WeakReference<SwitchAccountControl> SwitchControl;

            public ChangedAccountView (CGRect frame) : base (frame)
            {
                AccountImageView = new UIImageView (Bounds);
                AccountImageView.Layer.CornerRadius = AccountImageView.Frame.Width / 2.0f;
                AccountImageView.ClipsToBounds = true;
                AccountImageView.Alpha = 0.5f;

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
                    AccountImageView.Alpha = 1.0f;
                } else if (PressRecognizer.State == UIGestureRecognizerState.Ended) {
                    SwitchAccountControl switcher;
                    if (SwitchControl.TryGetTarget (out switcher)) {
                        switcher.SwitchToChangedAccountView (this);
                    }
                    AccountImageView.Alpha = 0.5f;
                }else if (PressRecognizer.State == UIGestureRecognizerState.Changed) {
                    AccountImageView.Alpha = PressRecognizer.IsInsideView ? 1.0f : 0.5f;
                } else if (PressRecognizer.State == UIGestureRecognizerState.Failed) {
                    AccountImageView.Alpha = 0.5f;
                } else if (PressRecognizer.State == UIGestureRecognizerState.Cancelled) {
                    AccountImageView.Alpha = 0.5f;
                }
            }

            public void SetAccountInfo (NcAccountMonitor.AccountInfo info)
            {
                AccountInfo = info;
                AccountImageView.Image = Util.ImageForAccount (info.Account);
                UnreadIndicator.Text = info.RecentUnreadCount.ToString ();
                SetNeedsLayout ();
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
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

