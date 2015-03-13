using System;
using SQLite;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.Model
{
    public class McCred : McAbstrObjectPerAcc
    {
        public string Username { get; set; }

        // We want to remember if the user entered their
        // own username or if we copied in the email address.
        public bool UserSpecifiedUsername { get; set; }

        /// <summary>
        /// DO NOT ACCESS. Use UpdatePassword/GetPassword.
        /// Property is here for SQLite.Net only!
        /// </summary>
        /// <value>The password.</value>
        private string Password { get; set; }

        /// <summary>
        /// The semantics are that the storage is written, whether in device keychain or DB.
        /// The Id must be valid (Insert()ed) before this API is called.
        /// </summary>
        /// <param name="password">Password.</param>
        public void UpdatePassword (string password)
        {
            NcAssert.True (0 != Id);
            if (Keychain.Instance.HasKeychain ()) {
                Keychain.Instance.SetPassword (Id, password);
                Password = null;
                Update ();
            } else {
                Password = password;
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
