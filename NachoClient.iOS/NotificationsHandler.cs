//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

using ObjCRuntime;
using Foundation;
using UIKit;
using CoreGraphics;
using CoreAnimation;

using Newtonsoft.Json;

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;

namespace NachoClient.iOS
{

    public class NotificationsHandler
    {

        private const string NotificationActionIdentifierReply = "reply";
        private const string NotificationActionIdentifierMark = "mark";
        private const string NotificationActionIdentifierDelete = "delete";
        private const string NotificationActionIdentifierArchive = "archive";
        private const string NotificationCategoryIdentifierMessage = "message";
        private const string NotificationCategoryIdentifierChat = "chat";

        static public NSString EmailNotificationKey = new NSString ("McEmailMessage.Id");
        static public NSString ChatNotificationKey = new NSString ("McChat.Id,McChatMessage.MessageId");
        static public NSString EventNotificationKey = new NSString ("NotifiOS.handle");

        static public NSString LocalNotificationReceivedNotificationName = new NSString ("NachoClient.iOS.NotificationHandler.LocalNotificationReceivedNotificationName");

        private const UIUserNotificationType KNotificationSettings = UIUserNotificationType.Alert | UIUserNotificationType.Badge | UIUserNotificationType.Sound;

        public BackgroundController BackgroundController;

        public NotificationsHandler ()
        {
        }

        #region State

        public bool HasRegisteredForRemoteNotifications { get; private set; } = false;
        private bool NotifyAllowed = true;

        private ulong UserNotificationSettings {
            get {
                if (UIDevice.CurrentDevice.CheckSystemVersion (8, 0)) {
                    return (ulong)UIApplication.SharedApplication.CurrentUserNotificationSettings.Types;
                }
                // Older iOS does not have this property. So, just assume it's ok and let 
                // iOS to reject it.
                return (ulong)KNotificationSettings;
            }
        }

        private bool NotificationCanAlert {
            get {
                return (0 != (UserNotificationSettings & (ulong)UIUserNotificationType.Alert));
            }
        }

        private bool NotificationCanSound {
            get {
                return (0 != (UserNotificationSettings & (ulong)UIUserNotificationType.Sound));
            }
        }

        private bool NotificationCanBadge {
            get {
                return (0 != (UserNotificationSettings & (ulong)UIUserNotificationType.Badge));
            }
        }

        public void BecomeActive ()
        {
            NcApplication.Instance.StatusIndEvent -= BackgroundStatusIndHandler;
            NotifyAllowed = false;
            UpdateBadgeCount ();
        }

        public void BecomeInactive ()
        {
            UpdateBadgeCount ();
            NotifyAllowed = true;
            NcApplication.Instance.StatusIndEvent += BackgroundStatusIndHandler;
        }

        #endregion

        #region Registration

        public void RegisterForNotifications ()
        {
            if (UIApplication.SharedApplication.RespondsToSelector (new Selector ("registerUserNotificationSettings:"))) {
                // iOS 8 and after
                var replyAction = new UIMutableUserNotificationAction ();
                replyAction.ActivationMode = UIUserNotificationActivationMode.Foreground;
                replyAction.Identifier = NotificationActionIdentifierReply;
                replyAction.Title = "Reply";
                var markAction = new UIMutableUserNotificationAction ();
                markAction.ActivationMode = UIUserNotificationActivationMode.Background;
                markAction.Identifier = NotificationActionIdentifierMark;
                markAction.Title = "Mark as Read";
                var archiveAction = new UIMutableUserNotificationAction ();
                archiveAction.ActivationMode = UIUserNotificationActivationMode.Background;
                archiveAction.Identifier = NotificationActionIdentifierArchive;
                archiveAction.Title = "Archive";
                var deleteAction = new UIMutableUserNotificationAction ();
                deleteAction.ActivationMode = UIUserNotificationActivationMode.Background;
                deleteAction.Identifier = NotificationActionIdentifierDelete;
                deleteAction.Title = "Delete";
                deleteAction.Destructive = true;
                var defaultActions = new UIUserNotificationAction [] { replyAction, markAction, archiveAction, deleteAction };
                var minimalActions = new UIUserNotificationAction [] { replyAction, markAction };
                var messageCategory = new UIMutableUserNotificationCategory ();
                messageCategory.Identifier = NotificationCategoryIdentifierMessage;
                messageCategory.SetActions (defaultActions, UIUserNotificationActionContext.Default);
                messageCategory.SetActions (minimalActions, UIUserNotificationActionContext.Minimal);
                var categories = new NSSet (messageCategory);
                var settings = UIUserNotificationSettings.GetSettingsForTypes (KNotificationSettings, categories);
                UIApplication.SharedApplication.RegisterUserNotificationSettings (settings);
                UIApplication.SharedApplication.RegisterForRemoteNotifications ();
            } else if (UIApplication.SharedApplication.RespondsToSelector (new Selector ("registerForRemoteNotificationTypes:"))) {
                UIApplication.SharedApplication.RegisterForRemoteNotificationTypes (UIRemoteNotificationType.NewsstandContentAvailability);
            } else {
                Log.Error (Log.LOG_PUSH, "notification not registered!");
            }
        }

        public void HandleRemoteNoficationRegistration (NSData deviceToken)
        {
            HasRegisteredForRemoteNotifications = true;
            var deviceTokenBytes = deviceToken.ToArray ();
            PushAssist.SetDeviceToken (Convert.ToBase64String (deviceTokenBytes));
            Log.Info (Log.LOG_LIFECYCLE, "RegisteredForRemoteNotifications: {0}", deviceToken.ToString ());
        }

        public void HandleRemoteNotificationRegistrationError (NSError error)
        {
            // null Value indicates token is lost.
            PushAssist.SetDeviceToken (null);
            Log.Info (Log.LOG_LIFECYCLE, "FailedToRegisterForRemoteNotifications: {0}", error.LocalizedDescription);
        }

        #endregion

        #region Handling User Interaction

        public void HandleLaunchNotification (UILocalNotification localNotification)
        {
            var emailNotificationDictionary = localNotification.UserInfo.ObjectForKey (EmailNotificationKey);
            var eventNotificationDictionary = localNotification.UserInfo.ObjectForKey (EventNotificationKey);
            var chatNotificationDictionary = (NSArray)localNotification.UserInfo.ObjectForKey (ChatNotificationKey);
            if (emailNotificationDictionary != null) {
                var emailMessageId = ((NSNumber)emailNotificationDictionary).NIntValue;
                SaveNotification ("FinishedLaunching", EmailNotificationKey, emailMessageId);
            } else if (eventNotificationDictionary != null) {
                var eventId = ((NSNumber)eventNotificationDictionary).NIntValue;
                SaveNotification ("FinishedLaunching", EventNotificationKey, eventId);
            } else if (chatNotificationDictionary != null) {
                var chatId = (chatNotificationDictionary.GetItem<NSNumber> (0)).NIntValue;
                var messageId = (chatNotificationDictionary.GetItem<NSNumber> (1)).NIntValue;
                SaveNotification ("FinishedLaunching", ChatNotificationKey, new nint [] { chatId, messageId });
            }
        }

        public void HandleLocalNotification (UILocalNotification notification)
        {
            var emailMutables = McMutables.Get (McAccount.GetDeviceAccount ().Id, EmailNotificationKey);
            var eventMutables = McMutables.Get (McAccount.GetDeviceAccount ().Id, EventNotificationKey);
            var chatMutables = McMutables.Get (McAccount.GetDeviceAccount ().Id, ChatNotificationKey);

            var emailNotification = (NSNumber)notification.UserInfo.ObjectForKey (EmailNotificationKey);
            var eventNotification = (NSNumber)notification.UserInfo.ObjectForKey (EventNotificationKey);
            var chatNotification = (NSArray)notification.UserInfo.ObjectForKey (ChatNotificationKey);

            // The app is 'active' if it is already running when the local notification
            // arrives or if the app is started when a local notification is delivered.
            // When the app is started by a notification, FinishedLauching adds mutables.
            bool appWasAlreadyActive = (UIApplicationState.Active == UIApplication.SharedApplication.ApplicationState) && (emailMutables.Count + eventMutables.Count + chatMutables.Count == 0);
            if (appWasAlreadyActive) {
                string title = notification.AlertTitle;
                string body = notification.AlertBody;
                if (string.IsNullOrEmpty (title)) {
                    title = "Reminder";
                } else if (body.StartsWith (title + ": ")) {
                    body = body.Substring (title.Length + 2);
                }
                var alert = UIAlertController.Create (title, body, UIAlertControllerStyle.Alert);
                alert.AddAction (UIAlertAction.Create ("OK", UIAlertActionStyle.Default, (obj) => { }));
                var viewController = (UIApplication.SharedApplication.Delegate as AppDelegate).Window.RootViewController;
                while (viewController.PresentedViewController != null) {
                    viewController = viewController.PresentedViewController;
                }
                viewController.PresentViewController (alert, true, null);
            } else {
                if (emailNotification != null) {
                    var emailMessageId = emailNotification.ToMcModelIndex ();
                    SaveNotification ("ReceivedLocalNotification", EmailNotificationKey, emailMessageId);
                } else if (eventNotification != null) {
                    var eventId = eventNotification.ToMcModelIndex ();
                    SaveNotification ("ReceivedLocalNotification", EventNotificationKey, eventId);
                } else if (chatNotification != null) {
                    var chatId = ((NSNumber)chatNotification.GetItem<NSNumber> (0)).ToMcModelIndex ();
                    var messageId = ((NSNumber)chatNotification.GetItem<NSNumber> (1)).ToMcModelIndex ();
                    SaveNotification ("ReceivedLocalNotification", ChatNotificationKey, new nint [] { chatId, messageId });
                } else {
                    Log.Error (Log.LOG_LIFECYCLE, "ReceivedLocalNotification: received unknown notification");
                }
                NSNotificationCenter.DefaultCenter.PostNotificationName (LocalNotificationReceivedNotificationName, null);
            }
        }

        public void HandleAction (string actionIdentifier, UILocalNotification localNotification, Action completionHandler)
        {
            var emailNotification = (NSNumber)localNotification.UserInfo.ObjectForKey (EmailNotificationKey);
            if (emailNotification != null) {
                var emailMessageId = emailNotification.ToMcModelIndex ();
                var thread = new McEmailMessageThread ();
                thread.FirstMessageId = emailMessageId;
                thread.MessageCount = 1;
                var message = thread.FirstMessageSpecialCase ();
                if (null != message) {
                    if (actionIdentifier == NotificationActionIdentifierReply) {
                        if (NcApplication.ReadyToStartUI ()) {
                            var account = McAccount.EmailAccountForMessage (message);
                            EmailHelper.MarkAsRead (thread, force: true);
                            var composeViewController = new MessageComposeViewController (account);
                            composeViewController.Composer.RelatedThread = thread;
                            composeViewController.Composer.Kind = EmailHelper.Action.Reply;
                            composeViewController.Present (false, null);
                        }
                    } else if (actionIdentifier == NotificationActionIdentifierArchive) {
                        NcEmailArchiver.Archive (message);
                        BadgeCountAndMessageNotifications ();
                    } else if (actionIdentifier == NotificationActionIdentifierMark) {
                        // Bypassing EmailHelper becuase it runs the command in a task and the message notifications code doesn't see the change
                        BackEnd.Instance.MarkEmailReadCmd (message.AccountId, message.Id, true);
                        BadgeCountAndMessageNotifications ();
                    } else if (actionIdentifier == NotificationActionIdentifierDelete) {
                        NcEmailArchiver.Delete (message);
                        BadgeCountAndMessageNotifications ();
                    } else {
                        NcAssert.CaseError ("Unknown notification action");
                    }
                }
            }
            completionHandler ();
        }

        public void HandleRemoteNotification (NSDictionary userInfo, Action<UIBackgroundFetchResult> completionHandler)
        {
            Log.Info (Log.LOG_LIFECYCLE, "[PA] Got remote notification - {0}", userInfo);

            // Amazingly, it turns out the most programmatically simple way to convert a NSDictionary
            // to our own model objects.
            NSError error;
            var jsonData = NSJsonSerialization.Serialize (userInfo, NSJsonWritingOptions.PrettyPrinted, out error);
            var jsonStr = (string)NSString.FromData (jsonData, NSStringEncoding.UTF8);
            var notification = JsonConvert.DeserializeObject<Notification> (jsonStr);
            if (notification.HasPingerSection ()) {
                if (!PushAssist.ProcessRemoteNotification (notification.pinger, (accountId) => {
                    if (NcApplication.Instance.IsForeground) {
                        var inbox = NcEmailManager.PriorityInbox (accountId);
                        inbox.StartSync ();
                    }
                })) {
                    // Can't find any account matching those contexts. Abort immediately
                    completionHandler (UIBackgroundFetchResult.NoData);
                    return;
                }
                if (NcApplication.Instance.IsForeground) {
                    completionHandler (UIBackgroundFetchResult.NewData);
                } else {
                    if (BackgroundController.IsFetching) {
                        Log.Warn (Log.LOG_PUSH, "A perform fetch is already in progress. Do not start another one.");
                        completionHandler (UIBackgroundFetchResult.NewData);
                    } else {
                        BackgroundController.StartFetch (completionHandler, BackgroundController.FetchCause.RemoteNotification);
                    }
                }
            }
        }

        #endregion

        #region Posting Notifications

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
            int subjectLength;
            var previewString = Pretty.MessagePreview (message, out subjectLength, maxSubjectLength: 30);
            if (message.Intent != McEmailMessage.IntentType.None) {
                previewString = EmailHelper.CreateSubjectWithIntent (previewString, message.Intent, message.IntentDateType, message.IntentDate);
            }

            if (BuildInfoHelper.IsDev || BuildInfoHelper.IsAlpha) {
                // Add debugging info for dev & alpha
                var latency = (DateTime.UtcNow - message.DateReceived).TotalSeconds;
                var cause = BackgroundController.FetchCauseString;
                fromString += String.Format (" [{0}:{1:N1}s]", cause, latency);
                Log.Info (Log.LOG_PUSH, "[PA] notify email message: client_id={0}, message_id={1}, cause={2}, delay={3}",
                    NcApplication.Instance.ClientId, message.Id, cause, latency);
            }

            InvokeOnUIThread.Instance.Invoke (() => {
                if (NotificationCanAlert) {
                    var notif = new UILocalNotification ();
                    notif.Category = NotificationCategoryIdentifierMessage;
                    notif.AlertBody = String.Format ("{0}\n{1}", fromString, previewString);
                    if (notif.RespondsToSelector (new Selector ("setAlertTitle:"))) {
                        notif.AlertTitle = "New Email";
                    }
                    notif.AlertAction = null;
                    notif.UserInfo = NSDictionary.FromObjectAndKey (NSNumber.FromInt32 (message.Id), EmailNotificationKey);
                    if (withSound) {
                        if (NotificationCanSound) {
                            notif.SoundName = UILocalNotification.DefaultSoundName;
                        } else {
                            Log.Warn (Log.LOG_UI, "No permission to play sound. (emailMessageId={0})", message.Id);
                        }
                    }
                    UIApplication.SharedApplication.ScheduleLocalNotification (notif);
                } else {
                    Log.Warn (Log.LOG_UI, "No permission to badge. (emailMessageId={0})", message.Id);
                }
            });

            return true;
        }

        private bool NotifyChatMessage (NcChatMessage message, McChat chat, McAccount account, bool withSound)
        {
            var fromString = Pretty.SenderString (message.From);
            var bundle = new NcEmailMessageBundle (message);
            string preview = bundle.TopText;
            if (String.IsNullOrWhiteSpace (preview)) {
                preview = message.BodyPreview;
            }
            if (String.IsNullOrWhiteSpace (preview)) {
                return false;
            }

            if (BuildInfoHelper.IsDev || BuildInfoHelper.IsAlpha) {
                // Add debugging info for dev & alpha
                var latency = (DateTime.UtcNow - message.DateReceived).TotalSeconds;
                var cause = BackgroundController.FetchCauseString;
                preview = String.Format ("[{0}:{1:N1}s] {2}", cause, latency, preview);

                Log.Info (Log.LOG_PUSH, "[PA] notify email message: client_id={0}, message_id={1}, cause={2}, delay={3}",
                    NcApplication.Instance.ClientId, message.Id, cause, latency);
            }

            InvokeOnUIThread.Instance.Invoke (() => {
                if (NotificationCanAlert) {
                    var notif = new UILocalNotification ();
                    notif.Category = NotificationCategoryIdentifierChat;
                    notif.AlertBody = String.Format ("{0}\n{1}", fromString, preview);
                    if (notif.RespondsToSelector (new Selector ("setAlertTitle:"))) {
                        notif.AlertTitle = "New Chat Message";
                    }
                    notif.AlertAction = null;
                    notif.UserInfo = NSDictionary.FromObjectAndKey (NSArray.FromNSObjects (NSNumber.FromInt32 (message.ChatId), NSNumber.FromInt32 (message.Id)), ChatNotificationKey);
                    if (withSound) {
                        if (NotificationCanSound) {
                            notif.SoundName = UILocalNotification.DefaultSoundName;
                        } else {
                            Log.Warn (Log.LOG_UI, "No permission to play sound. (emailMessageId={0})", message.Id);
                        }
                    }
                    UIApplication.SharedApplication.ScheduleLocalNotification (notif);
                } else {
                    Log.Warn (Log.LOG_UI, "No permission to badge. (emailMessageId={0})", message.Id);
                }
            });
            return true;
        }

        #endregion

        #region Application Icon Badge

        bool IsRunningBadgeUpdate;
        bool NeedsBadgeUpdate;
        object BadgeUpdateLock = new object ();

        public void UpdateBadgeCount ()
        {
            bool isTaskRunning = false;
            lock (BadgeUpdateLock) {
                if (IsRunningBadgeUpdate) {
                    NeedsBadgeUpdate = true;
                    isTaskRunning = true;
                } else {
                    IsRunningBadgeUpdate = true;
                }
            }
            if (!isTaskRunning) {
                // Calculating the badge count requires database queries that are sometimes very slow.
                // Slow enough that they should not be run on the UI thread.
                NcTask.Run (() => {
                    bool needsRun = true;
                    while (needsRun) {
                        UpdateBadgeCountTask ();
                        lock (BadgeUpdateLock) {
                            if (NeedsBadgeUpdate) {
                                NeedsBadgeUpdate = false;
                            } else {
                                IsRunningBadgeUpdate = false;
                                needsRun = false;
                            }
                        }
                    }
                }, "UpdateBadgeCount");
            }
        }

        private void UpdateBadgeCountTask ()
        {
            int badgeCount;
            bool shouldClearBadge = EmailHelper.HowToDisplayUnreadCount () == EmailHelper.ShowUnreadEnum.RecentMessages && !NotifyAllowed;
            if (shouldClearBadge) {
                badgeCount = 0;
            } else {
                badgeCount = EmailHelper.GetUnreadMessageCountForBadge ();
                badgeCount += McChat.UnreadMessageCountForBadge ();
                badgeCount += McAction.CountOfNewActionsForBadge ();
                Log.Info (Log.LOG_UI, "UpdateBadgeCount: badge count = {0}", badgeCount);
            }
            InvokeOnUIThread.Instance.Invoke (() => {
                UIApplication.SharedApplication.ApplicationIconBadgeNumber = badgeCount;
            });
        }

        bool NeedsBadgeAndNotifications;
        bool IsRunningBadgeAndNotifications;
        object BadgeAndNotificationsLock = new object ();
        List<Action> BadgeAndNotificationCallbacks = new List<Action> ();

        public void BadgeCountAndMessageNotifications (Action updateDone = null)
        {
            if (Thread.CurrentThread.ManagedThreadId != NcApplication.Instance.UiThreadId) {
                // We need to access NotificationCanBadge, which must be called on the UI thread, so if we're
                // not already on the UI thread, dispatch a call to the UI thread.
                InvokeOnUIThread.Instance.Invoke (() => {
                    BadgeCountAndMessageNotifications (updateDone);
                });
                return;
            }
            // NotificationCanBadge must be called on the UI thread, so it must be called before starting the task.
            bool isTaskRunning = false;
            lock (BadgeAndNotificationsLock) {
                if (IsRunningBadgeAndNotifications) {
                    isTaskRunning = true;
                    NeedsBadgeAndNotifications = true;
                } else {
                    IsRunningBadgeAndNotifications = true;
                }
                if (updateDone != null) {
                    BadgeAndNotificationCallbacks.Add (updateDone);
                }
            }
            if (!isTaskRunning) {
                bool canBadge = NotificationCanBadge;
                NcTask.Run (() => {
                    bool needsRun = true;
                    List<Action> callbacks = new List<Action> ();
                    while (needsRun) {
                        BadgeNotificationsTask (canBadge);
                        lock (BadgeAndNotificationsLock) {
                            if (NeedsBadgeAndNotifications) {
                                NeedsBadgeAndNotifications = false;
                            } else {
                                IsRunningBadgeAndNotifications = false;
                                needsRun = false;
                                callbacks = new List<Action> (BadgeAndNotificationCallbacks);
                                BadgeAndNotificationCallbacks.Clear ();
                            }
                        }
                    }
                    if (callbacks.Count > 0) {
                        InvokeOnUIThread.Instance.Invoke (() => {
                            foreach (var callback in callbacks) {
                                callback ();
                            }
                        });
                    }
                }, "BadgeCountAndMessageNotifications");
            }
        }

        private void BadgeNotificationsTask (bool canBadge)
        {
            if (canBadge) {
                UpdateBadgeCountTask ();
            } else {
                Log.Info (Log.LOG_UI, "Skip badging due to lack of user permission.");
            }

            Log.Info (Log.LOG_UI, "Message notifications: called");
            if (!NotifyAllowed) {
                Log.Info (Log.LOG_UI, "Message notifications: early exit");
                return;
            }

            var datestring = McMutables.GetOrCreate (McAccount.GetDeviceAccount ().Id, "IOS", "GoInactiveTime", DateTime.UtcNow.ToString ());
            var since = DateTime.Parse (datestring);
            var unreadAndHot = McEmailMessage.QueryUnreadAndHotAfter (since);
            var unreadChatMessages = McChat.UnreadMessagesSince (since);
            var soundExpressed = false;
            int remainingVisibleSlots = 10;
            var accountTable = new Dictionary<int, McAccount> ();
            var chatTable = new Dictionary<int, McChat> ();
            McAccount account = null;
            McChat chat = null;

            var notifiedMessageIDs = new HashSet<string> ();

            foreach (var message in unreadAndHot) {
                if (!string.IsNullOrEmpty (message.MessageID) && notifiedMessageIDs.Contains (message.MessageID)) {
                    Log.Info (Log.LOG_UI, "Message notifications: Skipping message {0} because a message with that message ID has already been processed", message.Id);
                    message.MarkHasBeenNotified (true);
                    continue;
                }
                if (message.HasBeenNotified) {
                    if (message.ShouldNotify && !string.IsNullOrEmpty (message.MessageID)) {
                        notifiedMessageIDs.Add (message.MessageID);
                    }
                    continue;
                }
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
                if ((null == account) || !NotificationHelper.ShouldNotifyEmailMessage (message)) {
                    message.MarkHasBeenNotified (false);
                    continue;
                }
                if (!NotifyEmailMessage (message, account, !soundExpressed)) {
                    Log.Info (Log.LOG_UI, "Message notifications: Notification attempt for message {0} failed.", message.Id);
                    continue;
                } else {
                    soundExpressed = true;
                }

                var updatedMessage = message.MarkHasBeenNotified (true);
                if (!string.IsNullOrEmpty (updatedMessage.MessageID)) {
                    notifiedMessageIDs.Add (updatedMessage.MessageID);
                }
                Log.Info (Log.LOG_UI, "Message notifications: Notification for message {0}", updatedMessage.Id);
                --remainingVisibleSlots;
                if (0 >= remainingVisibleSlots) {
                    break;
                }
            }

            if (remainingVisibleSlots > 0) {
                foreach (var message in unreadChatMessages) {
                    if (!accountTable.TryGetValue (message.AccountId, out account)) {
                        account = McAccount.QueryById<McAccount> (message.AccountId);
                        if (null == account) {
                            Log.Warn (Log.LOG_PUSH, "Will not notify chat message from an unknown account (accoundId={0}, emailMessageId={1})", message.AccountId, message.Id);
                        }
                        accountTable.Add (message.AccountId, account);
                    }
                    if (!chatTable.TryGetValue (message.ChatId, out chat)) {
                        chat = McChat.QueryById<McChat> (message.ChatId);
                        if (null == chat) {
                            Log.Warn (Log.LOG_PUSH, "Will not notify chat message from an unknown chat (chatId={0}, emailMessageId={1})", message.ChatId, message.Id);
                        }
                        chatTable.Add (message.ChatId, chat);
                    }
                    if (message.HasBeenNotified) {
                        continue;
                    }
                    if ((null == account) || (null == chat) || !NotificationHelper.ShouldNotifyChatMessage (message)) {
                        // Have to re-query as McEmailMessage or else UpdateWithOCApply complains of a type mismatch
                        McEmailMessage.QueryById<McEmailMessage> (message.Id).MarkHasBeenNotified (false);
                        continue;
                    }
                    if (!NotifyChatMessage (message, chat, account, !soundExpressed)) {
                        Log.Info (Log.LOG_UI, "Message notifications: Notification attempt for message {0} failed.", message.Id);
                        continue;
                    } else {
                        soundExpressed = true;
                    }
                    // Have to re-query as McEmailMessage or else UpdateWithOCApply complains of a type mismatch
                    McEmailMessage.QueryById<McEmailMessage> (message.Id).MarkHasBeenNotified (true);
                    Log.Info (Log.LOG_UI, "Message notifications: Notification for message {0}", message.Id);
                    --remainingVisibleSlots;
                    if (0 >= remainingVisibleSlots) {
                        break;
                    }
                }
            }

            accountTable.Clear ();
        }

        #endregion

        #region System Events

        public void BackgroundStatusIndHandler (object sender, EventArgs e)
        {
            StatusIndEventArgs ea = (StatusIndEventArgs)e;
            // Use Info_SyncSucceeded rather than Info_NewUnreadEmailMessageInInbox because
            // we want to remove a notification if the server marks a message as read.
            // When the app is in QuickSync mode, BadgeCountAndMessageNotifications will be called when
            // QuickSync is done.  There isn't a need to call it when each account's sync
            // completes.
            if (NcResult.SubKindEnum.Info_SyncSucceeded == ea.Status.SubKind && NcApplication.ExecutionContextEnum.QuickSync != NcApplication.Instance.ExecutionContext) {
                BadgeCountAndMessageNotifications ();
            }
        }

        #endregion

        #region Private Helpers

        protected void SaveNotification (string traceMessage, string key, nint id)
        {
            Log.Info (Log.LOG_LIFECYCLE, "{0}: {1} id is {2}.", traceMessage, key, id);

            var devAccount = McAccount.GetDeviceAccount ();
            if (null != devAccount) {
                McMutables.Set (devAccount.Id, key, key, id.ToString ());
            }
        }

        protected void SaveNotification (string traceMessage, string key, nint [] id)
        {
            Log.Info (Log.LOG_LIFECYCLE, "{0}: {1} id is {2}.", traceMessage, key, id);

            var devAccount = McAccount.GetDeviceAccount ();
            if (null != devAccount) {
                McMutables.Set (devAccount.Id, key, key, String.Join<nint> (",", id));
            }
        }

        #endregion

        #region Test/Debug Helpers

        public static void TestScheduleEmailNotification ()
        {
            var list = NcEmailManager.PriorityInbox (2);
            var thread = list.GetEmailThread (0);
            var message = thread.FirstMessageSpecialCase ();
            var notif = new UILocalNotification () {
                AlertAction = null,
                AlertBody = ((null == message.Subject) ? "(No Subject)" : message.Subject) + ", From " + message.From,
                UserInfo = NSDictionary.FromObjectAndKey (NSNumber.FromInt32 (message.Id), EmailNotificationKey),
                FireDate = NSDate.FromTimeIntervalSinceNow (15),
            };
            notif.SoundName = UILocalNotification.DefaultSoundName;
            UIApplication.SharedApplication.ScheduleLocalNotification (notif);
        }


        public static void TestScheduleCalendarNotification ()
        {
            var e = NcModel.Instance.Db.Table<McEvent> ().Last ();
            var c = McCalendar.QueryById<McCalendar> (e.CalendarId);
            var notif = new UILocalNotification () {
                AlertAction = null,
                AlertBody = c.Subject + Pretty.ReminderTime (new TimeSpan (0, 7, 0)),
                UserInfo = NSDictionary.FromObjectAndKey (NSNumber.FromInt32 (c.Id), EventNotificationKey),
                FireDate = NSDate.FromTimeIntervalSinceNow (15),
            };
            notif.SoundName = UILocalNotification.DefaultSoundName;
            UIApplication.SharedApplication.ScheduleLocalNotification (notif);
        }

        #endregion
    }
}
