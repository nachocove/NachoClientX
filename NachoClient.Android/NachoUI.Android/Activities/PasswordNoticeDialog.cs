//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.AndroidClient
{
    public class PasswordNoticeDialog : NcDialogFragment
    {

        #region Creating a Dialog

        McAccount Account;
        DateTime Expiry;
        string RectifyUrl;

        public PasswordNoticeDialog (McAccount account, DateTime expiry, string rectifyUrl) : base ()
        {
            Account = account;
            Expiry = expiry;
            RectifyUrl = rectifyUrl;
        }

        #endregion

        #region Dialog Lifecycle

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            builder.SetTitle (Resource.String.password_update_title);
            builder.SetMessage (String.Format (GetString (Resource.String.password_notice_message_format), Pretty.ReminderDate (Expiry)));
            builder.SetNeutralButton (Resource.String.password_notice_close, CloseClicked);
            builder.SetNegativeButton (Resource.String.password_notice_clear, ClearClicked);
            if (!String.IsNullOrEmpty (RectifyUrl)) {
                builder.SetPositiveButton (Resource.String.password_notice_rectify, RectifyClicked);
            }
            return builder.Create ();
        }

        #endregion

        #region User Actions

        void ClearClicked (object sender, DialogClickEventArgs e)
        {
            LoginHelpers.ClearPasswordExpiration (Account.Id);
        }

        void CloseClicked (object sender, DialogClickEventArgs e)
        {
        }

        void RectifyClicked (object sender, DialogClickEventArgs e)
        {
            var intent = new Intent (Intent.ActionView, Android.Net.Uri.Parse (RectifyUrl));
            StartActivity (intent);
        }

        #endregion

    }
}
