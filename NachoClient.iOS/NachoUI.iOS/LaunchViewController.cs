// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public partial class LaunchViewController : UIViewController
    {

        AppDelegate appDelegate;

        private void EnterFullConfiguration () {
            // You will always need to supply user credentials (until certs, for sure).
            var cred = new NcCred () { Username = txtUserName.Text, Password = txtPassword.Text };
            appDelegate.Be.Db.Insert (BackEnd.DbActors.Ui, cred);
            // Once autodiscover is viable, you will only need to supply this server info IFF you get a callback.
            var server = new NcServer () { Fqdn = txtServerName.Text };
            appDelegate.Be.Db.Insert (BackEnd.DbActors.Ui, server);
            // In the near future, you won't need to create this protocol state object.
            var protocolState = new NcProtocolState ();
            appDelegate.Be.Db.Insert (BackEnd.DbActors.Ui, protocolState);
            var policy = new NcPolicy ();
            appDelegate.Be.Db.Insert (BackEnd.DbActors.Ui, policy);
            // You will always need to supply the user's email address.
            appDelegate.Account = new NcAccount () { EmailAddr = txtUserName.Text };
            // The account object is the "top", pointing to credential, server, and opaque protocol state.
            appDelegate.Account.CredId = cred.Id;
            appDelegate.Account.ServerId = server.Id;
            appDelegate.Account.ProtocolStateId = protocolState.Id;
            appDelegate.Account.PolicyId = policy.Id;
            appDelegate.Be.Db.Insert (BackEnd.DbActors.Ui, appDelegate.Account);
        }

        public override void ViewDidLoad()
        {
            // By the time we get here, we have launched a BE via appdelegate, so we should always be able to update
            // our app's DB by referencing the appdelegate BE
            // we want to gather any changes here, if its first time through we will need to gather a full set of configuration parameters
            // any other times we will want to update the info on a given field.


            base.ViewDidLoad();


            // Perform any additional setup after loading the view, typically from a nib.


            // listen for changes here
            getServerName ();
            getUserName ();
            getPassword ();
            // logic neeeded here so that any change in the fields kicks off a new update in the BE

        }
        partial void btnLaunchAcct (MonoTouch.Foundation.NSObject sender){
            EnterFullConfiguration();
            appDelegate.Be.Start ();
        
            PerformSegue("launchAccountSegue", this);

        }

        void getServerName(){
            // add all logic to ensure that any change in the field is updated .. unless cancel hit
            this.txtServerName.ShouldReturn += (textField) => { 
                if (txtServerName.Text.Contains("Hello")){
                    Console.WriteLine("Hello"); 
                }
                //ncServername = txtServerName.Text;
                //Console.WriteLine(ncServername);

                textField.ResignFirstResponder(); 
                return true;
            };
        }
        void getUserName(){
            this.txtUserName.ShouldReturn += (textField) => { 
                if (txtUserName.Text.Contains("Hello")){
                    Console.WriteLine("Hello"); 
                }
                //ncUserName = txtUserName.Text;
                //Console.WriteLine(ncPassword);

                textField.ResignFirstResponder(); 
                return true;
            };
        }
        void getPassword(){
            this.txtPassword.ShouldReturn += (textField) => { 
                if (txtPassword.Text.Contains("Hello")){
                    Console.WriteLine("Hello"); 
                }
                //ncPassword = txtPassword.Text;
                //Console.WriteLine(ncPassword);

                textField.ResignFirstResponder(); 
                return true;
            };
        }
        public LaunchViewController (IntPtr handle) : base (handle)
        {
            appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;

        
        }
    }
}
