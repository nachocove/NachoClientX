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
    [Activity (Label = "NowListActivity")]            
    public class NowListActivity : NcMessageListActivity
    {
        protected override INachoEmailMessages GetMessages (out List<int> adds, out List<int> deletes)
        {
            var messages = NcEmailSingleton.PrioritySingleton (NcApplication.Instance.Account.Id);
            messages.Refresh (out adds, out deletes);
            return messages;
        }

        public override bool ShowHotEvent ()
        {
            return true;
        }

        public override int ShowListStyle()
        {
            if (LoginHelpers.ShowHotCards ()) {
                return MessageListAdapter.CARDVIEW_STYLE;
            } else {
                return MessageListAdapter.LISTVIEW_STYLE;
            }
        }

        public override void SetActiveImage (View view)
        {
            // Highlight the tab bar icon of this activity
            var tabImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.hot_image);
            tabImage.SetImageResource (Resource.Drawable.nav_nachonow_active);
        }
    }
}
