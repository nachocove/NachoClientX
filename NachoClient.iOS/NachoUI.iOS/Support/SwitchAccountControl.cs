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
        UIImageView ChangedAccountViewRight;
        UIImageView ChangedAccountViewLeft;
        nfloat BorderWidth = 2.0f;
        Action<McAccount> AccountSwitched;

        enum ControlState {
            Collapsed,
            Expanded,
            Animating
        }

        ControlState State;
        bool NeedsUpdate;

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

        void HandleAccountSwitched (object sender, EventArgs e)
        {
            if (AccountSwitched != null) {
                AccountSwitched (NcApplication.Instance.Account);
            }
            Update ();
        }

        void HandleAccountSetChanged (object sender, EventArgs e)
        {
            Update ();
        }

        void Update ()
        {
            NeedsUpdate = false;
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
                    ChangedAccountViewRight = new UIImageView (SelectedAccountView.Frame.Inset (5.0f, 5.0f));
                    ChangedAccountViewRight.Layer.CornerRadius = ChangedAccountViewRight.Frame.Width / 2.0f;
                    ChangedAccountViewRight.Alpha = 0.5f;
                    ChangedAccountViewRight.Transform = CGAffineTransform.MakeTranslation (ChangedAccountViewRight.Frame.Width, 0.0f);
                    InsertSubviewBelow (ChangedAccountViewRight, BackgroundView);
                }
                ChangedAccountViewRight.Image = Util.ImageForAccount (accountsWithChanges [0].Account);
            } else {
                if (ChangedAccountViewRight != null) {
                    ChangedAccountViewRight.RemoveFromSuperview ();
                    ChangedAccountViewRight = null;
                }
            }

            if (accountsWithChanges.Count > 1) {
                if (ChangedAccountViewLeft == null) {
                    ChangedAccountViewLeft = new UIImageView (SelectedAccountView.Frame.Inset (5.0f, 5.0f));
                    ChangedAccountViewLeft.Layer.CornerRadius = ChangedAccountViewLeft.Frame.Width / 2.0f;
                    ChangedAccountViewLeft.Alpha = 0.5f;
                    ChangedAccountViewLeft.Transform = CGAffineTransform.MakeTranslation (-ChangedAccountViewLeft.Frame.Width, 0.0f);
                    InsertSubviewBelow (ChangedAccountViewLeft, BackgroundView);
                }
                ChangedAccountViewLeft.Image = Util.ImageForAccount (accountsWithChanges [1].Account);
            } else {
                if (ChangedAccountViewLeft != null) {
                    ChangedAccountViewLeft.RemoveFromSuperview ();
                    ChangedAccountViewLeft = null;
                }
            }


            if (State == ControlState.Collapsed) {
            } else if (State == ControlState.Expanded) {
            } else {
            }
        }

        bool IsCustomHidden;

        public void SetHidden (bool hidden, IUIViewControllerTransitionCoordinator animationCoordinator = null)
        {
            if (hidden != IsCustomHidden) {
                IsCustomHidden = hidden;
                if (animationCoordinator != null) {
                    Hidden = false;
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
    }
}

