//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using NachoCore.Model;
using NachoPlatform;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "RemoveAccountActivity")]            
    public class RemoveAccountActivity : Activity
    {
        private const string EXTRA_ACCOUNT_ID = "com.nachocove.nachomail.EXTRA_ACCOUNT_ID";

        public static Intent RemoveAccountIntent (Context context, McAccount account)
        {
            var intent = new Intent (context, typeof(RemoveAccountActivity));
            intent.SetAction (Intent.ActionDelete);
            intent.PutExtra (EXTRA_ACCOUNT_ID, account.Id);
            return intent;
        }

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.RemoveAccountActivity);
        }

        protected override void OnStart ()
        {
            base.OnStart ();

            var accountId = Intent.GetIntExtra (EXTRA_ACCOUNT_ID, -1);
            NcAssert.False (-1 == accountId);

            Action action = () => {
                NcAccountHandler.Instance.RemoveAccount (accountId);
                InvokeOnUIThread.Instance.Invoke (() => {
                    var spinner = FindViewById(Resource.Id.spinner);
                    spinner.Visibility = ViewStates.Gone;
                    // go back to main screen
                    NcUIRedirector.Instance.GoBackToMainScreen ();  
                });
            };
            NcTask.Run (action, "RemoveAccount");
        }

        public override void OnBackPressed ()
        {
            Toast toast = Toast.MakeText (ApplicationContext, "Please wait", ToastLength.Short);
            toast.Show ();
        }
    }
}

