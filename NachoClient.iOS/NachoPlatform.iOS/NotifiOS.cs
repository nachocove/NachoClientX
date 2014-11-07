//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.Utils;

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
            
        public UILocalNotification FindNotif (int handle)
        {
            foreach (var notif in UIApplication.SharedApplication.ScheduledLocalNotifications) {
                if (null != notif.UserInfo) {
                    var value = notif.UserInfo.ValueForKey (NachoClient.iOS.AppDelegate.EventNotificationKey);
                    if (null != value && value is NSNumber && handle == ((NSNumber)value).IntValue) {
                        return notif;
                    }
                }
            }
            return null;
        }

        public void ScheduleNotif (int handle, DateTime when, string message)
        {
            InvokeOnUIThread.Instance.Invoke (delegate {
                var notif = FindNotif (handle);
                NcAssert.True (null == notif, string.Format ("ScheduleNotif: attempt schedule another notif with same handle {0}.", handle));
                notif = new UILocalNotification () {
                    AlertAction = null,
                    AlertBody = message,
                    UserInfo = NSDictionary.FromObjectAndKey (NSNumber.FromInt32 (handle), NachoClient.iOS.AppDelegate.EventNotificationKey),
                    SoundName = UILocalNotification.DefaultSoundName,
                    FireDate = when.ToNSDate (),
                    //Commented out timezone because:

                    //Apple Doc: The date specified in fireDate is interpreted according to the value of this property. 
                    //If you specify nil (the default), the fire date is interpreted as an absolute GMT time, 
                    //which is suitable for cases such as countdown timers. If you assign a valid NSTimeZone object to 
                    //this property, the fire date is interpreted as a wall-clock time that is automatically adjusted 
                    //when there are changes in time zones; an example suitable for this case is an an alarm clock.

                    //TimeZone = NSTimeZone.FromAbbreviation ("UTC"),
                };
                UIApplication.SharedApplication.ScheduleLocalNotification (notif);
            });
        }


        public void CancelNotif (int handle)
        {
            CancelNotif (new List<int> () { handle });
        }

        public void CancelNotif (List<int> handles)
        {
            InvokeOnUIThread.Instance.Invoke (delegate {
                // TODO: O(N**2).
                foreach (var handle in handles) {
                    var notif = FindNotif (handle);
                    if (null != notif) {
                        UIApplication.SharedApplication.CancelLocalNotification (notif);
                    }
                }
            });
        }
    }

    public static class DateExtensions
    {
        public static NSDate ToNSDate (this DateTime dateTime)
        {
            return NSDate.FromTimeIntervalSinceReferenceDate ((dateTime - (new DateTime (2001, 1, 1, 0, 0, 0))).TotalSeconds);
        }

        public static NSDate ShiftToUTC (this NSDate nsDate, NSTimeZone nsTimeZone)
        {
            var deltaSecs = nsTimeZone.SecondsFromGMT (nsDate);
            var origSecs = nsDate.SecondsSinceReferenceDate;
            return NSDate.FromTimeIntervalSinceReferenceDate (origSecs + deltaSecs);
        }

        public static DateTime ToDateTime (this NSDate nsDate)
        {
            return (new DateTime (2001, 1, 1, 0, 0, 0)).AddSeconds (nsDate.SecondsSinceReferenceDate);
        }
    }
}

