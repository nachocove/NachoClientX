//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

using Foundation;
using ObjCRuntime;
using UIKit;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using PM = NachoCore.Utils.PermissionManager;

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

        public PermissionManager ()
        {
        }

        // Notifications -- no callback?
        public static void DealWithNotificationPermission ()
        {
            var module = PM.Module_Notifications;
            var accountId = McAccount.GetDeviceAccount ().Id;

            if (McMutables.GetOrCreateBool (accountId, module, PM.Key_AskedUserForPermission, false)) {
                return;
            }
            McMutables.SetBool (accountId, module, PM.Key_AskedUserForPermission, true);

            var title = "Nacho Mail would like to send you push notifications.";
            var body = "This allows Nacho Mail to tell you when you have new mail or an upcoming meeting.";

            var alert = new UIAlertView (title, body, null, null, new string[] { "Don't Allow", "OK" });
            alert.Clicked += (s, b) => {
                if ((alert.FirstOtherButtonIndex + 1) == b.ButtonIndex) {
                    McMutables.SetBool (accountId, module, PM.Key_AskedUserForPermission, true);
                    Log.Info (Log.LOG_UI, "{0}: {1} {2}", module, PM.Key_AskedUserForPermission, "yes");
                    var application = UIApplication.SharedApplication;
                    if (application.RespondsToSelector (new Selector ("registerUserNotificationSettings:"))) {
                        var settings = UIUserNotificationSettings.GetSettingsForTypes (UIUserNotificationType.Alert | UIUserNotificationType.Badge, new NSSet ());
                        application.RegisterUserNotificationSettings (settings);
                    }
                } else {
                    McMutables.SetBool (accountId, module, PM.Key_AskedUserForPermission, true);
                    Log.Info (Log.LOG_UI, "{0}: {1} {2}", module, PM.Key_AskedUserForPermission, "no");
                }
            };
            alert.Show ();
        }

        // Calendar
        public static void DealWithCalendarPermission ()
        {
            var module = PM.Module_Calendar;
            var accountId = McAccount.GetDeviceAccount ().Id;

            // If we have already asked, then don't ask again.  TODO:  Setting to enabled access
            if (McMutables.GetOrCreateBool (accountId, module, PM.Key_AskedUserForPermission, false)) {
                return;
            }

            // Has the system already allowed or denied Nacho Mail?
            if (!NachoPlatform.Calendars.Instance.ShouldWeBotherToAsk ()) {
                McMutables.SetBool (accountId, module, PM.Key_AskedUserForPermission, true);
                Log.Info (Log.LOG_UI, "{0}: {1} {2}", module, PM.Key_AskedUserForPermission, "do not bother");
                return;
            }

            McMutables.SetBool (accountId, module, PM.Key_AskedUserForPermission, true);

            NachoPlatform.Calendars.Instance.AskForPermission ((bool granted) => {
                McMutables.SetBool (accountId, module, PM.Key_UserGrantedUsPermission, granted);
                Log.Info (Log.LOG_UI, "{0}: {1} {2}", module, PM.Key_AskedUserForPermission, "yes");
                Log.Info (Log.LOG_UI, "{0}: {1} {2}", module, PM.Key_UserGrantedUsPermission, granted);
                if (granted) {
                    // FIXME: Shouldn't this be BackEnd.Instance.SyncCmd()?
                    BackEnd.Instance.Start (accountId);
                }
            });
        }

        // Contacts
        public static void DealWithContactsPermission ()
        {
            var module = PM.Module_Contacts;
            var accountId = McAccount.GetDeviceAccount ().Id;

            // If we have already asked, then don't ask again.  TODO:  Setting to enabled access
            if (McMutables.GetOrCreateBool (accountId, module, PM.Key_AskedUserForPermission, false)) {
                return;
            }

            // Has the system already allowed or denied Nacho Mail?
            if (!NachoPlatform.Contacts.Instance.ShouldWeBotherToAsk ()) {
                McMutables.SetBool (accountId, module, PM.Key_AskedUserForPermission, true);
                Log.Info (Log.LOG_UI, "{0}: {1} {2}", module, PM.Key_AskedUserForPermission, "do not bother");
                return;
            }

            McMutables.SetBool (accountId, module, PM.Key_AskedUserForPermission, true);

            NachoPlatform.Contacts.Instance.AskForPermission ((bool granted) => {
                McMutables.SetBool (accountId, module, PM.Key_UserGrantedUsPermission, granted);
                Log.Info (Log.LOG_UI, "{0}: {1} {2}", module, PM.Key_AskedUserForPermission, "yes");
                Log.Info (Log.LOG_UI, "{0}: {1} {2}", module, PM.Key_UserGrantedUsPermission, granted);
                if (granted) {
                    // Trigger a Sync.
                    // FIXME: Shouldn't this be BackEnd.Instance.SyncCmd()?
                    BackEnd.Instance.Start (accountId);
                }
            });
        }

    }
}
