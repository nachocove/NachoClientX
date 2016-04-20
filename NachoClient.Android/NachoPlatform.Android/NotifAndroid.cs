//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Support.V7.App;

using NachoClient.AndroidClient;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoPlatform
{
    public sealed class Notif : IPlatformNotif
    {
        private static volatile Notif instance;
        private static object syncRoot = new Object();

        private Notif ()
        {
        }

        public static Notif Instance
        {
            get 
            {
                if (instance == null) 
                {
                    lock (syncRoot) 
                    {
                        if (instance == null) 
                            instance = new Notif ();
                    }
                }
                return instance;
            }
        }

        public void ImmediateNotification (McEvent ev)
        {
            // Use the regular ScheduleNotification(), so that the notification cancelation is properly
            // scheduled.  Alarms in the past are delivered immediately, so the notification will appear
            // right away.
            ScheduleNotification (ev);
        }

        public void ScheduleNotification (McEvent ev)
        {
            string title;
            string body;
            Pretty.EventNotification (ev, out title, out body);
            var context = MainApplication.Instance.ApplicationContext;
            var alarmManager = (AlarmManager)context.GetSystemService (Context.AlarmService);
            // One alarm creates the notification at the event's reminder time.  A second alarm cancels
            // the notification when the event is over.  The first alarm should be exact and should wake
            // up the device.  The second alarm can safely be delayed and should not wake up the device.
            // The second alarm should be at lest fifteen minutes after the first, so the user has plenty
            // of time to see the notification.
            var createTime = ev.ReminderTime;
            var cancelTime = ev.GetEndTimeUtc ();
            if ((cancelTime - createTime) < TimeSpan.FromMinutes (15)) {
                cancelTime = createTime + TimeSpan.FromMinutes (15);
            }
            alarmManager.SetExact (AlarmType.RtcWakeup, createTime.MillisecondsSinceEpoch (),
                EventReminderNotificationPublisher.CreateNotificationIntent (context, ev.Id, title, body));
            alarmManager.Set (AlarmType.Rtc, cancelTime.MillisecondsSinceEpoch (),
                EventReminderNotificationPublisher.CancelNotificationIntent (context, ev.Id));
        }

        public void ScheduleNotifications (List<McEvent> events)
        {
            foreach (var ev in events) {
                ScheduleNotification (ev);
            }
        }

        public void CancelNotification (int eventId)
        {
            var context = MainApplication.Instance.ApplicationContext;
            var alarmManager = (AlarmManager)context.GetSystemService (Context.AlarmService);
            alarmManager.Cancel (EventReminderNotificationPublisher.CreateNotificationIntent (context, eventId, "", ""));
            alarmManager.Cancel (EventReminderNotificationPublisher.CancelNotificationIntent (context, eventId));
            var notificationManager = (NotificationManager)context.GetSystemService (Context.NotificationService);
            notificationManager.Cancel (EventReminderNotificationPublisher.NOTIFICATION_TAG, eventId);
        }

        public static void DumpNotifications ()
        {
            Log.Info (Log.LOG_CALENDAR, "NotifAndroid.DumpNotifications: Android does not provide a way to list the pending scheduled notifications.");
        }
    }
}

namespace NachoClient.AndroidClient
{
    /// <summary>
    /// The broadcast receiver class that runs when an alarm fires.  It creates the notification or cancels an existing notification.
    /// </summary>
    [BroadcastReceiver (Enabled = true)]
    [IntentFilter (new[] { EventReminderNotificationPublisher.ACTION_CREATE_NOTIFICATION, EventReminderNotificationPublisher.ACTION_CANCEL_NOTIFICATION })]
    public class EventReminderNotificationPublisher : BroadcastReceiver
    {
        private const string ACTION_CREATE_NOTIFICATION = "com.nachocove.nachomail.ACTION_CREATE_NOTIFICATION";
        private const string ACTION_CANCEL_NOTIFICATION = "com.nachocove.nachomail.ACTION_CANCEL_NOTIFICATION";

        private const string EXTRA_EVENT_ID = "com.nachocove.nachomail.EXTRA_EVENT_ID";
        private const string EXTRA_TITLE = "com.nachocove.nachomail.EXTRA_TITLE";
        private const string EXTRA_MESSAGE = "com.nachocove.nachomail.EXTRA_MESSAGE";

        public const string NOTIFICATION_TAG = "event";

        private static Intent BasicIntent (Context context, int eventId)
        {
            var intent = new Intent (context, typeof(EventReminderNotificationPublisher));
            intent.SetDataAndType (Android.Net.Uri.Parse (string.Format ("content://eventId/{0}", eventId)), "application/nachomailevent");
            intent.PutExtra (EXTRA_EVENT_ID, eventId);
            return intent;
        }

        public static PendingIntent CreateNotificationIntent (Context context, int eventId, string title, string message)
        {
            var intent = BasicIntent (context, eventId);
            intent.SetAction (ACTION_CREATE_NOTIFICATION);
            intent.PutExtra (EXTRA_TITLE, title);
            intent.PutExtra (EXTRA_MESSAGE, message);
            return PendingIntent.GetBroadcast (context, 0, intent, 0);
        }

        public static PendingIntent CancelNotificationIntent (Context context, int eventId)
        {
            var intent = BasicIntent (context, eventId);
            intent.SetAction (ACTION_CANCEL_NOTIFICATION);
            return PendingIntent.GetBroadcast (context, 0, intent, 0);
        }

        public override void OnReceive (Context context, Intent intent)
        {
            int eventId = intent.GetIntExtra (EXTRA_EVENT_ID, 0);
            var notificationManager = (NotificationManager)context.GetSystemService (Context.NotificationService);

            if (ACTION_CREATE_NOTIFICATION == intent.Action) {
                notificationManager.Notify (NOTIFICATION_TAG, eventId,
                    BuildNotification (context, eventId, intent.GetStringExtra (EXTRA_TITLE), intent.GetStringExtra (EXTRA_MESSAGE)));
            } else {
                notificationManager.Cancel (NOTIFICATION_TAG, eventId);
            }
        }

        public static Notification BuildNotification (Context context, int eventId, string title, string body)
        {
            var builder = new NotificationCompat.Builder (context);

            var largeIcon = BitmapFactory.DecodeResource (context.Resources, Resource.Drawable.Icon);
            largeIcon = Bitmap.CreateScaledBitmap (largeIcon, dp2px (context, 32), dp2px (context, 32), true);
            builder.SetLargeIcon (largeIcon);

            builder.SetSmallIcon (Resource.Drawable.Loginscreen_2);
            builder.SetPriority (NotificationCompat.PriorityHigh);
            builder.SetCategory (NotificationCompat.CategoryEvent);
            builder.SetVibrate (new long[] { 0, 100 }); // Vibration or sound needs to be set to trigger a heads-up notification.
            builder.SetContentTitle (title);
            builder.SetContentText (body);
            builder.SetAutoCancel (true);
            builder.SetContentIntent (EventNotificationActivity.ShowEventIntent (context, eventId));

            return builder.Build ();
        }

        private static int dp2px (Context context, int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, context.Resources.DisplayMetrics);
        }
    }

    /// <summary>
    /// The activity that runs when the user taps a notification for an event.  It open the event detail view for the event.
    /// </summary>
    [Activity (Label = "EventNotificationActivity")]
    public class EventNotificationActivity : NcActivity
    {
        private const string EXTRA_EVENT_ID = "com.nachocove.nachomail.EXTRA_EVENT_ID";

        public static PendingIntent ShowEventIntent (Context context, int eventId)
        {
            // The event ID is duplicated in the intent data and in the extra properties.  It is in the extra
            // properties because that is the easiest way to extract it when processing the intent.  It is in
            // the data because PendingIntents need to be unique, and the extra properties are not considered
            // when comparing PendingIntents.
            var intent = new Intent (context, typeof(EventNotificationActivity));
            intent.SetAction (Intent.ActionView);
            intent.SetDataAndType (Android.Net.Uri.Parse (string.Format ("content://eventId/{0}", eventId)), "application/nachomailevent");
            intent.PutExtra (EXTRA_EVENT_ID, eventId);
            return PendingIntent.GetActivity (context, 0, intent, 0);
        }

        protected override void OnCreate (Android.OS.Bundle savedInstanceState)
        {
            MainApplication.OneTimeStartup ("EventNotificationActivity");

            base.OnCreate (savedInstanceState);

            int eventId = Intent.GetIntExtra (EXTRA_EVENT_ID, 0);
            var ev = McEvent.QueryById<McEvent> (eventId);

            Intent eventIntent;
            if (null == ev) {
                eventIntent = NcTabBarActivity.CalendarIntent (this);
            } else {
                eventIntent = EventViewActivity.ShowEventIntent (this, ev);
            }
            eventIntent.SetFlags (ActivityFlags.NoAnimation);
            if (!NcTabBarActivity.TabBarWasCreated && null != ev) {
                // Since the app was just launched, pressing Back from the event detail view will return to
                // the home screen, which is not the desired behavior.  To keep the user in the app, insert
                // a calendar view activity into the activity back stack.
                StartActivities (new Intent[] { NcTabBarActivity.CalendarIntent (this), eventIntent });
            } else {
                StartActivity (eventIntent);
            }
            Finish ();
        }
    }
}
