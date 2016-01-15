//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Xamarin.Auth;
using System.Threading.Tasks;
using NachoCore.Model;
using System.Net.Http;
using NachoPlatform;
using Newtonsoft.Json;
using System.Text;
using System.Threading;

namespace NachoCore.Utils
{

    public class GoogleOAuthConstants
    {
        public static string ClientId = "135541750674-3bmfkmlm767ipe0ih0trqf9o4jgum27h.apps.googleusercontent.com";
        public static string ClientSecret = "T08VVinKbAPiXjIlV3U5O12S";
        public static string TokenUrl = "https://accounts.google.com/o/oauth2/token";
        public static string AuthorizeUrl = "https://accounts.google.com/o/oauth2/auth";
        public static string RefreshUrl = "https://www.googleapis.com/oauth2/v3/token";
        public static string Redirecturi = "http://www.nachocove.com/authorization_callback";
    }

    public class GoogleOAuth2Authenticator : OAuth2Authenticator
    {


        public GoogleOAuth2Authenticator (string clientId, string clientSecret, string scope, Uri authorizeUrl, Uri redirectUrl, Uri accessTokenUrl, string loginHint, GetUsernameAsyncFunc getUsernameAsync = null)
            : base (clientId, clientSecret, scope, authorizeUrl, redirectUrl, accessTokenUrl, getUsernameAsync)
        {
            this.LoginHint = loginHint;
        }

        public string LoginHint {
            get;
            set;
        }

        // Need offline & approval_prompt to get refresh token
        public override Task<Uri> GetInitialUrlAsync ()
        {
            string uriString = string.Format (
                "{0}?client_id={1}&redirect_uri={2}&response_type={3}&scope={4}&state={5}&access_type=offline&approval_prompt=force",
                this.AuthorizeUrl.AbsoluteUri,
                Uri.EscapeDataString (this.ClientId),
                Uri.EscapeDataString (this.RedirectUrl.AbsoluteUri),
                this.AccessTokenUrl == null ? "token" : "code",
                Uri.EscapeDataString (this.Scope),
                Uri.EscapeDataString (this.RequestState)
            );

            if (!String.IsNullOrEmpty (LoginHint)) {
                uriString += String.Format ("&login_hint={0}", Uri.EscapeDataString (LoginHint));
            }

            var url = new Uri (uriString);
            return Task.FromResult (url);
        }
    }

    public class GoogleOauth2Refresh : Oauth2Refresh
    {
        public GoogleOauth2Refresh (McCred cred) : base (cred, GoogleOAuthConstants.RefreshUrl, GoogleOAuthConstants.ClientSecret, GoogleOAuthConstants.ClientId)
        {
        }
    }
}

