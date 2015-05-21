//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using Foundation;
using UIKit;
using NachoCore.Model;
using NachoCore;

namespace NachoClient.iOS
{
    public class SwitchAccountView : UIView, INachoAccountsTableDelegate
    {
        public SwitchAccountView ()
        {
        }

        UIView coverView;
        SwitchAccountView switchAccountView;
        SwitchAccountButton switchAccountButton;

        UITableView accountsTableView;
        AccountsTableViewSource accountsTableViewSource;

        public delegate void SwitchAccountCallback (McAccount account);

        SwitchAccountCallback SwitchToAccount;

        public void Activate (SwitchAccountCallback SwitchToAccount)
        {
            this.SwitchToAccount = SwitchToAccount;

            var topView = Util.FindOutermostView (this);
            coverView = new CoverView (topView, new CGRect (0, 0, 0, 0));
            topView.AddSubview (coverView);

            switchAccountButton = new SwitchAccountButton (SwitchAccountButtonPressed);
            ViewFramer.Create (switchAccountButton).Y (20).CenterX (0, coverView.Frame.Width);
            switchAccountButton.SetImage ("gen-avatar-backarrow");
            coverView.AddSubview (switchAccountButton);

            accountsTableView = new UITableView (coverView.Frame);
            ViewFramer.Create (accountsTableView).Y (switchAccountButton.Frame.Bottom);
            accountsTableView.BackgroundColor = A.Color_NachoBackgroundGray;

            accountsTableViewSource = new AccountsTableViewSource ();
            accountsTableViewSource.Setup (this, showAccessory: false);
            accountsTableView.Source = accountsTableViewSource;

            coverView.AddSubview (accountsTableView);
            coverView.BringSubviewToFront (switchAccountButton);

            switchAccountButton.Alpha = 1;
            var h = accountsTableView.Frame.Height;
            ViewFramer.Create (accountsTableView).Height (0);

            UIView.Animate (0.4, 0, UIViewAnimationOptions.CurveLinear,
                () => {
                    ViewFramer.Create (accountsTableView).Height (h);
                    coverView.BackgroundColor = UIColor.FromWhiteAlpha (0.3f, 0.3f); // DEBUG
                },
                () => {
                }
            );
        }

        void SwitchAccountButtonPressed ()
        {
            Deactivate (null);
        }

        public void AccountSelected (McAccount account)
        {
            NcApplication.Instance.Account = account;
            Deactivate (account);
        }

        private bool CoverShouldRecognizeSimultaneously (UIGestureRecognizer a, UIGestureRecognizer b)
        {
            return true;
        }

        public void Deactivate (McAccount account)
        {
            UIView.Animate (0.3, 0, UIViewAnimationOptions.CurveLinear,
                () => {
                    ViewFramer.Create (accountsTableView).Height (0);
                    coverView.BackgroundColor = UIColor.Clear;
                },
                () => {
                    switchAccountButton.Alpha = 0;
                    coverView.RemoveFromSuperview ();
                    coverView = null;
                    if (null != account) {
                        SwitchToAccount (account);
                    }
                });
        }
    }
}
   

