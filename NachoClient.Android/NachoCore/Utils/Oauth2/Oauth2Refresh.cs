//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using System.Threading;
using NachoPlatform;
using NachoCore.Utils;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;

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
            var query = !string.IsNullOrEmpty (ClientSecret) ? "client_secret=" + Uri.EscapeDataString (ClientSecret) : "";
            query += "&grant_type=" + "refresh_token" +
                "&refresh_token=" + Uri.EscapeDataString (Cred.GetRefreshToken ()) +
                "&client_id=" + Uri.EscapeDataString (ClientId);
            
            var requestUri = new Uri (RefreshUrl + "?" + query);
            var request = new NcHttpRequest (HttpMethod.Post, requestUri);

            HttpClient.SendRequest (request, TimeoutSecs, ((response, token) => {
                if (response.StatusCode != System.Net.HttpStatusCode.OK) {
                    Log.Error (Log.LOG_SYS, "OAUTH2 HTTP Status {0}", response.StatusCode.ToString ());
                    onFailure (Cred);
                    return;
                }
                var jsonResponse = response.GetContent ();
                var decodedResponse = JsonConvert.DeserializeObject<OAuth2RefreshRespose> (Encoding.UTF8.GetString (jsonResponse));
                if ("Bearer" != decodedResponse.token_type) {
                    Log.Error (Log.LOG_SYS, "Unknown OAUTH2 token_type {0}", decodedResponse.token_type);
                }
                if (null == decodedResponse.access_token || null == decodedResponse.expires_in) {
                    Log.Error (Log.LOG_SYS, "Missing OAUTH2 access_token {0} or expires_in {1}", decodedResponse.access_token, decodedResponse.expires_in);
                    onFailure (Cred);
                }
                Log.Info (Log.LOG_SYS, "OAUTH2 Token refreshed. expires_in={0}", decodedResponse.expires_in);
                // also there's an ID token: http://stackoverflow.com/questions/8311836/how-to-identify-a-google-oauth2-user/13016081#13016081
                Cred.UpdateOauth2 (decodedResponse.access_token, 
                    string.IsNullOrEmpty (decodedResponse.refresh_token) ? Cred.GetRefreshToken () : decodedResponse.refresh_token,
                    uint.Parse (decodedResponse.expires_in));
                onSuccess (Cred);
            }), ((ex, token) => {
                Log.Error (Log.LOG_SYS, "OAUTH2 Exception {0}", ex.ToString ());
                onFailure (Cred);
            }), Token);
        }
    }
}

