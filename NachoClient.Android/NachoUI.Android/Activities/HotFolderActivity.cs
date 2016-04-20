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
    [Activity (Label = "HotFolderActivity")]            
    public class HotFolderActivity : MessageFolderActivity
    {
        public static Intent ShowHotFolderIntent (Context context, McFolder folder)
        {
            var intent = new Intent (context, typeof(HotFolderActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_FOLDER, IntentHelper.StoreValue (folder));
            return intent;
        }

        protected override NachoEmailMessages GetMessages (out List<int> adds, out List<int> deletes)
        {
            var messages = NcEmailManager.PriorityInbox (NcApplication.Instance.Account.Id);
            messages.Refresh (out adds, out deletes);
            return messages;
        }
    }
}
