// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using UIKit;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class InboxViewController : MessageListViewController
    {
        public InboxViewController (IntPtr handle) : base (handle)
        {
            SetEmailMessages (NcEmailManager.Inbox (NcApplication.Instance.Account.Id));
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            NavigationController.NavigationBar.Translucent = false;
        }

        protected override void CustomizeBackButton ()
        {
            BackShouldSwitchToFolders ();
        }
    }
}
