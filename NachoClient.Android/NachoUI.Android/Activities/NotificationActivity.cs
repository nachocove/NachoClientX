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
    [Activity (Label = "NotificationActivity")]
    public class NotificationActivity : NcActivity
    {
        private const string EXTRA_MESSAGE = "com.nachocove.nachomail.EXTRA_MESSAGE";

        public static Intent ShowMessageIntent (Context context, McEmailMessage message)
        {
            var intent = new Intent (context, typeof(NotificationActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_MESSAGE, message.Id);
            return intent;
        }

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            this.SetContentView (Resource.Layout.NotificationActivity);

            var messageId = Intent.GetIntExtra (EXTRA_MESSAGE, 0);
            var message = McEmailMessage.QueryById<McEmailMessage> (messageId);

            // In case notification started the app
            MainApplication.OneTimeStartup ("NotificationActivity");

            if (null == message) {
                var inboxIntent = NcTabBarActivity.InboxIntent (this);
                StartActivity (inboxIntent);
                Finish ();
                return;
            }

            NcTask.Run (() => {
                BackEnd.Instance.SendEmailBodyFetchHint(message.AccountId, message.Id);
            }, "NotificationActivity.SendEmailBodyFetchHint");

            // Create show message intent
            var thread = new McEmailMessageThread ();
            thread.FirstMessageId = message.Id;
            thread.MessageCount = 1;
            var intent = MessageViewActivity.ShowMessageIntent (this, thread, message);
            intent.SetFlags (ActivityFlags.NoAnimation);

            if (NcTabBarActivity.TabBarWasCreated) {
                StartActivity (intent);
            } else {
                StartActivities (new Intent[] { NcTabBarActivity.InboxIntent (this), intent });
            }
            Finish ();
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

    }
}
