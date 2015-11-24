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

        public void ImmediateNotification (int handle, string message)
        {
            var context = MainApplication.Instance.ApplicationContext;
            var notificationManager = (NotificationManager)context.GetSystemService (Context.NotificationService);
            notificationManager.Notify (handle, EventReminderNotificationPublisher.BuildNotification (context, handle, message));
        }

        public void ScheduleNotification (int handle, DateTime when, string message)
        {
            var context = MainApplication.Instance.ApplicationContext;
            var alarmManager = (AlarmManager)context.GetSystemService (Context.AlarmService);
            alarmManager.SetExact (AlarmType.RtcWakeup, when.MillisecondsSinceEpoch (),
                EventReminderNotificationPublisher.CreatePendingIntent (context, handle, message));
        }

        public void ScheduleNotification (NotificationInfo notification)
        {
            ScheduleNotification (notification.Handle, notification.When, notification.Message);
        }

        public void ScheduleNotifications (List<NotificationInfo> notifications)
        {
            foreach (var notification in notifications) {
                ScheduleNotification (notification);
            }
        }

        public void CancelNotification (int handle)
        {
            var context = MainApplication.Instance.ApplicationContext;
            var alarmManager = (AlarmManager)context.GetSystemService (Context.AlarmService);
            alarmManager.Cancel (EventReminderNotificationPublisher.CreatePendingIntent (context, handle, ""));
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
    /// The broadcast receiver class that runs when an alarm fires.  It creates the notification.
    /// </summary>
    [BroadcastReceiver (Enabled = true)]
    [IntentFilter (new[] { "com.nachocove.nachomail.ACTION_EVENT_ALARM" })]
    public class EventReminderNotificationPublisher : BroadcastReceiver
    {
        private const string EXTRA_EVENT_ID = "com.nachocove.nachomail.EXTRA_EVENT_ID";
        private const string EXTRA_MESSAGE = "com.nachocove.nachomail.EXTRA_MESSAGE";

        public static PendingIntent CreatePendingIntent (Context context, int eventId, string message)
        {
            // The event ID is duplicated in the intent data and in the extra properties.  It is in the extra
            // properties because that is the easiest way to extract it when processing the intent.  It is in
            // the data because PendingIntents need to be unique, and the extra properties are not considered
            // when comparing PendingIntents.
            var intent = new Intent (context, typeof(EventReminderNotificationPublisher));
            intent.SetAction ("com.nachocove.nachomail.ACTION_EVENT_ALARM");
            intent.SetDataAndType (Android.Net.Uri.Parse (string.Format ("content://eventId/{0}", eventId)), "application/nachomailevent");
            intent.PutExtra (EXTRA_EVENT_ID, eventId);
            intent.PutExtra (EXTRA_MESSAGE, message);
            return PendingIntent.GetBroadcast (context, 0, intent, 0);
        }

        public override void OnReceive (Context context, Intent intent)
        {
            int eventId = intent.GetIntExtra (EXTRA_EVENT_ID, 0);

            var notificationManager = (NotificationManager)context.GetSystemService (Context.NotificationService);
            notificationManager.Notify (eventId, BuildNotification (context, eventId, intent.GetStringExtra (EXTRA_MESSAGE)));
        }

        public static Notification BuildNotification (Context context, int eventId, string message)
        {
            var builder = new NotificationCompat.Builder (context);

            var largeIcon = BitmapFactory.DecodeResource (context.Resources, Resource.Drawable.Icon);
            largeIcon = Bitmap.CreateScaledBitmap (largeIcon, dp2px (context, 32), dp2px (context, 32), true);
            builder.SetLargeIcon (largeIcon);

            builder.SetSmallIcon (Resource.Drawable.Loginscreen_2);
            builder.SetContentTitle ("Nacho Mail");
            builder.SetContentText (message);
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
            StartActivity (eventIntent);
            Finish ();
        }
    }
}