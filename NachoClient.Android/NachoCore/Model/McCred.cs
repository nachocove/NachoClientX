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

namespace NachoCore.Model
{
    public class McCred : McAbstrObjectPerAcc
    {
        public enum CredTypeEnum { 
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
        // General-use. Where can a user manually refresh this credential?
        public string RectificationUrl { get; set; }
        // General-use. When we open the web view for web-based auth, where should we point it?
        public string RedirectUrl { get; set; }
        // General-use. What substring in a URL will cause us to tear-down the web view?
        public string DoneSubstring { get; set; }

        public McCred ()
        {
            Expiry = DateTime.MaxValue;
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
                Update ();
            } else {
                Password = password;
                Update ();
            }
            var account = McAccount.QueryById<McAccount> (AccountId);
            NcApplication.Instance.InvokeStatusIndEventInfo (account, NcResult.SubKindEnum.Info_McCredPasswordChanged);
        }

        public void UpdateOauth2 (string accessToken, string refreshToken, DateTime expiry)
        {
            NcAssert.True (0 != Id);
            NcAssert.AreEqual ((int)CredTypeEnum.OAuth2, (int)CredType, string.Format ("UpdateOauth2:CredType:{0}", CredType));
            Expiry = expiry;
            if (Keychain.Instance.HasKeychain ()) {
                NcAssert.True (Keychain.Instance.SetAccessToken (Id, accessToken));
                AccessToken = null;
                NcAssert.True (Keychain.Instance.SetRefreshToken (Id, refreshToken));
                RefreshToken = null;
                Update ();
            } else {
                AccessToken = accessToken;
                RefreshToken = refreshToken;
                Update ();
            }
        }

        static public string Join(string domain, string username)
        {
            if (String.IsNullOrEmpty (domain)) {
                return username;
            } else {
                return String.Join ("\\", new string[] { domain, username });
            }
        }

        static public void Split(string username, out string domain, out string user)
        {
            user = "";
            domain = "";

            if (String.IsNullOrEmpty (username)) {
                return;
            }
            int slashIndex = username.IndexOf ("\\", StringComparison.OrdinalIgnoreCase);
            if (-1 == slashIndex) {
                user = username;
            } else if(username.Length == (slashIndex + 1)) {
                user = username.Substring (0, slashIndex);
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

        public string GetTestPassword()
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

        public bool AttemptRefresh (Action<McCred> onSuccess, Action<McCred> onFailure)
        {
            switch (CredType) {
            case CredTypeEnum.Password:
                return false;
            case CredTypeEnum.OAuth2:
                var account = McAccount.QueryById<McAccount> (AccountId);
                if (account.AccountService == McAccount.AccountServiceEnum.GoogleDefault) {
                    RefreshOAuth2Google (onSuccess, onFailure);
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
        }

        private async Task<bool> TryRefresh ()
        {
            var handler = new NativeMessageHandler ();
            var client = (IHttpClient)Activator.CreateInstance (HttpClientType, handler, true);
            var query = "client_secret=" + BuildInfo.GoogleClientSecret +
                        "&grant_type=" + "refresh_token" +
                        "&refresh_token=" + GetRefreshToken () +
                        "&client_id=" + BuildInfo.GoogleClientId;
            var requestUri = new Uri ("https://www.googleapis.com/oauth2/v3/token" + "?" + query);
            var httpRequest = new HttpRequestMessage (HttpMethod.Post, requestUri);
            try {
                var httpResponse = await client.SendAsync (httpRequest, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
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
                    UpdateOauth2 (response.access_token, GetRefreshToken (), 
                        DateTime.UtcNow.AddSeconds (int.Parse (response.expires_in)));
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

        private async void RefreshOAuth2Google (Action<McCred> onSuccess, Action<McCred> onFailure)
        {    
            bool result = await TryRefresh ();
            if (result) {
                onSuccess (this);
            } else {
                onFailure (this);
            }
        }
    }
}
