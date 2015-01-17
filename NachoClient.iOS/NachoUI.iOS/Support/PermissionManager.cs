﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

using MonoTouch.Foundation;
using MonoTouch.ObjCRuntime;
using MonoTouch.UIKit;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    //
    // Ask for permission code for notifications, calendar and contacts.
    // TODO: The UIImagePickerController asks for permissions.  We need
    // to our own code to ask for permissions to access the camera roll.
    //
    // The ask is two-steps. The first step is for Nacho Mail to ask on
    // behalf of itself instead of calling the iOS permission routines.
    // The iOS permissions cannot be undone by the app so before we let
    // the user say 'no' to the system, we ask first.  if the user says
    // no to us, then we do not ask the system.
    //
    // The second step is to ask the system for permission.  (We do not
    // get a callback for notifications yet.) If the user says no, then
    // we record that fact that the user said yes to Nacho Mail but not
    // to iOS.
    //
    // We can use these variables to prompt the user to give permission
    // after they've said no. If the first step got a no, we can prompt
    // the user to say yet. If the second step got a no, we can explain
    // to the user to use the system settings to reset the permission.

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
            var module = Module_Notifications;
            var accountId = LoginHelpers.GetCurrentAccountId ();

            if (McMutables.GetOrCreateBool (accountId, module, Key_AskedUserForPermission, false)) {
                return;
            }
            McMutables.SetBool (accountId, module, Key_AskedUserForPermission, true);

            var title = "Nacho Mail would like to send you push notifications.";
            var body = "This allows Nacho Mail to tell you when you have new mail or an upcoming meeting.";

            var alert = new UIAlertView (title, body, null, "OK", new string[] { "No" });
            alert.Clicked += (s, b) => {
                if (0 == b.ButtonIndex) {
                    McMutables.SetBool (accountId, module, Key_AskedUserForPermission, true);
                    Log.Info (Log.LOG_UI, "{0}: {1} {2}", module, Key_AskedUserForPermission, "yes");
                    var application = UIApplication.SharedApplication;
                    if (application.RespondsToSelector (new Selector ("registerUserNotificationSettings:"))) {
                        var settings = UIUserNotificationSettings.GetSettingsForTypes (UIUserNotificationType.Alert | UIUserNotificationType.Badge | UIUserNotificationType.Sound, new NSSet ());
                        application.RegisterUserNotificationSettings (settings);
                    }
                } else {
                    McMutables.SetBool (accountId, module, Key_AskedUserForPermission, true);
                    Log.Info (Log.LOG_UI, "{0}: {1} {2}", module, Key_AskedUserForPermission, "no");
                }
            };
            alert.Show ();
        }

        // Calendar
        public static void DealWithCalendarPermission ()
        {
            // FIXME
            return;

//            var module = Module_Calendar;
//            var accountId = LoginHelpers.GetCurrentAccountId ();
//
//            if (McMutables.GetOrCreateBool (accountId, module, Key_AskedUserForPermission, false)) {
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
//                        McMutables.SetBool (accountId, module, Key_AskedUserForPermission, true);
//                        McMutables.SetBool (accountId, module, Key_UserGrantedUsPermission, granted);
//                        Log.Info(Log.LOG_UI, "{0}: {1} {2}", module, Key_AskedUserForPermission, "yes");
//                        Log.Info(Log.LOG_UI, "{0}: {1} {2}", module, Key_UserGrantedUsPermission, granted);
//                        if (granted) {
//                            NcDeviceCalendars.Run ();
//                        }
//                    });
//                } else {
//                    McMutables.SetBool (accountId, module, Key_AskedUserForPermission, true);
//                    Log.Info(Log.LOG_UI, "{0}: {1} {2}", module, Key_AskedUserForPermission, "no");
//                }
//            };
//            alert.Show ();
        }

        // Contacts
        public static void DealWithContactsPermission ()
        {
            var module = Module_Contacts;
            var accountId = LoginHelpers.GetCurrentAccountId ();

            if (McMutables.GetOrCreateBool (accountId, module, Key_AskedUserForPermission, false)) {
                return;
            }

            var title = "Nacho Mail would like to access your Address Book";
            var body = "This allows you to choose contacts from your address book in addition to your connected email accounts.";

            var alert = new UIAlertView (title, body, null, "OK", new string[] { "Cancel" });
            alert.Clicked += (s, b) => {
                if (0 == b.ButtonIndex) {
                    NachoPlatform.Contacts.Instance.AskForPermission ((bool granted) => {
                        McMutables.SetBool (accountId, module, Key_AskedUserForPermission, true);
                        McMutables.SetBool (accountId, module, Key_UserGrantedUsPermission, granted);
                        Log.Info (Log.LOG_UI, "{0}: {1} {2}", module, Key_AskedUserForPermission, "yes");
                        Log.Info (Log.LOG_UI, "{0}: {1} {2}", module, Key_UserGrantedUsPermission, granted);
                        if (granted) {
                            NcDeviceContacts.Run ();
                        }
                    });
                } else {
                    McMutables.SetBool (accountId, module, Key_AskedUserForPermission, true);
                    Log.Info (Log.LOG_UI, "{0}: {1} {2}", module, Key_AskedUserForPermission, "no");
                }
            };
            alert.Show ();
        }
    }
}
