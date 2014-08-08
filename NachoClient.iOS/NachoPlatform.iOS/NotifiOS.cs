//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.Utils;

namespace NachoPlatform
{
    public class Notif : IPlatformNotif
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

        public static NSString NoteKey = new NSString ("NotifiOS.handle");

        private UILocalNotification FindNotif (int handle)
        {
            foreach (var notif in UIApplication.SharedApplication.ScheduledLocalNotifications) {
                if (null != notif.UserInfo) {
                    var value = notif.UserInfo.ValueForKey (NoteKey);
                    if (null != value && value is NSNumber && handle == ((NSNumber)value).IntValue) {
                        return notif;
                    }
                }
            }
            return null;
        }

        public int BadgeNumber { 
            get { 
                return UIApplication.SharedApplication.ApplicationIconBadgeNumber;
            }
            set {
                UIApplication.SharedApplication.ApplicationIconBadgeNumber = value;
            }
        }

        public void ScheduleNotif (int handle, DateTime when, string message, bool sound)
        {
            InvokeOnUIThread.Instance.Invoke (delegate {
                var notif = FindNotif (handle);
                NcAssert.True (null == notif, string.Format ("ScheduleNotif: attempt schedule another notif with same handle {0}.", handle));
                notif = new UILocalNotification () {
                    AlertAction = "Nacho Mail",
                    AlertBody = message,
                    UserInfo = NSDictionary.FromObjectAndKey (NSNumber.FromInt32 (handle), NoteKey),
                    SoundName = UILocalNotification.DefaultSoundName,
                    FireDate = when.ToNSDate(),
                    TimeZone = NSTimeZone.FromAbbreviation ("UTC"),
                };
                if (sound) {
                    notif.SoundName = UILocalNotification.DefaultSoundName;
                }
                UIApplication.SharedApplication.ScheduleLocalNotification (notif);
            });
        }

        public void CancelNotif (int handle)
        {
            InvokeOnUIThread.Instance.Invoke (delegate {
                var notif = FindNotif (handle);
                if (null != notif) {
                    UIApplication.SharedApplication.CancelLocalNotification (notif);
                }
            });
        }
    }

    public static class DateExtensions
    {
        public static NSDate ToNSDate(this DateTime dateTime)
        {
            return NSDate.FromTimeIntervalSinceReferenceDate((dateTime-(new DateTime(2001,1,1,0,0,0))).TotalSeconds);
        }

        public static DateTime ToDateTime(this NSDate nsDate)
        {
            return (new DateTime(2001,1,1,0,0,0)).AddSeconds(nsDate.SecondsSinceReferenceDate);
        }
    }

    public static class UILocalNotificationExtensions
    {
        public static int Handle (this UILocalNotification notif)
        {
            return ((NSNumber)notif.UserInfo.ObjectForKey (Notif.NoteKey)).IntValue;
        }
    }
}

