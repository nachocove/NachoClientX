using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using SQLite;
using ModernHttpClient;
using Newtonsoft.Json;
using NachoCore.Utils;
using NachoClient.Build;
using NachoPlatform;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class McCred : McAbstrObjectPerAcc
    {
        public enum CredTypeEnum
        {
            Password = 0,
            OAuth2,
            SAML,
            // Only append - values stored in DB.
        };

        public CredTypeEnum CredType { get; set; }
        // General-use.
        public string Username { get; set; }

        // We want to remember if the user entered their
        // own username or if we copied in the email address.
        public bool UserSpecifiedUsername { get; set; }

        public string Password { get; set; }

        /// <summary>
        /// DO NOT ACCESS private properties. Use UpdateXxx/GetXxx.
        /// private properties are here for SQLite.Net only!
        /// </summary>
        /// <value>The password.</value>

        // OAUTH2
        private string AccessToken { get; set; }
        // OAUTH2
        private string RefreshToken { get; set; }
        // General-use. When will the credential expire?
        public DateTime Expiry { get; set; }
        // General-use. Seconds the credential will expire in (should be used to set Expiry)
        public uint ExpirySecs { get; set; }
        // General-use. Where can a user manually refresh this credential?
        public string RectificationUrl { get; set; }
        // General-use. When we open the web view for web-based auth, where should we point it?
        public string RedirectUrl { get; set; }
        // General-use. What substring in a URL will cause us to tear-down the web view?
        public string DoneSubstring { get; set; }

        // General-use. The epoch of the Credentials.
        public int Epoch { get; set; }

        public McCred ()
        {
            Expiry = DateTime.MaxValue;
        }

        private void UpdateCredential ()
        {
            Epoch += 1;
            Update ();
        }

        /// <summary>
        /// The semantics are that the storage is written, whether in device keychain or DB.
        /// The Id must be valid (Insert()ed) before this API is called.
        /// </summary>
        /// <param name="password">Password.</param>
        public void UpdatePassword (string password)
        {
            NcAssert.True (0 != Id);
            NcAssert.AreEqual ((int)CredTypeEnum.Password, (int)CredType, string.Format ("UpdatePassword:CredType:{0}", CredType));
            Expiry = DateTime.MaxValue;
            RectificationUrl = null;
            if (Keychain.Instance.HasKeychain ()) {
                NcAssert.True (Keychain.Instance.SetPassword (Id, password));
                Password = null;
                UpdateCredential ();
            } else {
                Password = password;
                UpdateCredential ();
            }
            var account = McAccount.QueryById<McAccount> (AccountId);
            NcApplication.Instance.InvokeStatusIndEventInfo (account, NcResult.SubKindEnum.Info_McCredPasswordChanged);
        }

        public void UpdateOauth2 (string accessToken, string refreshToken, uint expirySecs)
        {
            NcAssert.True (0 != Id);
            NcAssert.AreEqual ((int)CredTypeEnum.OAuth2, (int)CredType, string.Format ("UpdateOauth2:CredType:{0}", CredType));
            Expiry = DateTime.UtcNow.AddSeconds (expirySecs);
            ExpirySecs = expirySecs;
            if (Keychain.Instance.HasKeychain ()) {
                NcAssert.True (Keychain.Instance.SetAccessToken (Id, accessToken));
                AccessToken = null;
                NcAssert.True (Keychain.Instance.SetRefreshToken (Id, refreshToken));
                RefreshToken = null;
                UpdateCredential ();
            } else {
                AccessToken = accessToken;
                RefreshToken = refreshToken;
                UpdateCredential ();
            }
        }

        public void ClearExpiry ()
        {
            Expiry = DateTime.MaxValue;
            RectificationUrl = null;
            Update ();
        }

        static public string Join (string domain, string username)
        {
            if (String.IsNullOrEmpty (domain)) {
                return username;
            } else {
                return String.Join ("\\", new string[] { domain, username });
            }
        }

        static public void Split (string username, out string domain, out string user)
        {
            user = "";
            domain = "";

            if (String.IsNullOrEmpty (username)) {
                return;
            }
            int slashIndex = username.IndexOf ("\\", StringComparison.OrdinalIgnoreCase);
            if (-1 == slashIndex) {
                user = username;
            } else if (username.Length == (slashIndex + 1)) {
                domain = username.Substring (0, slashIndex);
            } else {
                domain = username.Substring (0, slashIndex);
                user = username.Substring (slashIndex + 1);
            }
        }


        /// <summary>
        /// Sets the password in RAM only. Used only when validating.
        /// Use UpdatePassord to actually persist the password.
        /// </summary>
        /// <param name="password">Password.</param>
        public void SetTestPassword (string password)
        {
            Password = password;
        }

        public string GetTestPassword ()
        {
            return Password;
        }

        public string GetPassword ()
        {
            if (Keychain.Instance.HasKeychain () && null == Password) {
                return Keychain.Instance.GetPassword (Id);
            } else {
                return Password;
            }
        }

        public string GetAccessToken ()
        {
            if (Keychain.Instance.HasKeychain () && null == AccessToken) {
                return Keychain.Instance.GetAccessToken (Id);
            } else {
                return AccessToken;
            }
        }

        public string GetRefreshToken ()
        {
            if (Keychain.Instance.HasKeychain () && null == RefreshToken) {
                return Keychain.Instance.GetRefreshToken (Id);
            } else {
                return RefreshToken;
            }
        }

        public override int Delete ()
        {
            if (Keychain.Instance.HasKeychain ()) {
                Keychain.Instance.DeletePassword (Id);
                Password = null;
                Update ();
            } 
            return base.Delete ();
        }

        static int KOauth2RefreshIntervalSecs = 600;
        static int KOauth2RefreshPercent = 80;
        static CancellationTokenSource RefreshCancelSource;
        static NcTimer Oauth2RefreshTimer = null;

        public static void StartOauthRefreshTimer ()
        {
            if (null == Oauth2RefreshTimer) {
                RefreshCancelSource = new CancellationTokenSource ();
                var x = new NcTimer ("McCred:Oauth2RefreshTimer", state => {
                    foreach (var cred in McCred.QueryAllOauth2()) {
                        PossiblyRefreshToken (cred, RefreshCancelSource.Token);
                    }
                }, null, KOauth2RefreshIntervalSecs, KOauth2RefreshIntervalSecs);
                x.Stfu = true;
                // protect against stop having been called right during initialization.
                if (!RefreshCancelSource.IsCancellationRequested) {
                    Oauth2RefreshTimer = x;
                }
            }            
        }

        public static void StopOauthRefreshTimer ()
        {
            if (null != RefreshCancelSource) {
                RefreshCancelSource.Cancel ();
            }
            if (null != Oauth2RefreshTimer) {
                Oauth2RefreshTimer.Dispose ();
                Oauth2RefreshTimer = null;
            }
        }

        private static void PossiblyRefreshToken (McCred cred, CancellationToken Token)
        {
            Action<McCred> onSuccess = (c) => {
                Log.Info (Log.LOG_BACKEND, "PossiblyRefreshToken({0}): success", c.AccountId);
            };

            Action<McCred> onFailure = (c) => {
                Log.Info (Log.LOG_BACKEND, "PossiblyRefreshToken({0}): failure", c.AccountId);
            };

            var expiryFractionSecs = Math.Round ((double)(cred.ExpirySecs * (100 - KOauth2RefreshPercent)) / 100);
            if (cred.Expiry.AddSeconds (-expiryFractionSecs) <= DateTime.UtcNow) {
                if (!cred.AttemptRefresh (onSuccess, onFailure, Token)) {
                    onFailure (cred);
                }
            }
        }

        public override int Insert ()
        {
            if (CredType == CredTypeEnum.OAuth2) {
                StartOauthRefreshTimer ();
            }
            return base.Insert ();
        }

        public static List<McCred> QueryAllOauth2 ()
        {
            return NcModel.Instance.Db.Query<McCred> (
                "SELECT * FROM McCred WHERE CredType = ?", CredTypeEnum.OAuth2);
        }

        public bool AttemptRefresh (Action<McCred> onSuccess, Action<McCred> onFailure, CancellationToken Token)
        {
            switch (CredType) {
            case CredTypeEnum.Password:
                return false;
            case CredTypeEnum.OAuth2:
                var account = McAccount.QueryById<McAccount> (AccountId);
                if (account.AccountService == McAccount.AccountServiceEnum.GoogleDefault) {
                    RefreshOAuth2Google (onSuccess, onFailure, Token);
                    return true;
                }
                Log.Error (Log.LOG_BACKEND, "Unable to do OAUTH2 refresh for {0}", account.AccountService.ToString ());
                return false;
            default:
                NcAssert.CaseError (CredType.ToString ());
                return false;
            }
        }

        public static Type HttpClientType = typeof(MockableHttpClient);

        private class OAuth2RefreshRespose
        {
            public string access_token { get; set; }

            public string expires_in { get; set; }

            public string token_type { get; set; }

            public string refresh_token { get; set; }
        }

        private async Task<bool> TryRefresh (CancellationToken Token)
        {
            var handler = new NativeMessageHandler ();
            var client = (IHttpClient)Activator.CreateInstance (HttpClientType, handler, true);
            var query = "client_secret=" + Uri.EscapeDataString (GoogleOAuthConstants.ClientSecret) +
                        "&grant_type=" + "refresh_token" +
                        "&refresh_token=" + Uri.EscapeDataString (GetRefreshToken ()) +
                        "&client_id=" + Uri.EscapeDataString (GoogleOAuthConstants.ClientId);
            var requestUri = new Uri ("https://www.googleapis.com/oauth2/v3/token" + "?" + query);
            var httpRequest = new HttpRequestMessage (HttpMethod.Post, requestUri);
            try {
                var httpResponse = await client.SendAsync (httpRequest, HttpCompletionOption.ResponseContentRead, Token);
                if (httpResponse.StatusCode == System.Net.HttpStatusCode.OK) {
                    var jsonResponse = await httpResponse.Content.ReadAsStringAsync ().ConfigureAwait (false);
                    var response = JsonConvert.DeserializeObject<OAuth2RefreshRespose> (jsonResponse);
                    if ("Bearer" != response.token_type) {
                        Log.Error (Log.LOG_SYS, "Unknown OAUTH2 token_type {0}", response.token_type);
                    }
                    if (null == response.access_token || null == response.expires_in) {
                        Log.Error (Log.LOG_SYS, "Missing OAUTH2 access_token {0} or expires_in {1}", response.access_token, response.expires_in);
                        return false;
                    }
                    Log.Info (Log.LOG_SYS, "OAUTH2 Token refreshed. expires_in={0}", response.expires_in);
                    UpdateOauth2 (response.access_token, 
                        string.IsNullOrEmpty (response.refresh_token) ? GetRefreshToken () : response.refresh_token,
                        uint.Parse (response.expires_in));
                    return true;
                } else {
                    Log.Error (Log.LOG_SYS, "OAUTH2 HTTP Status {0}", httpResponse.StatusCode.ToString ());
                    return false;
                }
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "OAUTH2 Exception {0}", ex.ToString ());
                return false;
            }
        }

        private async void RefreshOAuth2Google (Action<McCred> onSuccess, Action<McCred> onFailure, CancellationToken Token)
        {    
            bool result = await TryRefresh (Token);
            if (result) {
                onSuccess (this);
            } else {
                onFailure (this);
            }
        }
    }
}
