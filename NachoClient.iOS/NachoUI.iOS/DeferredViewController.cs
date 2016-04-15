// (C) Copyright 2013-2015 Nacho Cove, Inc.
using System;

using Foundation;
using UIKit;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class DeferredViewController : MessageListViewController
    {
        public DeferredViewController () : base ()
        {
            SetEmailMessages (GetNachoEmailMessages (NcApplication.Instance.Account.Id));
        }

        protected override INachoEmailMessages GetNachoEmailMessages (int accountId)
        {
            return new NachoDeferredEmailMessages (accountId);
        }

        protected override void SetRowHeight ()
        {
            TableView.RowHeight = MessageTableViewConstants.DATED_ROW_HEIGHT;
            searchDisplayController.SearchResultsTableView.RowHeight = MessageTableViewConstants.DATED_ROW_HEIGHT;
        }

        public override bool HasAccountSwitcher ()
        {
            return true;
        }

//        protected override void CustomizeBackButton ()
//        {
//            BackShouldSwitchToFolders ();
//        }
    }
}
