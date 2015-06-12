using System;
using SQLite;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.Model
{
    public class McCred : McAbstrObjectPerAcc
    {
        public enum CredTypeEnum { Password = 0, OAuth2 };

        public CredTypeEnum CredType { get; set; }

        public string Username { get; set; }

        // We want to remember if the user entered their
        // own username or if we copied in the email address.
        public bool UserSpecifiedUsername { get; set; }

        /// <summary>
        /// DO NOT ACCESS private properties. Use UpdateXxx/GetXxx.
        /// private properties are here for SQLite.Net only!
        /// </summary>
        /// <value>The password.</value>
        private string Password { get; set; }

        private string AccessToken { get; set; }

        private string RefreshToken { get; set; }

        public DateTime Expiry { get; set; }

        public string RectificationUrl { get; set; }

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
    }
}
