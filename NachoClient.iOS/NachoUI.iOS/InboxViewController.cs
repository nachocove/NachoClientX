//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class InboxViewController : MessageListViewController, IAccountSwitching
    {
        
        McAccount Account;

        public InboxViewController () : base ()
        {
            Account = NcApplication.Instance.Account;
            SetEmailMessages (NcEmailManager.Inbox (Account.Id));
        }

        public override void ViewDidLoad ()
        {
            if (NcApplication.Instance.Account.Id != Account.Id) {
                SwitchToAccount (NcApplication.Instance.Account);
            }
            base.ViewDidLoad ();
        }

        public override void ViewWillAppear (bool animated)
        {
            if (NcApplication.Instance.Account.Id != Account.Id) {
                SwitchToAccount (NcApplication.Instance.Account);
            }
            base.ViewWillAppear (animated);
        }

        public void SwitchToAccount (McAccount account)
        {
            Account = account;
            CancelSyncing ();
            if (TableView.Editing) {
                CancelEditingTable (animated: false);
            }
            if (SwipingIndexPath != null) {
                EndSwiping ();
            }
            SetEmailMessages (NcEmailManager.Inbox (account.Id));
            UpdateFilterBar ();
            TableView.ReloadData ();  // to clear the table
            HasLoadedOnce = false;
            SetNeedsReload ();
        }
    }
}

