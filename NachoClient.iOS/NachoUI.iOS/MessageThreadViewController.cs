// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using UIKit;

namespace NachoClient.iOS
{
    public partial class MessageThreadViewController : MessageListViewController
	{
		public MessageThreadViewController (IntPtr handle) : base (handle)
		{
		}

        public override void MaybeDismissView()
        {
            // Yes, thread views disappear when empty
            NavigationController.PopViewController (true);
        }
	}
}
