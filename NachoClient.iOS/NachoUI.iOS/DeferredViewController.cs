// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class DeferredViewController : MessageListViewController
    {
        public DeferredViewController (IntPtr handle) : base (handle)
        {
            SetEmailMessages (new NachoDeferredEmailMessages ());
        }

        protected override void CustomizeBackButton ()
        {
            BackShouldSwitchToFolders ();
        }
    }
}
