using Android.OS;
using Android.Views;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V7.App;
using Android.Support.V4.Widget;
using Android.Widget;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class CredentialsFragment : Fragment
    {
        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            // Create your fragment here
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var rootView = inflater.Inflate (Resource.Layout.CredentialsFragment, container, false);

            rootView.FindViewById<Button> (Resource.Id.btnConnect).Click += (object sender, System.EventArgs e) => {
 
                var txtServer = rootView.FindViewById<TextView> (Resource.Id.user_server).Text;
                var txtUsername = rootView.FindViewById<TextView> (Resource.Id.user_email).Text;
                var txtPassword = rootView.FindViewById<TextView> (Resource.Id.user_password).Text;

                var cred = new McCred () { Username = txtUsername, Password = txtPassword };
                BackEnd.Instance.Db.Insert (cred);
                // Once autodiscover is viable, you will only need to supply this server info IFF you get a callback.
                var server = new McServer () { Fqdn = txtServer };
                BackEnd.Instance.Db.Insert (server);
                // In the near future, you won't need to create this protocol state object.
                var protocolState = new McProtocolState ();
                BackEnd.Instance.Db.Insert (protocolState);
                var policy = new McPolicy ();
                BackEnd.Instance.Db.Insert (policy);
                // You will always need to supply the user's email address.
                var Account = new McAccount () { EmailAddr = txtUsername };
                // The account object is the "top", pointing to credential, server, and opaque protocol state.
                Account.CredId = cred.Id;
                Account.ServerId = server.Id;
                Account.ProtocolStateId = protocolState.Id;
                Account.PolicyId = policy.Id;
                BackEnd.Instance.Db.Insert (Account);
                NcBackendOwner.Instance.Account = Account;
                NcBackendOwner.Instance.LaunchBackEnd();
                // Clean up UI
                Activity.SupportFragmentManager.BeginTransaction().Remove(this).Commit();
            };

            return rootView;
        }
    }
}

