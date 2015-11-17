//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using Android.Widget;
using NachoCore;
using Android.Content;

namespace NachoClient.AndroidClient
{
    public static class Util
    {

        public static int GetAccountServiceImageId (McAccount.AccountServiceEnum service)
        {
            int imageId;

            switch (service) {
            case McAccount.AccountServiceEnum.Exchange:
                imageId = Resource.Drawable.avatar_msexchange;
                break;
            case McAccount.AccountServiceEnum.GoogleDefault:
                imageId = Resource.Drawable.avatar_gmail;
                break;
            case McAccount.AccountServiceEnum.GoogleExchange:
                imageId = Resource.Drawable.avatar_googleapps;
                break;
            case McAccount.AccountServiceEnum.HotmailExchange:
                imageId = Resource.Drawable.avatar_hotmail;
                break;
            case McAccount.AccountServiceEnum.IMAP_SMTP:
                imageId = Resource.Drawable.avatar_imap;
                break;
            case McAccount.AccountServiceEnum.OutlookExchange:
                imageId = Resource.Drawable.avatar_outlook;
                break;
            case McAccount.AccountServiceEnum.Office365Exchange:
                imageId = Resource.Drawable.avatar_office365;
                break;
            case McAccount.AccountServiceEnum.Device:
                imageId = Resource.Drawable.avatar_iphone;
                break;
            case McAccount.AccountServiceEnum.iCloud:
                imageId = Resource.Drawable.avatar_icloud;
                break;
            case McAccount.AccountServiceEnum.Yahoo:
                imageId = Resource.Drawable.avatar_yahoo;
                break;
            case McAccount.AccountServiceEnum.Aol:
                imageId = Resource.Drawable.Icon;
                break;
            default:
                imageId = Resource.Drawable.Icon;
                break;
            }
            return imageId;
        }

        #region Date/time conversions and other methods

        public static long MillisecondsSinceEpoch (this DateTime dateTime)
        {
            return (dateTime.ToUniversalTime () - new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Ticks / TimeSpan.TicksPerMillisecond;
        }

        public static Java.Util.Date ToJavaDate (this DateTime dateTime)
        {
            return new Java.Util.Date (dateTime.MillisecondsSinceEpoch ());
        }

        public static DateTime ToDateTime (this Java.Util.Date javaDate)
        {
            return new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc) + TimeSpan.FromMilliseconds (javaDate.Time);
        }

        #endregion

        public static int ColorResourceForEmail (string email)
        {
            McEmailAddress address;
            if (McEmailAddress.Get (NcApplication.Instance.Account.Id, email, out address)) {
                return Bind.ColorForUser (address.ColorIndex);
            } else {
                return Resource.Drawable.UserColor0;
            }
        }

        public static void SendEmail (Context context, McContact contact, string alternateEmailAddress)
        {
            if (null != alternateEmailAddress) {
                var intent = MessageComposeActivity.NewMessageIntent (context, alternateEmailAddress);
                context.StartActivity (intent);
                return;
            }
            if (0 == contact.EmailAddresses.Count) {
                NcAlertView.ShowMessage (context, "Cannot Send Message", "This contact does not have an email address.");
                return;
            }
            var emailAddress = contact.GetDefaultOrSingleEmailAddress ();
            if (null == emailAddress) {
                NcAlertView.ShowMessage (context, "Contact has multiple addresses", "Please send an email address to use.");
                return;
            }
            context.StartActivity (MessageComposeActivity.NewMessageIntent (context, emailAddress));
        }

        public static void CallNumber(Context context, McContact contact, string alternatePhoneNumber)
        {
            if (null != alternatePhoneNumber) {
                var number = Android.Net.Uri.Parse (String.Format ("tel:{0}", alternatePhoneNumber));
                context.StartActivity(new Intent(Intent.ActionDial, number));
                return;
            }
            if (0 == contact.PhoneNumbers.Count) {
                NcAlertView.ShowMessage (context, "Cannot Call Contact", "This contact does not have a phone number.");
                return;
            }
            var  phoneNumber = contact.GetDefaultOrSinglePhoneNumber ();
            if (null == phoneNumber) {
                NcAlertView.ShowMessage (context, "Contact has multiple numbers", "Please select a number to call.");
                return;
            }
            var phoneUri = Android.Net.Uri.Parse (String.Format ("tel:{0}", alternatePhoneNumber));
            context.StartActivity(new Intent(Intent.ActionDial, phoneUri));
        }
    }


    /// <summary>
    /// Use JavaObjectWrapper to store an object with a C# type in a place that
    /// only accepts Java objects derived from Java.Lang.Object.  For example,
    /// this class can be used with View.SetTag() and View.GetTag().
    /// </summary>
    public class JavaObjectWrapper<T> : Java.Lang.Object
    {
        public T Item { get; set; }
    }
}

