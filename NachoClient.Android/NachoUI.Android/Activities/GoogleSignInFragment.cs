
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

namespace NachoClient.AndroidClient
{
    public class GoogleSignInFragment : Fragment
    {
        McAccount Account;
        McAccount.AccountServiceEnum Service;

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
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.GoogleSignInFragment, container, false);
            return view;
        }

        public override void OnStart ()
        {
            base.OnStart ();

            var account = McAccount.GetAccountBeingConfigured ();
            if ((null != account) && (McAccount.ConfigurationInProgressEnum.CredentialsValidated == account.ConfigurationInProgress)) {
                var parent = (CredentialsFragmentDelegate)Activity;
                parent.CredentialsValidated (account);
                return;
            }

            if (null == Authenticator) {
                RestartAuthenticator ();
            }
        }

        public override void OnResume ()
        {
            base.OnResume ();

        }

        void RestartAuthenticator ()
        {
            if (Authenticator != null) {
                Authenticator.Completed -= AuthCompleted;
                Authenticator.Error -= AuthError;
            }
            WebAuthenticator.ClearCookies ();
            var scopes = new List<string> ();
            scopes.Add ("email");
            scopes.Add ("profile");
            scopes.Add ("https://mail.google.com");
            scopes.Add ("https://www.googleapis.com/auth/calendar");
            scopes.Add ("https://www.google.com/m8/feeds/");
            string loginHint = null;
            if (Account != null) {
                loginHint = Account.EmailAddr;
            }
            Authenticator = new GoogleOAuth2Authenticator (
                clientId: GoogleOAuthConstants.ClientId,
                clientSecret: GoogleOAuthConstants.ClientSecret,
                scope: String.Join (" ", scopes.ToArray ()),
                accessTokenUrl: new Uri ("https://accounts.google.com/o/oauth2/token"),
                authorizeUrl: new Uri ("https://accounts.google.com/o/oauth2/auth"),
                redirectUrl: new Uri ("http://www.nachocove.com/authorization_callback"),
                loginHint: loginHint);
            Authenticator.AllowCancel = true;
            Authenticator.Completed += AuthCompleted;
            Authenticator.Error += AuthError;
            var vc = Authenticator.GetUI (Activity);
            StartActivity (vc);
        }

        public void AuthCompleted (object sender, AuthenticatorCompletedEventArgs e)
        {
            if (e.IsAuthenticated) {
                string access_token;
                e.Account.Properties.TryGetValue ("access_token", out access_token);

                string refresh_token;
                e.Account.Properties.TryGetValue ("refresh_token", out refresh_token);

                string expires_in;
                e.Account.Properties.TryGetValue ("expires_in", out expires_in);
                Log.Info (Log.LOG_SYS, "OAUTH2 Token acquired. expires_in={0}", expires_in);

                string expiresString = "0";
                uint expireSecs = 0;
                if (e.Account.Properties.TryGetValue ("expires", out expiresString)) {
                    if (!uint.TryParse (expiresString, out expireSecs)) {
                        Log.Info (Log.LOG_UI, "AuthCompleted: Could not convert expires value {0} to int", expiresString);
                    }
                }

                var url = String.Format ("https://www.googleapis.com/oauth2/v1/userinfo?access_token={0}", access_token);

                string userInfoString = null;
                try {
                    userInfoString = new System.Net.WebClient ().DownloadString (url);
                } catch (Exception ex) {
                    Log.Info (Log.LOG_UI, "AuthCompleted: exception fetching user info {0}", ex);
                    NcAlertView.ShowMessage (Activity, "Nacho Mail", "We could not complete your account authentication.  Please try again.");
                    return;
                }

                var userInfo = Newtonsoft.Json.Linq.JObject.Parse (userInfoString);

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
            } else {
                Log.Info (Log.LOG_UI, "GoogleCredentialsViewController completed unauthenticated");
                NcAlertView.ShowMessage (Activity, "Nacho Mail", "We could not complete your account authentication.  Please try again.");
            }
        }

        public void AuthError (object sender, AuthenticatorErrorEventArgs e)
        {
            Log.Info (Log.LOG_UI, "GoogleCredentialsViewController auth error");
            NcAlertView.ShowMessage (Activity, "Nacho Mail", "We could not complete your account authentication.  Please try again.");
        }
    }
}

