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
    public class Oauth2Refresh
    {
        const int KDefaultOauth2HttpTimeout = 30;

        protected McCred Cred;
        string ClientSecret;
        string ClientId;
        string RefreshUrl;
        int TimeoutSecs;

        public Oauth2Refresh (McCred cred, string refreshUrl, string clientSecret, string clientId, int timeoutSecs = KDefaultOauth2HttpTimeout)
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

        private class OAuth2RefreshRespose
        {
            public string access_token { get; set; }

            public string expires_in { get; set; }

            public string token_type { get; set; }

            public string refresh_token { get; set; }
        }

        public virtual void Refresh (Action<McCred> onSuccess, Action<McCred> onFailure, CancellationToken Token)
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

            HttpClient.SendRequest (request, TimeoutSecs, ((response, token) => {
                if (response.StatusCode != System.Net.HttpStatusCode.OK) {
                    string body = "";
                    if (response.HasBody) {
                        body = Encoding.UTF8.GetString (response.GetContent());
                    }
                    Log.Error (Log.LOG_SYS, "OAUTH2 HTTP Status {0}: {1}", response.StatusCode.ToString (), body);
                    onFailure (Cred);
                    return;
                }
                var jsonResponse = Encoding.UTF8.GetString(response.GetContent ());
                var decodedResponse = Newtonsoft.Json.Linq.JObject.Parse (jsonResponse);

                //var decodedResponse = JsonConvert.DeserializeObject<OAuth2RefreshRespose> (jsonResponse);
                Newtonsoft.Json.Linq.JToken tokenType;
                if (!decodedResponse.TryGetValue ("token_type", out tokenType) || (string)tokenType != "Bearer") {
                    Log.Error (Log.LOG_SYS, "Unknown OAUTH2 token_type {0}", tokenType);
                }

                Newtonsoft.Json.Linq.JToken accessToken;
                if (!decodedResponse.TryGetValue ("access_token", out accessToken)) {
                    Log.Error (Log.LOG_SYS, "Missing OAUTH2 access_token");
                    onFailure (Cred);
                }
                Newtonsoft.Json.Linq.JToken expiresIn;
                if (!decodedResponse.TryGetValue ("expiresIn", out expiresIn)) {
                    Log.Info (Log.LOG_SYS, "OAUTH2 Token refreshed.");
                } else {
                    Log.Info (Log.LOG_SYS, "OAUTH2 Token refreshed. expires_in={0}", expiresIn);
                }

                Newtonsoft.Json.Linq.JToken refreshToken;
                decodedResponse.TryGetValue ("refresh_token", out refreshToken);

                // also there's an ID token: http://stackoverflow.com/questions/8311836/how-to-identify-a-google-oauth2-user/13016081#13016081
                Cred.UpdateOauth2 ((string)accessToken,
                    string.IsNullOrEmpty ((string)refreshToken) ? Cred.GetRefreshToken () : (string)refreshToken,
                    string.IsNullOrEmpty ((string)expiresIn) ? 0 : uint.Parse ((string)expiresIn));
                onSuccess (Cred);
                RefreshAction (Cred, decodedResponse);
            }), ((ex, token) => {
                Log.Error (Log.LOG_SYS, "OAUTH2 Exception {0}", ex.ToString ());
                onFailure (Cred);
            }), Token);
        }

        protected virtual void RefreshAction (McCred cred, Newtonsoft.Json.Linq.JObject jsonObject)
        {
        }
    }
}

