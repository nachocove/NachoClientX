//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using Foundation;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoPlatform
{
    public class Notif : IPlatformNotif
    {
        private static volatile Notif instance;
        private static object syncRoot = new Object ();

        private Notif ()
        {
        }

        public static Notif Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new Notif ();
                    }
                }
                return instance;
            }
        }

        // FIXME: 

        /// <summary>
        /// Find the UILocalNotification with the given handle. Returns null if
        /// none exists. This may only be called on the UI thread.
        /// </summary>
//        private UILocalNotification FindNotification (int handle)
//        {
//            foreach (var notification in UIApplication.SharedApplication.ScheduledLocalNotifications) {
//                if (null != notification.UserInfo) {
//                    var value = notification.UserInfo.ValueForKey (NachoClient.iOS.AppDelegate.EventNotificationKey);
//                    if (null != value && value is NSNumber && handle == ((NSNumber)value).NIntValue) {
//                        return notification;
//                    }
//                }
//            }
//            return null;
//        }

        /// <summary>
        /// Create a UILocalNotification object with the given information.  The notification
        /// is not scheduled; this method just creates the object. This may only be called on
        /// the UI thread.
        /// </summary>
//        private UILocalNotification CreateUILocalNotification (McEvent ev)
//        {
//            string title;
//            string body;
//            Pretty.EventNotification (ev, out title, out body);
//            return new UILocalNotification () {
//                AlertAction = null,
//                AlertTitle = title,
//                AlertBody = string.Format("{0}: {1}", title, body),
//                FireDate = ev.ReminderTime.ToNSDate (),
//                TimeZone = null,
//                SoundName = UILocalNotification.DefaultSoundName,
//                UserInfo = NSDictionary.FromObjectAndKey (NSNumber.FromInt32 (ev.Id), NachoClient.iOS.AppDelegate.EventNotificationKey),
//            };
//        }
//
        public void ImmediateNotification (McEvent ev)
        {
//            InvokeOnUIThread.Instance.Invoke (delegate {
//                UIApplication.SharedApplication.PresentLocalNotificationNow (CreateUILocalNotification (ev));
//            });
        }

        public void ScheduleNotification (McEvent ev)
        {
//            InvokeOnUIThread.Instance.Invoke (delegate {
//                DoCancelNotification (ev.Id);
//                UIApplication.SharedApplication.ScheduleLocalNotification (CreateUILocalNotification (ev));
//            });
        }

        public void ScheduleNotifications (List<McEvent> events)
        {
//            InvokeOnUIThread.Instance.Invoke (delegate {
//                UILocalNotification[] iosNotifications = new UILocalNotification[events.Count];
//                int index = 0;
//                foreach (var ev in events) {
//                    iosNotifications [index++] = CreateUILocalNotification (ev);
//                }
//                // This gets rid of all existing local notifications, replacing them with the new ones.
//                UIApplication.SharedApplication.ScheduledLocalNotifications = iosNotifications;
//            });
        }

        /// <summary>
        /// Cancel a notification with the given handle. This must be run
        /// on the UI thread.
        /// </summary>
        private void DoCancelNotification (int handle)
        {
//            var existing = FindNotification (handle);
//            if (null != existing) {
//                UIApplication.SharedApplication.CancelLocalNotification (existing);
//            }
        }

        public void CancelNotification (int handle)
        {
            InvokeOnUIThread.Instance.Invoke (delegate {
                DoCancelNotification (handle);
            });
        }

        public static void DumpNotifications ()
        {
//            InvokeOnUIThread.Instance.Invoke (delegate {
//                UILocalNotification[] notifications = UIApplication.SharedApplication.ScheduledLocalNotifications;
//                Log.Info (Log.LOG_CALENDAR, "LocalNotificationManager: currently scheduled: {0}:", notifications.Length);
//                foreach (var notification in notifications) {
//                    var handleValue = notification.UserInfo.ValueForKey (NachoClient.iOS.AppDelegate.EventNotificationKey);
//                    nint handle = -1;
//                    if (null != handleValue && handleValue is NSNumber) {
//                        handle = ((NSNumber)handleValue).NIntValue;
//                    }
//                    Log.Info (Log.LOG_CALENDAR, "Handle: {0}", handle);
//                }
//            });
        }
    }

    /// <summary>
    /// Conversions between DateTime and NSDate.  This code comes from http://developer.xamarin.com/guides/cross-platform/macios/unified/
    /// </summary>
    public static class DateExtensions
    {
        public static NSDate ToNSDate (this DateTime dateTime)
        {
            if (DateTimeKind.Unspecified == dateTime.Kind) {
                dateTime = DateTime.SpecifyKind (dateTime, DateTimeKind.Utc);
            }
            return (NSDate)dateTime;
        }

        public static DateTime ToDateTime (this NSDate nsDate)
        {
            // NSDate has a wider range than DateTime.  If the given NSDate is outside DateTime's range,
            // return DateTime.MinValue or DateTime.MaxValue.
            double seconds = nsDate.SecondsSinceReferenceDate;
            if (seconds < -63113904000) {
                return DateTime.MinValue;
            }
            if (seconds > 252423993599) {
                return DateTime.MaxValue;
            }
            return (DateTime)nsDate;
        }
    }
}

