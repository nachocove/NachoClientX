//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Xamarin.Auth;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using NachoCore.Model;
using System.Net.Http;
using NachoPlatform;
using Newtonsoft.Json;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace NachoCore.Utils
{

    public class GoogleOAuthConstants
    {
        public static string ClientId = "135541750674-r1oih3ikecbp0mqeje0sr03ns19f8rk9.apps.googleusercontent.com";
        public static string ClientSecret = "k9IvmgO7g_o_zGC9zVx9CPXm";
        public static string TokenUrl = "https://accounts.google.com/o/oauth2/token";
        public static string AuthorizeUrl = "https://accounts.google.com/o/oauth2/auth";
        public static string RefreshUrl = "https://www.googleapis.com/oauth2/v3/token";
        public static List<string> Scopes = new List<string> () {
            "email",
            "profile",
            "https://mail.google.com",
            "https://www.googleapis.com/auth/calendar",
            "https://www.google.com/m8/feeds/",
        };
    }

    public class GoogleOAuth2Authenticator : OAuth2Authenticator
    {

        OauthHttpServer HttpServer;
        Uri RedirectUri;

        public GoogleOAuth2Authenticator (string loginHint, OauthHttpServer httpServer, Uri redirectUri, GetUsernameAsyncFunc getUsernameAsync = null)
            : base (
                clientId: GoogleOAuthConstants.ClientId,
                clientSecret: GoogleOAuthConstants.ClientSecret,
                scope: String.Join (" ", GoogleOAuthConstants.Scopes.ToArray ()),
                authorizeUrl: new Uri (GoogleOAuthConstants.AuthorizeUrl),
                redirectUrl: redirectUri,
                accessTokenUrl: new Uri (GoogleOAuthConstants.TokenUrl),
                getUsernameAsync: getUsernameAsync,
                isUsingNativeUI: true)
        {
            LoginHint = loginHint;
            HttpServer = httpServer;
            HttpServer.OnRequest += OnHttpServerRequest;
            RedirectUri = redirectUri;
        }

        public string LoginHint {
            get;
            set;
        }

        public static void Create (string loginHint, Action<GoogleOAuth2Authenticator> completion)
        {
            var server = new OauthHttpServer ();
            server.Start (() => {
                var uri = new Uri (String.Format ("http://127.0.0.1:{0}/authorization_callback", server.Port));
                var auth = new GoogleOAuth2Authenticator (loginHint, server, uri);
                completion (auth);
            });
        }

        // Need offline & approval_prompt to get refresh token
        public override Task<Uri> GetInitialUrlAsync ()
        {
            var task = base.GetInitialUrlAsync ();
            var baseUrl = task.Result;
            var urlString = baseUrl.AbsoluteUri + "&access_type=offline&approval_prompt=force";

            if (!String.IsNullOrEmpty (LoginHint)) {
                urlString += String.Format ("&login_hint={0}", Uri.EscapeDataString (LoginHint));
            }

            var url = new Uri (urlString);
            return Task.FromResult (url);
        }

        public void OnHttpServerRequest (object sender, OauthHttpServer.Client client)
        {
            if (client.RequestUri.Host == RedirectUri.Host && client.RequestUri.LocalPath == RedirectUri.LocalPath) {
                client.Send (200, "OK", "<html><body></body></html>");
            } else {
                client.Send (404, "Not Found", "<html><body></body></html>");
            }
            InvokeOnUIThread.Instance.Invoke (() => {
                OnPageLoaded (client.RequestUri);
            });
        }

        protected override void OnRedirectPageLoaded (Uri url, IDictionary<string, string> query, IDictionary<string, string> fragment)
        {
            base.OnRedirectPageLoaded (url, query, fragment);
            HttpServer.GracefulStop (null);
        }

        public void Stop ()
        {
            HttpServer.Stop ();
        }
    }

    public class GoogleOauth2Refresh : Oauth2TokenRefresh
    {
        public GoogleOauth2Refresh (McCred cred) : base (cred, GoogleOAuthConstants.RefreshUrl, GoogleOAuthConstants.ClientSecret, GoogleOAuthConstants.ClientId)
        {
        }
    }
}

