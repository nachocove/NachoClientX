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

namespace NachoClient.AndroidClient
{
    [Activity (Label = "MessageComposeActivity")]            
    public class MessageComposeActivity : AppCompatActivity
    {

        public static readonly string EXTRA_ACTION = "com.nachocove.nachomail.action";
        public static readonly string EXTRA_RELATED_MESSAGE_ID = "com.nachocove.nachomail.relatedMessageId";
        public static readonly string EXTRA_RELATED_CALENDAR_ID = "com.nachocove.nachomail.relatedCalendarId";
        public static readonly string EXTRA_MESSAGE_ID = "com.nachocove.nachomail.messageId";
        public static readonly string EXTRA_INITIAL_TEXT = "com.nachocove.nachomail.initialText";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.MessageComposeActivity);

            var composeFragment = new ComposeFragment ();
            if (Intent != null && Intent.Extras != null) {
                if (Intent.Extras.ContainsKey (EXTRA_ACTION)) {
                    composeFragment.Composer.Kind = (NachoCore.Utils.EmailHelper.Action)Intent.Extras.GetInt (EXTRA_ACTION);
                }
                if (Intent.Extras.ContainsKey (EXTRA_RELATED_MESSAGE_ID)) {
                    var relatedThread = new McEmailMessageThread ();
                    relatedThread.FirstMessageId = Intent.Extras.GetInt (EXTRA_RELATED_MESSAGE_ID);
                    relatedThread.MessageCount = 1;
                    composeFragment.Composer.RelatedThread = relatedThread;
                }
                if (Intent.Extras.ContainsKey (EXTRA_RELATED_CALENDAR_ID)) {
                    var relatedCalendarItem = McCalendar.QueryById<McCalendar> (Intent.Extras.GetInt (EXTRA_RELATED_CALENDAR_ID));
                    composeFragment.Composer.RelatedCalendarItem = relatedCalendarItem;
                }
                if (Intent.Extras.ContainsKey (EXTRA_RELATED_MESSAGE_ID)) {
                    var message = McEmailMessage.QueryById<McEmailMessage> (Intent.Extras.GetInt (EXTRA_MESSAGE_ID));
                    composeFragment.Composer.Message = message;
                }
                if (Intent.Extras.ContainsKey (EXTRA_INITIAL_TEXT)) {
                    var text = Intent.Extras.GetString (EXTRA_INITIAL_TEXT);
                    composeFragment.Composer.InitialText = text;
                }
            }

            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, composeFragment).AddToBackStack("Now").Commit ();
           
        }
    }
}

