//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{

    public class ExchangeEnableFragment : Fragment
    {

        public interface Listener
        {
            void OnExchangeEnabled ();
        }

        public McAccount.AccountServiceEnum Service;
        WeakReference<Listener> WeakListener;

        public ExchangeEnableFragment ()
        {
            WeakListener = new WeakReference<Listener> (null);
        }

        #region Subviews

        TextView CodeField;
        TextView MessageLabel;
        ImageView AvatarView;
        Button RequestCodeButton;
        Button EnterCodeButton;
        Button SubmitButton;

        void FindSubviews (View view)
        {
            CodeField = view.FindViewById (Resource.Id.code_field) as TextView;
            MessageLabel = view.FindViewById (Resource.Id.message) as TextView;
            AvatarView = view.FindViewById (Resource.Id.avatar) as ImageView;
            RequestCodeButton = view.FindViewById (Resource.Id.request_code) as Button;
            EnterCodeButton = view.FindViewById (Resource.Id.enter_code) as Button;
            SubmitButton = view.FindViewById (Resource.Id.submit) as Button;

            RequestCodeButton.Click += RequestCode;
            EnterCodeButton.Click += EnterCode;
            SubmitButton.Click += SubmitCode;
        }

        void ClearSubviews ()
		{
			RequestCodeButton.Click -= RequestCode;
			EnterCodeButton.Click -= EnterCode;
			SubmitButton.Click -= SubmitCode;
            
            CodeField = null;
            MessageLabel = null;
            AvatarView = null;
            RequestCodeButton = null;
            EnterCodeButton = null;
            SubmitButton = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Android.OS.Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ExchangeEnableFragment, container, false);
            FindSubviews (view);
            AvatarView.SetImageResource (Util.GetAccountServiceImageId (Service));
            return view;
        }

        public override void OnAttach (Android.Content.Context context)
        {
            base.OnAttach (context);
            if (context is Listener){
                WeakListener.SetTarget ((context as Listener));
            }
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        #region User Actions

        void RequestCode (object sender, EventArgs e)
        {
            RequestCode ();
        }

        void EnterCode(object sender, EventArgs e)
        {
            EnterCode ();
        }

        void SubmitCode (object sender, EventArgs e)
        {
            SubmitCode ();
        }

        #endregion

        #region Private Helpers

        void RequestCode ()
        {
            var account = NcApplication.Instance.DefaultEmailAccount;
            var intent = new Intent (Intent.ActionSendto, Android.Net.Uri.FromParts ("mailto", "info@nachocove.com", null));
            intent.PutExtra (Intent.ExtraSubject, "Using Exchange with Nacho Mail");
            intent.PutExtra (Intent.ExtraText, "Hello,\n\nI'm interested in using Nacho Mail with an Exchange account.  Please send me a code to enable Exchange accounts.\n\nThanks");

            var activities = Util.EmailActivities (Activity, intent);
            if (activities.Count == 0) {
                var builder = new AlertDialog.Builder (Activity);
                builder.SetMessage (Resource.String.exchange_enable_request_issue);
                builder.SetPositiveButton (Resource.String.exchange_enable_request_issue_ack, (sender, e) => {});
                builder.Show ();
            }else if (activities.Count == 1){
                intent.SetPackage (activities[0].ActivityInfo.ApplicationInfo.PackageName);
                StartActivity (intent);
            } else {
                // FIXME: use our own picker that shows only items from activities, which may have filtered out Nacho
                var chooser = Intent.CreateChooser (intent, GetString (Resource.String.exchange_enable_send_request));
                StartActivity (chooser);
            }
        }

        void EnterCode ()
        {
            MessageLabel.SetText (Resource.String.exchange_enable_enter_code_message);
            EnterCodeButton.Visibility = ViewStates.Gone;
            RequestCodeButton.Visibility = ViewStates.Gone;
            SubmitButton.Visibility = ViewStates.Visible;
            CodeField.Visibility = ViewStates.Visible;
            CodeField.RequestFocus ();
        }

        void SubmitCode ()
        {
            var code = CodeField.Text;
            var verified = NachoCore.Utils.PermissionManager.Instance.VerifyExchangeCode (code);
            if (verified) {
                Listener listener;
                if (WeakListener.TryGetTarget (out listener)) {
                    listener.OnExchangeEnabled ();
                }
            } else {
                var builder = new AlertDialog.Builder (Activity);
                builder.SetMessage (Resource.String.exchange_enable_invalid_code);
                builder.SetPositiveButton (Resource.String.exchange_enable_invalid_code_ack, (sender, e) => {});
                builder.Show ();
            }
        }

        #endregion
    }
}
