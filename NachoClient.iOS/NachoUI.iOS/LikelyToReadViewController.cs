// (C) Copyright 2015 Nacho Cove, Inc.
using System;

using Foundation;
using UIKit;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class LikelyToReadViewController : MessageListViewController
    {
        public LikelyToReadViewController () : base ()
        {
            SetEmailMessages (GetNachoEmailMessages (NcApplication.Instance.Account.Id));
        }

        protected override INachoEmailMessages GetNachoEmailMessages (int accountId)
        {
            return NcEmailManager.LikelyToReadInbox (accountId);
        }

        public override bool HasAccountSwitcher ()
        {
            return false;
        }
    }
}

