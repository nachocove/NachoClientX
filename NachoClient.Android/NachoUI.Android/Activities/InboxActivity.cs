using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "InboxActivity")]            
    public class InboxActivity : NcMessageListActivity
    {
        protected override NachoEmailMessages GetMessages (out List<int> adds, out List<int> deletes)
        {
            var messages = NcEmailSingleton.InboxSingleton (NcApplication.Instance.Account.Id);
            NcEmailSingleton.RefreshIfNeeded (messages, out adds, out deletes);
            return messages;
        }

        public override void SetActiveImage (View view)
        {
            // Highlight the tab bar icon of this activity
            var tabImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.inbox_image);
            tabImage.SetImageResource (Resource.Drawable.nav_mail_active);
        }
    }
}
