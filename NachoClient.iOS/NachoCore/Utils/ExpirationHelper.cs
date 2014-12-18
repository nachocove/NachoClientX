//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace NachoCore.Utils
{
    public class ExpirationHelper
    {
        public ExpirationHelper ()
        {
        }

        public static bool AccountIsExpiring (McAccount whatAccount)
        {
            if (whatAccount.DaysUntilPasswordExpires == -1) {
                return false;
            }
            return true;
        }

        public static bool ProvidedFixUrl (McAccount whatAccount)
        {
            if (string.IsNullOrEmpty (whatAccount.PasswordExpirationUrl)) {
                return false;
            }
            return true;
        }

        public static bool HasAlertedToday (McAccount whatAccount)
        {
            if (GetAlertDate(whatAccount) == DateTime.Today) {
                return true;
            }
            return false;
        }

        public static void UserClickedFix (McAccount whatAccount)
        {
            UIApplication.SharedApplication.OpenUrl (new NSUrl(whatAccount.PasswordExpirationUrl));
            McMutables.SetBool (whatAccount.Id, "PASSEXPIRE", "UserClickedFix", true);
        }

        public static bool HasUserClickedFix (McAccount whatAcount)
        {
            return McMutables.GetOrCreateBool (whatAcount.Id, "PASSEXPIRE", "UserClickedFix", false);
        }

        public static void RemoveUserClickedFix (McAccount whatAcount)
        {
            McMutables.Delete (whatAcount.Id, "PASSEXPIRE", "UserClickedFix");
        }

        public static DateTime GetAlertDate(McAccount whatAccount)
        {
            return DateTime.Parse(McMutables.GetOrCreate (whatAccount.Id, "PASSEXPIRE", "AlertDate", DateTime.Today.AddDays (-1).ToString ()));
        }

        public static void SetAlertDate (McAccount whatAccount)
        {
            McMutables.Set (whatAccount.Id, "PASSEXPIRE", "AlertDate", DateTime.Today.ToString ());
        }

        public static void RemoveAlertDate (McAccount whatAccount)
        {
            McMutables.Delete (whatAccount.Id, "PASSEXPIRE", "AlertDate");
        }

    }
}

