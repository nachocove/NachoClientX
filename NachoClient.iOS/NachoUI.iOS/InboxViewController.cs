//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class InboxViewController : MessageListViewController
    {

        SwitchAccountButton SwitchAccountButton;

        public InboxViewController () : base ()
        {
            SetEmailMessages (NcEmailManager.Inbox (NcApplication.Instance.Account.Id));
        }

        public override void ViewDidLoad ()
        {
            SwitchAccountButton = new SwitchAccountButton (ShowAccountSwitcher);
            NavigationItem.TitleView = SwitchAccountButton;
            SwitchAccountButton.SetAccountImage (NcApplication.Instance.Account);
            base.ViewDidLoad ();
        }

        void ShowAccountSwitcher ()
        {
            SwitchAccountViewController.ShowDropdown (this, SwitchToAccount);
        }

        void SwitchToAccount (McAccount account)
        {
            SwitchAccountButton.SetAccountImage (account);
            SetEmailMessages (NcEmailManager.Inbox (account.Id));
            TableView.ReloadData ();
            HasLoadedOnce = false;
            // Relying on ViewWillAppear to call Reload
        }
    }
}

