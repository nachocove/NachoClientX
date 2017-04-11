
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
using Android.Support.CustomTabs;

namespace NachoClient.AndroidClient
{
    public class GoogleSignInFragment : Fragment
    {
        private const int SIGNIN_REQUEST_CODE = 1;

        private const string ACCOUNT_ID_KEY = "accountId";
        private const string SERVICE_KEY = "service";

        McAccount Account;
        McAccount.AccountServiceEnum Service;

        bool finished = false;

        GoogleOAuth2Authenticator Authenticator;

        public static GoogleSignInFragment newInstance (McAccount.AccountServiceEnum Service, McAccount account)
        {
            var fragment = new GoogleSignInFragment ();
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
            var view = inflater.Inflate (Resource.Layout.GoogleSignInFragment, container, false);
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

        public override void OnDestroy ()
        {
            base.OnDestroy ();
            if (Authenticator != null) {
                Authenticator.Stop ();
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
                Authenticator.Stop ();
                Authenticator.Completed -= AuthCompleted;
                Authenticator.Error -= AuthError;
            }
            WebAuthenticator.ClearCookies ();
            GoogleOAuth2Authenticator.Create (Account != null ? Account.EmailAddr : null, (GoogleOAuth2Authenticator auth) => {
                Authenticator = auth;
                Authenticator.AllowCancel = true;
                Authenticator.Completed += AuthCompleted;
                Authenticator.Error += AuthError;
                var tabsIntent = Authenticator.GetUI (Activity) as CustomTabsIntent;
                //var tabsIntent = builder.Build ();
                var url = Authenticator.GetInitialUrlAsync ().Result;
                tabsIntent.LaunchUrl (Activity, Android.Net.Uri.Parse (url.AbsoluteUri));
            });
        }

        bool ValidationIsFinished()
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

        void ValidationFailed ()
        {
            var parent = (CredentialsFragmentDelegate)Activity;
            parent.CredentialsValidationFailed ();
        }

        public void AuthCompleted (object sender, AuthenticatorCompletedEventArgs e)
        {
            if (e.IsAuthenticated) {
                string access_token;
                e.Account.Properties.TryGetValue ("access_token", out access_token);

                string refresh_token;
                e.Account.Properties.TryGetValue ("refresh_token", out refresh_token);

                string expiresString;
                uint expireSecs = 0;
                if (e.Account.Properties.TryGetValue ("expires_in", out expiresString)) {
                    if (!uint.TryParse (expiresString, out expireSecs)) {
                        Log.Info (Log.LOG_UI, "AuthCompleted: Could not convert expires value {0} to int", expiresString);
                    } else {
                        Log.Info (Log.LOG_SYS, "OAUTH2 Token acquired. expires_in={0}", expireSecs);
                    }
                }

                var source = "https://www.googleapis.com/oauth2/v1/userinfo";
                var url = String.Format ("{0}?access_token={1}", source, access_token);

                Newtonsoft.Json.Linq.JObject userInfo;
                try {
                    var userInfoString = new System.Net.WebClient ().DownloadString (url);
                    userInfo = Newtonsoft.Json.Linq.JObject.Parse (userInfoString);
                } catch (Exception ex) {
                    Log.Info (Log.LOG_UI, "AuthCompleted: exception fetching user info from {0}: {1}", source, ex);
                    NcAlertView.ShowMessage (Activity, "Apollo Mail", "We could not complete your account authentication.  Please try again.");
                    return;
                }

                if (LoginHelpers.ConfiguredAccountExists ((string)userInfo ["email"])) {
                    Log.Info (Log.LOG_UI, "GoogleCredentialsViewController existing account: {0}", userInfo.Property ("email"));
                    NcAlertView.ShowMessage (Activity, "Account Exists", "An account with that email address already exists. Duplicate accounts are not supported.");
                    RestartAuthenticator ();
                } else {
                    if (Account != null) {
                        Log.Info (Log.LOG_UI, "GoogleCredentialsViewController removing account ID{0}", Account.Id);
                        NcAccountHandler.Instance.RemoveAccount (Account.Id);
                        Account = null;
                    }
                    Account = NcAccountHandler.Instance.CreateAccount (Service,
                        (string)userInfo ["email"],
                        access_token,
                        refresh_token,
                        expireSecs);
                    NcAccountHandler.Instance.MaybeCreateServersForIMAP (Account, Service);
                    Log.Info (Log.LOG_UI, "GoogleCredentialsViewController created account ID{0}", Account.Id);

                    Newtonsoft.Json.Linq.JToken picture;
                    if (userInfo.TryGetValue ("picture", out picture)) {
                        var imageUrlString = ((string)picture).Replace ("/photo.jpg", "/s200-c-k/photo.jpg");
                        Account.PopulateProfilePhotoFromURL (new Uri (imageUrlString));
                    }
                    Account.ConfigurationInProgress = McAccount.ConfigurationInProgressEnum.CredentialsValidated;
                    Account.Update ();
                    BackEnd.Instance.Start (Account.Id);
                }
                ValidationIsFinished ();
            } else {
                Log.Info (Log.LOG_UI, "GoogleCredentialsViewController completed unauthenticated");
                ValidationFailed ();
            }
        }

        public void AuthError (object sender, AuthenticatorErrorEventArgs e)
        {
            Log.Info (Log.LOG_UI, "GoogleCredentialsViewController auth error");
            ValidationFailed ();
        }
    }
}

