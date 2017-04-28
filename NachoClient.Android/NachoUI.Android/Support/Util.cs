//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using Android.Content;
using Android.Content.PM;
using Android.Provider;
using Android.Support.V4.Content;
using Android.Widget;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using Android.App;
using Android.Graphics.Drawables;

namespace NachoClient.AndroidClient
{
    public static class Util
    {

        public static Drawable GetSizedAndRoundedAccountImage (Context context, McAccount account, int size)
        {
            var bitmap = (GetAccountImage (context, account) as BitmapDrawable).Bitmap;
            using (bitmap = Android.Graphics.Bitmap.CreateScaledBitmap (bitmap, size, size, true)) {
                var roundedBitmap = Android.Support.V4.Graphics.Drawable.RoundedBitmapDrawableFactory.Create (context.Resources, bitmap);
                roundedBitmap.CornerRadius = size / 2;
                return roundedBitmap;
            }
        }

        public static Drawable GetAccountImage (Context context, McAccount account)
        {
            if (account.DisplayPortraitId == 0) {
                var resource = GetAccountServiceImageId (account.AccountService);
                return context.GetDrawable (resource);
            }
            var portrait = McPortrait.QueryById<McPortrait> (account.DisplayPortraitId);
            return Drawable.CreateFromPath (portrait.GetFilePath ());
        }

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
                imageId = Resource.Drawable.avatar_aol;
                break;
            case McAccount.AccountServiceEnum.SalesForce:
                imageId = Resource.Drawable.avatar_salesforce;
                break;
            default:
                imageId = Resource.Drawable.avatar_unified;
                break;
            }
            return imageId;
        }

        #region Date/time conversions and other methods

        private static readonly DateTime UnixEpoch = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long MillisecondsSinceEpoch (this DateTime dateTime)
        {
            // DateTime.ToUniversalTime() assumes than unspecified times are local.  But we want unspecified
            // times to be UTC, since the database code loads DateTimes as unspecified and we don't have a
            // good place to change them all to UTC.
            if (DateTimeKind.Unspecified == dateTime.Kind) {
                dateTime = DateTime.SpecifyKind (dateTime, DateTimeKind.Utc);
            }
            return (long)((dateTime.ToUniversalTime () - UnixEpoch).TotalMilliseconds);
        }

        public static Java.Util.Date ToJavaDate (this DateTime dateTime)
        {
            return new Java.Util.Date (dateTime.MillisecondsSinceEpoch ());
        }

        public static DateTime ToDateTime (this Java.Util.Date javaDate)
        {
            return javaDate.Time.JavaMsToDateTime ();
        }

        public static DateTime JavaMsToDateTime (this long javaMs)
        {
            return UnixEpoch + TimeSpan.FromMilliseconds (javaMs);
        }

        #endregion

        public static int ColorResourceForEmail (int accountId, string email)
        {
            McEmailAddress address;
            if (McEmailAddress.Get (accountId, email, out address)) {
                return Bind.ColorForUser (address.ColorIndex);
            } else {
                return Resource.Drawable.UserColor0;
            }
        }

        public static void SendEmail (Context context, int accountId, McContact contact, string alternateEmailAddress)
        {
            if (!String.IsNullOrEmpty (alternateEmailAddress)) {
                var intent = MessageComposeActivity.NewMessageIntent (context, accountId, alternateEmailAddress);
                context.StartActivity (intent);
                return;
            }
            if (0 == contact.EmailAddresses.Count) {
                NcAlertView.ShowMessage (context, "Cannot Send Message", "This contact does not have an email address.");
                return;
            }
            var emailAddress = contact.GetDefaultOrSingleEmailAddress ();
            if (null == emailAddress) {
                NcAlertView.ShowMessage (context, "Contact has multiple addresses", "Please select an email address to use.");
                return;
            }
            context.StartActivity (MessageComposeActivity.NewMessageIntent (context, accountId, emailAddress));
        }

        public static void CallNumber (Context context, McContact contact, string alternatePhoneNumber)
        {
            try {
                if (null != alternatePhoneNumber) {
                    var number = Android.Net.Uri.Parse (String.Format ("tel:{0}", alternatePhoneNumber));
                    context.StartActivity (new Intent (Intent.ActionDial, number));
                    return;
                }
                if (0 == contact.PhoneNumbers.Count) {
                    NcAlertView.ShowMessage (context, "Cannot Call Contact", "This contact does not have a phone number.");
                    return;
                }
                var phoneNumber = contact.GetDefaultOrSinglePhoneNumber ();
                if (null == phoneNumber) {
                    NcAlertView.ShowMessage (context, "Contact has multiple numbers", "Please select a number to call.");
                    return;
                }
                var phoneUri = Android.Net.Uri.Parse (String.Format ("tel:{0}", phoneNumber));
                context.StartActivity (new Intent (Intent.ActionDial, phoneUri));
            } catch (ActivityNotFoundException) {
                NcAlertView.ShowMessage (context, "Cannot Call", "This device does not support making phone calls.");
            }
        }

        public static bool CanTakePhoto (Context context)
        {
            Intent intent = new Intent (MediaStore.ActionImageCapture);
            IList<Android.Content.PM.ResolveInfo> activities = context.PackageManager.QueryIntentActivities (intent, Android.Content.PM.PackageInfoFlags.MatchDefaultOnly);
            return activities != null && activities.Count > 0;
        }

        public static Android.Net.Uri TakePhoto (Fragment fragment, int requestCode)
        {
            var dir = new Java.IO.File (Android.OS.Environment.GetExternalStoragePublicDirectory (Android.OS.Environment.DirectoryPictures), "NachoAttachmentCamera");
            if (!dir.Exists ()) {
                dir.Mkdirs ();
            }
            var intent = new Intent (MediaStore.ActionImageCapture);
            var file = new Java.IO.File (dir, String.Format ("photo_{0}.jpg", Guid.NewGuid ()));
            var outputUri = Android.Net.Uri.FromFile (file);
            intent.PutExtra (MediaStore.ExtraOutput, outputUri);
            fragment.StartActivityForResult (intent, requestCode);
            return outputUri;
        }

        public static List<int> accountColors = null;

        static Dictionary<int, int> AccountColorIndexCache = new Dictionary<int, int> ();

        public static int ColorForAccount (int accountId)
        {
            if (accountColors == null) {
                accountColors = new List<int> (McAccount.AccountColors.Length / 3);
                for (int i = 0; i < McAccount.AccountColors.Length / 3; ++i) {
                    accountColors.Add (unchecked((int)(0xFF000000 | (McAccount.AccountColors [i,0] << 16) | (McAccount.AccountColors [i,1] << 8) | McAccount.AccountColors [i,2])));
                }
            }
            if (!AccountColorIndexCache.ContainsKey (accountId)) {
                var account = McAccount.QueryById<McAccount> (accountId);
                AccountColorIndexCache [accountId] = account.ColorIndex;
            }
            var index = AccountColorIndexCache [accountId];
            return accountColors [index];
        }

        public static Drawable PortraitToDrawable (int portraitId)
        {
            if (portraitId == 0) {
                return null;
            }
            var portrait = McPortrait.QueryById<McPortrait> (portraitId);
            var drawable = Drawable.CreateFromPath (portrait.GetFilePath ());
            return drawable;
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

    public static class AlertHelper
    {

        private class AlertCancelListener : Java.Lang.Object, IDialogInterfaceOnCancelListener
        {

        	Action CancelAction;

        	public AlertCancelListener (Action cancelAction)
        	{
        		CancelAction = cancelAction;
        	}

            public void OnCancel (IDialogInterface dialog)
            {
                CancelAction ();
            }
        }

        public static void ShowWithCancelAction (this AlertDialog.Builder builder, Action cancelAction)
        {
            var listener = new AlertCancelListener (cancelAction);
            builder.SetOnCancelListener (listener);
            builder.Show ();
        }
    }
}

