﻿using System;

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
    public class MessageComposeActivity : NcActivity
    {

        public static readonly string EXTRA_ACTION = "com.nachocove.nachomail.action";
        public static readonly string EXTRA_RELATED_MESSAGE_ID = "com.nachocove.nachomail.relatedMessageId";
        public static readonly string EXTRA_RELATED_CALENDAR_ID = "com.nachocove.nachomail.relatedCalendarId";
        public static readonly string EXTRA_MESSAGE = "com.nachocove.nachomail.message";
        public static readonly string EXTRA_INITIAL_TEXT = "com.nachocove.nachomail.initialText";

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
                composeFragment.Composer.Message = IntentHelper.RetrieveValue<McEmailMessage> (Intent.GetStringExtra (EXTRA_MESSAGE));
            }
            if (Intent.HasExtra (EXTRA_INITIAL_TEXT)) {
                var text = Intent.GetStringExtra (EXTRA_INITIAL_TEXT);
                composeFragment.Composer.InitialText = text;
            }

            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, composeFragment).AddToBackStack("Now").Commit ();
           
        }

        public static Intent NewMessageIntent (Context context)
        {
            var intent = new Intent (context, typeof(MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
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
    }
}

