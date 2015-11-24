//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.App;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using System.Linq;
using System.Collections.Generic;
using Android.Content;
using Android.Support.V7.App;
using Android.Graphics;

namespace NachoClient.AndroidClient
{

    [Service]
    public class NotificationService : Service
    {

        public override void OnCreate ()
        {
            base.OnCreate ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void OnDestroy ()
        {
            base.OnDestroy ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public override StartCommandResult OnStartCommand (Android.Content.Intent intent, StartCommandFlags flags, int startId)
        {
            return StartCommandResult.Sticky;
        }

        public override Android.OS.IBinder OnBind (Android.Content.Intent intent)
        {
            throw new NotImplementedException ();
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            StatusIndEventArgs ea = (StatusIndEventArgs)e;
            // Use Info_SyncSucceeded rather than Info_NewUnreadEmailMessageInInbox because
            // we want to remove a notification if the server marks a message as read.
            // When the app is in QuickSync mode, BadgeNotifUpdate will be called when
            // QuickSync is done.  There isn't a need to call it when each account's sync
            // completes.
            if (NcResult.SubKindEnum.Info_SyncSucceeded == ea.Status.SubKind) {
                if (!LifecycleSpy.SharedInstance.IsForeground ()) {
                    try {
                        ShowNotifications ();
                    } catch (Exception ex) {
                        Log.Error (Log.LOG_EMAIL, "NotificationService: {0}", ex);
                    }
                }
            }
        }

        // It is okay if this function is called more than it needs to be.
        private void ShowNotifications ()
        {
            Log.Info (Log.LOG_UI, "BadgeNotifUpdate: called");

            var datestring = McMutables.GetOrCreate (McAccount.GetDeviceAccount ().Id, "Android", "BackgroundTime", DateTime.UtcNow.ToString ());
            var since = DateTime.Parse (datestring);
            var unreadAndHot = McEmailMessage.QueryUnreadAndHotAfter (since);

            unreadAndHot.RemoveAll (x => String.IsNullOrEmpty (x.From));

            if (1 == unreadAndHot.Count) {
                ExpandedNotification (0, unreadAndHot.ElementAt (0));
            } else if (1 < unreadAndHot.Count) {
                InboxNotification (0, unreadAndHot);
            }
        }

        private bool ExpandedNotification (int accountId, McEmailMessage message)
        {
            if (String.IsNullOrEmpty (message.From)) {
                // Don't notify or count in badge number from-me messages.
                Log.Info (Log.LOG_UI, "Not notifying on to-{0} message.", NcApplication.Instance.Account.EmailAddr);
                return false;
            }

            var fromString = Pretty.SenderString (message.From);
            var subjectString = Pretty.SubjectString (message.Subject);
            if (!String.IsNullOrEmpty (subjectString)) {
                subjectString += " ";
            }

            if (BuildInfoHelper.IsDev || BuildInfoHelper.IsAlpha) {
                // Add debugging info for dev & alpha
                var latency = (DateTime.UtcNow - message.DateReceived).TotalSeconds;
                subjectString += String.Format ("[{0:N1}s]", latency);
            }

            var largeIcon = BitmapFactory.DecodeResource (Resources, Resource.Drawable.Icon);
            largeIcon = Bitmap.CreateScaledBitmap (largeIcon, dp2px (32), dp2px (32), true);

            var builder = new NotificationCompat.Builder (this);
            builder.SetSmallIcon (Resource.Drawable.Loginscreen_2);
            builder.SetLargeIcon (largeIcon);
            builder.SetContentTitle (fromString);
            builder.SetContentText (subjectString);
            builder.SetWhen (message.DateReceived.MillisecondsSinceEpoch ());
            builder.SetAutoCancel (true);

            var preview = EmailHelper.AdjustPreviewText (message.GetBodyPreviewOrEmpty ());

            if (!String.IsNullOrEmpty (preview)) {
                var expanded = new NotificationCompat.BigTextStyle ();
                expanded.SetBigContentTitle (fromString);
                expanded.SetSummaryText (subjectString);
                expanded.BigText (preview);
                builder.SetStyle (expanded);
            }
           
            var intent = NotificationActivity.ShowMessageIntent (this, message);
            var pendingIntent = PendingIntent.GetActivity (this, 0, intent, 0);
            builder.SetContentIntent (pendingIntent);

            var nMgr = (NotificationManager)GetSystemService (NotificationService);
            nMgr.Notify (accountId, builder.Build ());

            return true;
        }

        private bool InboxNotification (int accountId, List<McEmailMessage> messages)
        {
            var countString = String.Format ("{0} new messages", messages.Count);

            var largeIcon = BitmapFactory.DecodeResource (Resources, Resource.Drawable.Icon);
            largeIcon = Bitmap.CreateScaledBitmap (largeIcon, dp2px (32), dp2px (32), true);

            var builder = new NotificationCompat.Builder (this);
            builder.SetSmallIcon (Resource.Drawable.Loginscreen_2);
            builder.SetLargeIcon (largeIcon);
            builder.SetContentTitle (countString);
            var UnixEpoch = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            builder.SetWhen ((long)((DateTime.UtcNow - UnixEpoch).TotalMilliseconds));
            builder.SetAutoCancel (true);

            var inbox = new NotificationCompat.InboxStyle ();

            inbox.SetBigContentTitle (countString);

            foreach (var message in messages) {

                var fromString = Pretty.SenderString (message.From);
                var subjectString = Pretty.SubjectString (message.Subject);
                if (!String.IsNullOrEmpty (subjectString)) {
                    subjectString += " ";
                }
                if (BuildInfoHelper.IsDev || BuildInfoHelper.IsAlpha) {
                    // Add debugging info for dev & alpha
                    var latency = (DateTime.UtcNow - message.DateReceived).TotalSeconds;
                    subjectString += String.Format ("[{0:N1}s]", latency);
                }

                inbox.AddLine (Pretty.Join (fromString, subjectString));
            }

            builder.SetStyle (inbox);

            var intent = NotificationActivity.ShowMessageIntent (this, messages.ElementAt (0));
            var pendingIntent = PendingIntent.GetActivity (this, 0, intent, 0);
            builder.SetContentIntent (pendingIntent);

            var nMgr = (NotificationManager)GetSystemService (NotificationService);
            nMgr.Notify (accountId, builder.Build ());

            return true;
        }

        private int dp2px (int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, Resources.DisplayMetrics);
        }

    }
}

