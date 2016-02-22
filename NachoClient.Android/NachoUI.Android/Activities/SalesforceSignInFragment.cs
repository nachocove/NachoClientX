﻿
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

using Xamarin.Auth;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore;
using System.Net;
using NachoCore.SFDC;

namespace NachoClient.AndroidClient
{
    public class SalesforceSignInFragment : Fragment
    {
        private const int SIGNIN_REQUEST_CODE = 1;

        private const string ACCOUNT_ID_KEY = "accountId";
        private const string SERVICE_KEY = "service";

        McAccount Account;
        McAccount.AccountServiceEnum Service;

        bool finished = false;

        SFDCOAuth2Authenticator Authenticator;

        public static SalesforceSignInFragment newInstance (McAccount.AccountServiceEnum Service, McAccount account)
        {
            var fragment = new SalesforceSignInFragment ();
            fragment.Service = Service;
            fragment.Account = account;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            if (null != savedInstanceState) {
                int accountId = savedInstanceState.GetInt (ACCOUNT_ID_KEY, 0);
                if (0 != accountId) {
                    Account = McAccount.QueryById<McAccount> (accountId);
                }
                Service = (McAccount.AccountServiceEnum)savedInstanceState.GetInt (SERVICE_KEY, (int)McAccount.AccountServiceEnum.None);
            }
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.SalesforceSignInFragment, container, false);
            return view;
        }

        public override void OnStart ()
        {
            base.OnStart ();

            if (ValidationIsFinished ()) {
                return;
            }

            if (null == Authenticator) {
                RestartAuthenticator ();
            }
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            if (null != Account) {
                outState.PutInt (ACCOUNT_ID_KEY, Account.Id);
            }
            outState.PutInt (SERVICE_KEY, (int)Service);
        }

        void RestartAuthenticator ()
        {
            if (Authenticator != null) {
                Authenticator.Completed -= AuthCompleted;
                Authenticator.Error -= AuthError;
            }
            WebAuthenticator.ClearCookies ();
            var scopes = new List<string> ();
            scopes.Add ("api");
            scopes.Add ("refresh_token");
            string loginHint = null;
            if (Account != null) {
                loginHint = Account.EmailAddr;
            }
            Authenticator = new SFDCOAuth2Authenticator (
                clientId: SFDCOAuth2Constants.ClientId,
                clientSecret: SFDCOAuth2Constants.ClientSecret,
                scope: String.Join (" ", scopes.ToArray ()),
                accessTokenUrl: new Uri (SFDCOAuth2Constants.TokenUrl),
                authorizeUrl: new Uri (SFDCOAuth2Constants.AuthorizeUrl),
                redirectUrl: new Uri (SFDCOAuth2Constants.Redirecturi),
                loginHint: loginHint);
            Authenticator.AllowCancel = true;
            Authenticator.Completed += AuthCompleted;
            Authenticator.Error += AuthError;
            var vc = Authenticator.GetUI (Activity);
            StartActivityForResult (vc, SIGNIN_REQUEST_CODE);
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult (requestCode, resultCode, data);

            switch (requestCode) {
            case SIGNIN_REQUEST_CODE:
                if (Result.Ok == resultCode) {
                    ValidationIsFinished ();
                } else {
                    // Canceled in Google land
                    Activity.Finish ();
                }
                break;
            }
        }

        bool ValidationIsFinished ()
        {
            if (!finished) {
                var account = McAccount.GetAccountBeingConfigured ();
                if ((null != account) && (McAccount.ConfigurationInProgressEnum.CredentialsValidated == account.ConfigurationInProgress)) {
                    finished = true;
                    var parent = (CredentialsFragmentDelegate)Activity;
                    parent.CredentialsValidated (account);
                }
            }
            return finished;
        }

        public void AuthCompleted (object sender, AuthenticatorCompletedEventArgs e)
        {
            if (e.IsAuthenticated) {
                string access_token;
                e.Account.Properties.TryGetValue ("access_token", out access_token);

                string refresh_token;
                e.Account.Properties.TryGetValue ("refresh_token", out refresh_token);

                uint expireSecs = 3600;

                string id_url;
                e.Account.Properties.TryGetValue ("id", out id_url);

                string userInfoString;
                try {
                    var url = String.Format (id_url);
                    var client = new WebClient ();
                    client.Headers.Add ("Authorization", String.Format ("Bearer {0}", access_token));
                    userInfoString = client.DownloadString (url);
                } catch (Exception ex) {
                    Log.Info (Log.LOG_UI, "AuthCompleted: exception fetching user info {0}", ex);
                    NcAlertView.ShowMessage (Activity, "Nacho Mail", "We could not complete your account authentication.  Please try again.");
                    return;
                }

                var userInfo = Newtonsoft.Json.Linq.JObject.Parse (userInfoString);

                if (LoginHelpers.ConfiguredAccountExists ((string)userInfo ["email"], Service)) {
                    Log.Info (Log.LOG_UI, "SalesforceSignInFragment existing account: {0}", userInfo.Property ("email"));
                    NcAlertView.ShowMessage (this.Activity, "Account Exists", "An account with that email address already exists. Duplicate accounts are not supported.");
                    RestartAuthenticator ();
                } else {
                    if (Account != null) {
                        Log.Info (Log.LOG_UI, "SalesforceSignInFragment removing account ID{0}", Account.Id);
                        NcAccountHandler.Instance.RemoveAccount (Account.Id);
                        Account = null;
                    }
                    string instanceUrl;
                    e.Account.Properties.TryGetValue ("instance_url", out instanceUrl);
                    Account = NcAccountHandler.Instance.CreateAccountAndServerForSalesForce (Service,
                        (string)userInfo ["email"],
                        access_token,
                        refresh_token,
                        expireSecs,
                        new Uri (instanceUrl));
                    Log.Info (Log.LOG_UI, "SalesforceSignInFragment created account ID{0}", Account.Id);
                    Account.ConfigurationInProgress = McAccount.ConfigurationInProgressEnum.Done;
                    NcApplication.Instance.InvokeStatusIndEventInfo (null, NcResult.SubKindEnum.Info_AccountSetChanged);
                    Account.Update ();
                    SalesForceProtoControl.SetShouldAddBccToEmail (Account.Id, true);
                    BackEnd.Instance.Start (Account.Id);
                    Activity.Finish ();
                }
            } else {
                Log.Info (Log.LOG_UI, "SalesforceSignInFragment completed unauthenticated");
                NcAlertView.ShowMessage (this.Activity, "Nacho Mail", "We could not complete your account authentication.  Please try again.");
            }
        }

        public void AuthError (object sender, AuthenticatorErrorEventArgs e)
        {
            Log.Info (Log.LOG_UI, "SalesforceSignInFragment auth error");
            // Salesforce has already complained
            Activity.Finish ();
        }
    }
}
