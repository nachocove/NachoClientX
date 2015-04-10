// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using UIKit;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class DeferredViewController : MessageListViewController
    {
        public DeferredViewController (IntPtr handle) : base (handle)
        {
            SetEmailMessages (new NachoDeferredEmailMessages ());
        }

        protected override void SetRowHeight ()
        {
            TableView.RowHeight = MessageTableViewConstants.DATED_ROW_HEIGHT;
            searchDisplayController.SearchResultsTableView.RowHeight = MessageTableViewConstants.DATED_ROW_HEIGHT;
        }

//        protected override void CustomizeBackButton ()
//        {
//            BackShouldSwitchToFolders ();
//        }
    }
}
