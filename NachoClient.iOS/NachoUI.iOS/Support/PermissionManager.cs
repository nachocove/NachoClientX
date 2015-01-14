//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

using MonoTouch.Foundation;
using MonoTouch.ObjCRuntime;
using MonoTouch.UIKit;

using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{

    public class PermissionManager
    {
        const string Module_Calendar = "DeviceCalendar";
        const string Module_Contacts = "DeviceContacts";
        const string Module_Notifications = "DeviceNotifications";

        const string Key_AskedUserForPermission = "AskedUserForPermission";
        const string Key_UserGrantedUsPermission = "UserGrantedUsPermission";


        public PermissionManager ()
        {
        }

        // Notifications -- no callback?
        public static void DealWithNotificationPermission ()
        {
            var accountId = LoginHelpers.GetCurrentAccountId ();

            if (McMutables.GetOrCreateBool (accountId, Module_Notifications, Key_AskedUserForPermission, false)) {
                return;
            }
            McMutables.SetBool (accountId, Module_Notifications, Key_AskedUserForPermission, true);

            var title = "Nacho Mail would like to send you push notifications.";
            var body = "This allows Nacho Mail to tell you when you have new mail or an upcoming meeting.";

            var alert = new UIAlertView (title, body, null, "OK", new string[] { "No" });
            alert.Clicked += (s, b) => {
                if (0 == b.ButtonIndex) {
                    McMutables.SetBool (accountId, Module_Notifications, Key_AskedUserForPermission, true);
                    var application = UIApplication.SharedApplication;
                    if (application.RespondsToSelector (new Selector ("registerUserNotificationSettings:"))) {
                        var settings = UIUserNotificationSettings.GetSettingsForTypes (UIUserNotificationType.Alert | UIUserNotificationType.Badge | UIUserNotificationType.Sound, new NSSet ());
                        application.RegisterUserNotificationSettings (settings);
                    }
                } else {
                    McMutables.SetBool (accountId, Module_Notifications, Key_AskedUserForPermission, true);
                }
            };
            alert.Show ();
        }

        // Calendar
        public static void DealWithCalendarPermission ()
        {
            // FIXME
            return;

//            var accountId = LoginHelpers.GetCurrentAccountId ();
//
//            if (McMutables.GetOrCreateBool (accountId, Module_Calendar, Key_AskedUserForPermission, false)) {
//                return;
//            }
//
//            var title = "Nacho Mail would like to access your Calendar";
//            var body = "This allows Nacho Mail to show events from your calendar in the Nacho Mail calendar.";
//
//            var alert = new UIAlertView (title, body, null, "OK", new string[] { "No" });
//            alert.Clicked += (s, b) => {
//                if (0 == b.ButtonIndex) {
//                    NachoPlatform.Calendars.Instance.AskForPermission ((bool granted) => {
//                        McMutables.SetBool (accountId, Module_Calendar, Key_AskedUserForPermission, true);
//                        McMutables.SetBool (accountId, Module_Calendar, Key_UserGrantedUsPermission, granted);
//                        if (granted) {
//                            NcDeviceCalendars.Run ();
//                        }
//                    });
//                } else {
//                    McMutables.SetBool (accountId, Module_Calendar, Key_AskedUserForPermission, true);
//                }
//            };
//            alert.Show ();
        }

        // Contacts
        public static void DealWithContactsPermission ()
        {
            var accountId = LoginHelpers.GetCurrentAccountId ();

            if (McMutables.GetOrCreateBool (accountId, Module_Contacts, Key_AskedUserForPermission, false)) {
                return;
            }

            var title = "Nacho Mail would like to access your Address Book";
            var body = "This allows you to choose contacts from your address book in addition to your connected email accounts.";

            var alert = new UIAlertView (title, body, null, "OK", new string[] { "Cancel" });
            alert.Clicked += (s, b) => {
                if (0 == b.ButtonIndex) {
                    NachoPlatform.Contacts.Instance.AskForPermission ((bool granted) => {
                        McMutables.SetBool (accountId, Module_Contacts, Key_AskedUserForPermission, true);
                        McMutables.SetBool (accountId, Module_Contacts, Key_UserGrantedUsPermission, granted);
                        if (granted) {
                            NcDeviceContacts.Run ();
                        }
                    });
                } else {
                    McMutables.SetBool (accountId, Module_Contacts, Key_AskedUserForPermission, true);
                }
            };
            alert.Show ();
        }
    }
}
