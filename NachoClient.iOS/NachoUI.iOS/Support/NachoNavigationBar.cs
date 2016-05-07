//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class NachoNavigationBar : UINavigationBar
    {

        public SwitchAccountControl AccountSwitcher { get; private set; }
        public WeakReference<UINavigationController> NavigationController;

        public NachoNavigationBar (IntPtr handle) : base (handle)
        {
            Initialize ();
        }

        void Initialize ()
        {
            AccountSwitcher = new SwitchAccountControl ();
            AccountSwitcher.AccountSwitched = SwitchToAccount;
            AddSubview (AccountSwitcher);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            AccountSwitcher.Center = new CGPoint (Bounds.Width / 2.0f, Bounds.Height / 2.0f + 4.0f);
        }

        public void Cleanup ()
        {
            AccountSwitcher.AccountSwitched = null;
        }

        void SwitchToAccount (McAccount account)
        {
            UINavigationController navController;
            if (NavigationController != null && NavigationController.TryGetTarget (out navController)) {
                var switchingViewController = navController.TopViewController as IAccountSwitching;
                if (switchingViewController != null) {
                    switchingViewController.SwitchToAccount (account);
                }
            }
        }
    }
}

