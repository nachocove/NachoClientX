using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.OS;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.Design.Widget;

using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "MessageComposeActivity")]            
    public class MessageComposeActivity : NcActivityWithData<McEmailMessage>
    {

        public static readonly string EXTRA_ACTION = "com.nachocove.nachomail.action";
        public static readonly string EXTRA_RELATED_MESSAGE_ID = "com.nachocove.nachomail.relatedMessageId";
        public static readonly string EXTRA_RELATED_CALENDAR_ID = "com.nachocove.nachomail.relatedCalendarId";
        public static readonly string EXTRA_MESSAGE = "com.nachocove.nachomail.message";
        public static readonly string EXTRA_INITIAL_TEXT = "com.nachocove.nachomail.initialText";
        public static readonly string EXTRA_INITIAL_RECIPIENT = "com.nachocove.nachomail.initialRecipient";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.MessageComposeActivity);

            var composeFragment = new ComposeFragment ();
            if (Intent.HasExtra (EXTRA_ACTION)) {
                composeFragment.Composer.Kind = (NachoCore.Utils.EmailHelper.Action)Intent.GetIntExtra (EXTRA_ACTION, 0);
            }
            if (Intent.HasExtra (EXTRA_RELATED_MESSAGE_ID)) {
                var relatedThread = new McEmailMessageThread ();
                relatedThread.FirstMessageId = Intent.GetIntExtra (EXTRA_RELATED_MESSAGE_ID, 0);
                relatedThread.MessageCount = 1;
                composeFragment.Composer.RelatedThread = relatedThread;
            }
            if (Intent.HasExtra (EXTRA_RELATED_CALENDAR_ID)) {
                var relatedCalendarItem = McCalendar.QueryById<McCalendar> (Intent.GetIntExtra (EXTRA_RELATED_CALENDAR_ID, 0));
                composeFragment.Composer.RelatedCalendarItem = relatedCalendarItem;
            }
            if (Intent.HasExtra(EXTRA_MESSAGE)) {
                var message = RetainedData;
                if (null == message) {
                    message = IntentHelper.RetrieveValue<McEmailMessage> (Intent.GetStringExtra (EXTRA_MESSAGE));
                    RetainedData = message;
                }
                composeFragment.Composer.Message = message;
            }
            if (Intent.HasExtra (EXTRA_INITIAL_TEXT)) {
                var text = Intent.GetStringExtra (EXTRA_INITIAL_TEXT);
                composeFragment.Composer.InitialText = text;
            }
            if (Intent.HasExtra (EXTRA_INITIAL_RECIPIENT)) {
                var to = Intent.GetStringExtra (EXTRA_INITIAL_RECIPIENT);
                composeFragment.Composer.InitialRecipient = to;
            }

            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, composeFragment).AddToBackStack("Now").Commit ();
           
        }

        public static Intent NewMessageIntent (Context context, string recipient = null)
        {
            var intent = new Intent (context, typeof(MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            if (!String.IsNullOrEmpty (recipient)) {
                intent.PutExtra (EXTRA_INITIAL_RECIPIENT, recipient);
            }
            return intent;
        }

        public static Intent RespondIntent (Context context, EmailHelper.Action action, int relatedMessageId)
        {
            var intent = new Intent (context, typeof(MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_ACTION, (int)action);
            intent.PutExtra (EXTRA_RELATED_MESSAGE_ID, relatedMessageId);
            return intent;
        }

        public static Intent InitialTextIntent (Context context, McEmailMessage message, string text)
        {
            var intent = new Intent (context, typeof(MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_MESSAGE, IntentHelper.StoreValue (message));
            intent.PutExtra (EXTRA_INITIAL_TEXT, text);
            return intent;
        }

        public static Intent ForwardCalendarIntent (Context context, int calendarId, McEmailMessage message)
        {
            var intent = new Intent (context, typeof(MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_ACTION, (int)EmailHelper.Action.Forward);
            intent.PutExtra (EXTRA_RELATED_CALENDAR_ID, calendarId);
            intent.PutExtra (EXTRA_MESSAGE, IntentHelper.StoreValue (message));
            return intent;
        }

        public override void OnBackPressed ()
        {
            var alert = new Android.App.AlertDialog.Builder (this).SetTitle ("Would you like to save this message?").SetMessage ("You can access saved messages from your Drafts folder.");
            alert.SetNegativeButton ("Discard Draft", Discard);
            alert.SetPositiveButton ("Save Draft", Save);
            alert.Show ();
        }

        public void Discard (object sender, EventArgs args)
        {
            var fragment = FragmentManager.FindFragmentById<ComposeFragment> (Resource.Id.content);
            fragment.Discard ();
            Finish ();
        }

        public void Save (object sender, EventArgs args)
        {
            var fragment = FragmentManager.FindFragmentById<ComposeFragment> (Resource.Id.content);
            fragment.Save (() => {
                Finish ();
            });
        }
    }
}

