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
                if(!LifecycleSpy.SharedInstance.IsForeground()) {
                    BadgeNotifUpdate();
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

            var nMgr = (NotificationManager)GetSystemService (NotificationService);
            var notification = new Android.App.Notification( Resource.Drawable.notification, fromString);
            notification.Flags |= NotificationFlags.AutoCancel;
            var pendingIntent = PendingIntent.GetActivity (this, 0, new Intent (this, typeof(NowListActivity)), 0);
            notification.SetLatestEventInfo (this, fromString, subjectString, pendingIntent);
            nMgr.Notify (0, notification);

            return true;
        }

    }
}

