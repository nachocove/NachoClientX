// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using UIKit;

namespace NachoClient.iOS
{
    public partial class MessageThreadViewController : MessageListViewController
	{

        public MessageThreadViewController () : base ()
        {
        }

		public MessageThreadViewController (IntPtr handle) : base (handle)
		{
		}

        // MaybeDismiss might be called a few
        // times if the status inds fire quickly.
        // It just pops the stack. PopViewController
        // just keeps popping, not just this view but for
        // others on top.
        bool alreadyDismissed;

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            alreadyDismissed = false;
        }

        public override bool MaybeDismissView()
        {
            // Thread views disappear when empty.
            // Message list views show "no more messages".
            if (!alreadyDismissed) {
                alreadyDismissed = true;
                NavigationController.PopViewController (true);
            }
            return true;
        }
	}
}
