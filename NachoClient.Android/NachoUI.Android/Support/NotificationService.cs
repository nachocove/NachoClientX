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
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
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
                    BadgeNotifUpdate ();
                }
            }
        }

        // It is okay if this function is called more than it needs to be.
        private void BadgeNotifUpdate ()
        {
            Log.Info (Log.LOG_UI, "BadgeNotifUpdate: called");

            var datestring = McMutables.GetOrCreate (McAccount.GetDeviceAccount ().Id, "Android", "BackgroundTime", DateTime.UtcNow.ToString ());
            var since = DateTime.Parse (datestring);
            var unreadAndHot = McEmailMessage.QueryUnreadAndHotAfter (since);
            var badgeCount = unreadAndHot.Count ();
            var soundExpressed = false;
            int remainingVisibleSlots = 10;
            var accountTable = new Dictionary<int, McAccount> ();

            var notifiedMessageIDs = new HashSet<string> ();

            foreach (var message in unreadAndHot) {
                if (!string.IsNullOrEmpty (message.MessageID) && notifiedMessageIDs.Contains (message.MessageID)) {
                    Log.Info (Log.LOG_UI, "BadgeNotifUpdate: Skipping message {0} because a message with that message ID has already been processed", message.Id);
                    --badgeCount;
                    message.MarkHasBeenNotified (true);
                    continue;
                }
                if (message.HasBeenNotified) {
                    if (message.ShouldNotify && !string.IsNullOrEmpty (message.MessageID)) {
                        notifiedMessageIDs.Add (message.MessageID);
                    }
                    continue;
                }
                McAccount account = null;
                if (!accountTable.TryGetValue (message.AccountId, out account)) {
                    var newAccount = McAccount.QueryById<McAccount> (message.AccountId);
                    if (null == newAccount) {
                        Log.Warn (Log.LOG_PUSH,
                            "Will not notify email message from an unknown account (accoundId={0}, emailMessageId={1})",
                            message.AccountId, message.Id);
                    }
                    accountTable.Add (message.AccountId, newAccount);
                    account = newAccount;
                }
                if ((null == account) || !NotificationHelper.ShouldNotifyEmailMessage (message, account)) {
                    --badgeCount;
                    message.MarkHasBeenNotified (false);
                    continue;
                }
                if (!NotifyEmailMessage (message, account, !soundExpressed)) {
                    Log.Info (Log.LOG_UI, "BadgeNotifUpdate: Notification attempt for message {0} failed.", message.Id);
                    --badgeCount;
                    continue;
                } else {
                    soundExpressed = true;
                }

                var updatedMessage = message.MarkHasBeenNotified (true);
                if (!string.IsNullOrEmpty (updatedMessage.MessageID)) {
                    notifiedMessageIDs.Add (updatedMessage.MessageID);
                }
                Log.Info (Log.LOG_UI, "BadgeNotifUpdate: Notification for message {0}", updatedMessage.Id);
                --remainingVisibleSlots;
                if (0 >= remainingVisibleSlots) {
                    break;
                }
            }
            accountTable.Clear ();

        }

        private bool NotifyEmailMessage (McEmailMessage message, McAccount account, bool withSound)
        {
            if (null == message) {
                return false;
            }
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

            DateTime UnixEpoch = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var largeIcon = BitmapFactory.DecodeResource (Resources, Resource.Drawable.Icon);
            largeIcon = Bitmap.CreateScaledBitmap (largeIcon, dp2px (32), dp2px (32), true);

            var builder = new NotificationCompat.Builder (this);
            builder.SetSmallIcon (Resource.Drawable.Loginscreen_2);
            builder.SetLargeIcon (largeIcon);
            builder.SetContentTitle (fromString);
            builder.SetContentText (subjectString);
            builder.SetWhen ((long)((message.DateReceived - UnixEpoch).TotalMilliseconds));
            builder.SetAutoCancel (true);

            var intent = NcTabBarActivity.HotListIntent (this);
            var pendingIntent = PendingIntent.GetActivity (this, 0, intent, 0);
            builder.SetContentIntent (pendingIntent);

            var nMgr = (NotificationManager)GetSystemService (NotificationService);
            nMgr.Notify (0, builder.Build ());

            return true;
        }

        private int dp2px (int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, Resources.DisplayMetrics);
        }

    }
}

