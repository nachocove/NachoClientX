//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using System.Threading;
using NachoPlatform;
using NachoCore.Utils;
using System.Net.Http;
using Newtonsoft;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace NachoCore
{
    public class Oauth2TokenRefresh
    {
        const int KDefaultOauth2HttpTimeout = 30;

        protected McCred Cred;
        string ClientSecret;
        string ClientId;
        string RefreshUrl;
        int TimeoutSecs;

        public Oauth2TokenRefresh (McCred cred, string refreshUrl, string clientSecret, string clientId, int timeoutSecs = KDefaultOauth2HttpTimeout)
        {
            Cred = cred;
            RefreshUrl = refreshUrl;
            TimeoutSecs = timeoutSecs;
            ClientSecret = clientSecret;
            ClientId = clientId;
        }

        static public INcHttpClient TestHttpClient { get; set; }
        public INcHttpClient HttpClient {
            get {
                if (TestHttpClient != null) {
                    return TestHttpClient;
                } else {
                    return NcHttpClient.Instance;
                }
            }
        }

        public virtual void Refresh (Action<McCred> onSuccess, Action<McCred, bool> onFailure, CancellationToken Token)
        {
            var queryDict = new Dictionary<string, string> ();
            queryDict["client_id"] = ClientId;
            queryDict ["client_secret"] = ClientSecret;
            queryDict["grant_type"] = "refresh_token";
            queryDict["refresh_token"] = Cred.GetRefreshToken ();

            var queryString = string.Join ("&", queryDict.Select (kv => string.Format ("{0}={1}", kv.Key, Uri.EscapeDataString (kv.Value))));
            var requestUri = new Uri (RefreshUrl + "?" + queryString);
            var request = new NcHttpRequest (HttpMethod.Post, requestUri);
            request.Headers.Add ("Accept", "application/json");
            request.SetContent (null, "application/x-www-form-urlencoded");

            Log.Info (Log.LOG_SYS, "RefreshOAuth2({0}): Attempting Refresh", Cred.AccountId);

            HttpClient.SendRequest (request, TimeoutSecs, ((response, token) => {
                if (response.StatusCode != System.Net.HttpStatusCode.OK) {
                    bool fatalError = false;
                    switch (response.StatusCode) {
                    case System.Net.HttpStatusCode.BadRequest:
                        // This probably means the refresh token is no longer valid. We don't immediately punch
                        // up to the UI, because we really don't fully trust HTTP response code. Treat it as 'fatal',
                        // and let the underlying code decide what to do with fatal responses.
                        Log.Info (Log.LOG_SYS, "RefreshOAuth2({0}): Refresh Status {1}", Cred.AccountId, response.StatusCode.ToString ());
                        fatalError = true;
                        break;
                    default:
                        string body = "";
                        if (response.HasBody) {
                            body = Encoding.UTF8.GetString (response.GetContent());
                        }
                        Log.Warn (Log.LOG_SYS, "RefreshOAuth2({0}): HTTP Status {1}\n{2}", Cred.AccountId, response.StatusCode.ToString (), body);
                        break;
                    }
                    onFailure (Cred, fatalError);
                    return;
                }
                var jsonResponse = Encoding.UTF8.GetString(response.GetContent ());
                var decodedResponse = Newtonsoft.Json.Linq.JObject.Parse (jsonResponse);

#if DEBUG
                Log.Info (Log.LOG_SYS, "RefreshOAuth2({0}): : OAUTH2 response: {1}", Cred.AccountId, jsonResponse);
#endif
                Newtonsoft.Json.Linq.JToken tokenType;
                if (!decodedResponse.TryGetValue ("token_type", out tokenType) || (string)tokenType != "Bearer") {
                    Log.Error (Log.LOG_SYS, "RefreshOAuth2({0}): Unknown OAUTH2 token_type {1}", Cred.AccountId, tokenType);
                }

                Newtonsoft.Json.Linq.JToken accessToken;
                if (!decodedResponse.TryGetValue ("access_token", out accessToken)) {
                    Log.Error (Log.LOG_SYS, "RefreshOAuth2({0}): Missing OAUTH2 access_token {1}", Cred.AccountId, accessToken);
                    onFailure (Cred, true);
                }
                Newtonsoft.Json.Linq.JToken expiresIn;
                if (!decodedResponse.TryGetValue ("expires_in", out expiresIn)) {
                    Log.Info (Log.LOG_SYS, "RefreshOAuth2({0}): OAUTH2 Token refreshed.", Cred.AccountId);
                } else {
                    Log.Info (Log.LOG_SYS, "RefreshOAuth2({0}): OAUTH2 Token refreshed. expires_in={1}", Cred.AccountId, expiresIn);
                }

                Newtonsoft.Json.Linq.JToken refreshToken;
                decodedResponse.TryGetValue ("refresh_token", out refreshToken);

                int expires = null != expiresIn ? (int)expiresIn : 0;
                if (expires <= 0) {
                    expires = 3600;
                }
                // also there's an ID token: http://stackoverflow.com/questions/8311836/how-to-identify-a-google-oauth2-user/13016081#13016081
                Cred.UpdateOauth2 ((string)accessToken,
                    string.IsNullOrEmpty ((string)refreshToken) ? Cred.GetRefreshToken () : (string)refreshToken,
                    (uint)expires);
                onSuccess (Cred);
                RefreshAction (Cred, decodedResponse);
            }), ((ex, token) => {
                Log.Error (Log.LOG_SYS, "RefreshOAuth2({0}): Exception {1}", Cred.AccountId, ex.ToString ());
                onFailure (Cred, false);
            }), Token);
        }

        protected virtual void RefreshAction (McCred cred, Newtonsoft.Json.Linq.JObject jsonObject)
        {
        }
    }
}

