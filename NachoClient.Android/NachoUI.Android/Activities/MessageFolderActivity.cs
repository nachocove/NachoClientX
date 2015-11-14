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
    [Activity (Label = "MessageFolderActivity")]            
    public class MessageFolderActivity : NcMessageListActivity
    {
        private const string EXTRA_FOLDER = "com.nachocove.nachomail.EXTRA_FOLDER";

        McFolder folder;

        public static Intent ShowFolderIntent (Context context, McFolder folder)
        {
            var intent = new Intent (context, typeof(MessageFolderActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_FOLDER, IntentHelper.StoreValue (folder));
            return intent;
        }

        protected override INachoEmailMessages GetMessages (out List<int> adds, out List<int> deletes)
        {
            folder = IntentHelper.RetrieveValue<McFolder> (Intent.GetStringExtra (EXTRA_FOLDER));
            var messages = new NachoEmailMessages (folder);
            messages.Refresh (out adds, out deletes);
            return messages;
        }

        public override void SetActiveImage (View view)
        {
            // Highlight the tab bar icon of this activity
            var moreImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);
            moreImage.SetImageResource (Resource.Drawable.nav_more_active);

            view.FindViewById<View> (Resource.Id.account).Visibility = ViewStates.Gone;

            var title = view.FindViewById<TextView> (Resource.Id.title);
            title.Text = folder.DisplayName;
            title.Visibility = ViewStates.Visible;
        }
    }
}
