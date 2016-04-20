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
        protected const string EXTRA_FOLDER = "com.nachocove.nachomail.EXTRA_FOLDER";

        private const string DATA_FRAGMENT_TAG = "FolderDataFragment";

        McFolder folder = null;

        private McFolder Folder {
            get {
                if (null == folder) {
                    var data = FragmentManager.FindFragmentByTag<DataFragment> (DATA_FRAGMENT_TAG);
                    if (null == data) {
                        data = new DataFragment ();
                        data.Folder = IntentHelper.RetrieveValue<McFolder> (Intent.GetStringExtra (EXTRA_FOLDER));
                        FragmentManager.BeginTransaction ().Add (data, DATA_FRAGMENT_TAG).Commit ();
                    }
                    folder = data.Folder;
                }
                return folder;
            }
        }

        public static Intent ShowFolderIntent (Context context, McFolder folder)
        {
            var intent = new Intent (context, typeof(MessageFolderActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_FOLDER, IntentHelper.StoreValue (folder));
            return intent;
        }

        protected override NachoEmailMessages GetMessages (out List<int> adds, out List<int> deletes)
        {
            NachoEmailMessages messages;
            if (Folder.IsClientOwnedDraftsFolder () || Folder.IsClientOwnedOutboxFolder ()) {
                messages = new NachoDraftMessages (Folder);
            } else {
                messages = new NachoFolderMessages (Folder);
            }
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
            title.Text = Folder.DisplayName;
            title.Visibility = ViewStates.Visible;
        }

        private class DataFragment : Fragment
        {
            public McFolder Folder { get; set; }

            public override void OnCreate (Bundle savedInstanceState)
            {
                base.OnCreate (savedInstanceState);
                this.RetainInstance = true;
            }
        }

        protected override void OnResume ()
        {
            base.OnResume ();
            var moreImage = Window.FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);
            if (LoginHelpers.ShouldAlertUser ()) {
                moreImage.SetImageResource (Resource.Drawable.gen_avatar_alert);
            } else {
                moreImage.SetImageResource (Resource.Drawable.nav_more);
            }
        }

        public override void OnBackPressed ()
        {
            Finish ();
        }
    }
}
