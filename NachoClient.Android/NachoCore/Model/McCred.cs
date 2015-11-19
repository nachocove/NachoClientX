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
using System.IO;
using System.Linq;
using System.Text;

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
            NcAssert.True (Keychain.Instance.SetPassword (Id, password));
            Password = null;
            UpdateCredential ();
            var account = McAccount.QueryById<McAccount> (AccountId);
            NcApplication.Instance.InvokeStatusIndEventInfo (account, NcResult.SubKindEnum.Info_McCredPasswordChanged);
        }

        public void UpdateOauth2 (string accessToken, string refreshToken, uint expirySecs)
        {
            NcAssert.True (0 != Id);
            NcAssert.AreEqual ((int)CredTypeEnum.OAuth2, (int)CredType, string.Format ("UpdateOauth2:CredType:{0}", CredType));
            Expiry = DateTime.UtcNow.AddSeconds (expirySecs);
            ExpirySecs = expirySecs;
            NcAssert.True (Keychain.Instance.SetAccessToken (Id, accessToken));
            AccessToken = null;
            NcAssert.True (Keychain.Instance.SetRefreshToken (Id, refreshToken));
            RefreshToken = null;
            UpdateCredential ();
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
            if (null == Password) {
                return Keychain.Instance.GetPassword (Id);
            } else {
                return Password;
            }
        }

        public string GetAccessToken ()
        {
            if (null == AccessToken) {
                return Keychain.Instance.GetAccessToken (Id);
            } else {
                return AccessToken;
            }
        }

        public string GetRefreshToken ()
        {
            if (null == RefreshToken) {
                return Keychain.Instance.GetRefreshToken (Id);
            } else {
                return RefreshToken;
            }
        }

        public override int Delete ()
        {
            Keychain.Instance.DeletePassword (Id);
            Password = null;
            Update ();
            return base.Delete ();
        }

        public static List<McCred> QueryByCredType (CredTypeEnum credType)
        {
            return NcModel.Instance.Db.Query<McCred> (
                "SELECT * FROM McCred WHERE CredType = ?", credType);
        }

        public bool CanRefresh ()
        {
            switch (CredType) {
            case CredTypeEnum.Password:
                return false;
            case CredTypeEnum.OAuth2:
                return true;
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

        public void RefreshOAuth2 (Action<McCred> onSuccess, Action<McCred> onFailure, CancellationToken Token)
        {
            var account = McAccount.QueryById<McAccount> (AccountId);
            switch (account.AccountService) {
            case McAccount.AccountServiceEnum.GoogleDefault:
                var query = "client_secret=" + Uri.EscapeDataString (GoogleOAuthConstants.ClientSecret) +
                            "&grant_type=" + "refresh_token" +
                            "&refresh_token=" + Uri.EscapeDataString (GetRefreshToken ()) +
                            "&client_id=" + Uri.EscapeDataString (GoogleOAuthConstants.ClientId);
                var requestUri = new Uri ("https://www.googleapis.com/oauth2/v3/token" + "?" + query);
                var request = new NcHttpRequest (HttpMethod.Post, requestUri);
                int timeoutSecs = 30;

                NcHttpClient.Instance.SendRequest (request, timeoutSecs, ((response, token) => {
                    if (response.StatusCode != System.Net.HttpStatusCode.OK) {
                        Log.Error (Log.LOG_SYS, "OAUTH2 HTTP Status {0}", response.StatusCode.ToString ());
                        onFailure (this);
                        return;
                    }
                    var memStream = new MemoryStream ();
                    response.Content.CopyTo (memStream);
                    var jsonResponse = memStream.GetBuffer ().Take ((int)memStream.Length).ToArray ();
                    var decodedResponse = JsonConvert.DeserializeObject<OAuth2RefreshRespose> (Encoding.UTF8.GetString (jsonResponse));
                    if ("Bearer" != decodedResponse.token_type) {
                        Log.Error (Log.LOG_SYS, "Unknown OAUTH2 token_type {0}", decodedResponse.token_type);
                    }
                    if (null == decodedResponse.access_token || null == decodedResponse.expires_in) {
                        Log.Error (Log.LOG_SYS, "Missing OAUTH2 access_token {0} or expires_in {1}", decodedResponse.access_token, decodedResponse.expires_in);
                        onFailure (this);
                    }
                    Log.Info (Log.LOG_SYS, "OAUTH2 Token refreshed. expires_in={0}", decodedResponse.expires_in);
                    // also there's an ID token: http://stackoverflow.com/questions/8311836/how-to-identify-a-google-oauth2-user/13016081#13016081
                    UpdateOauth2 (decodedResponse.access_token, 
                        string.IsNullOrEmpty (decodedResponse.refresh_token) ? GetRefreshToken () : decodedResponse.refresh_token,
                        uint.Parse (decodedResponse.expires_in));
                    onSuccess (this);
                }), ((ex, token) => {
                    Log.Error (Log.LOG_SYS, "OAUTH2 Exception {0}", ex.ToString ());
                    onFailure (this);
                }), Token);
                break;

            default:
                Log.Error (Log.LOG_SYS, "Can not refresh {0}:{1}", account.Id, account.AccountService);
                return;
            }
        }
    }
}
