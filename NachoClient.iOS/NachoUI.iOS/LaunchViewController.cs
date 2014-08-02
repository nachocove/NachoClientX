// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class LaunchViewController : NcUIViewController
    {
        AppDelegate appDelegate;

        private void EnterFullConfiguration () {
            NcModel.Instance.RunInTransaction (() => {
                // Need to regex-validate UI inputs.
                // You will always need to supply user credentials (until certs, for sure).

                var cred = new McCred () { Username = txtUserName.Text, Password = txtPassword.Text };
                cred.Insert ();
                int serverId = 0;
                if (string.Empty != txtServerName.Text) {
                    var server = new McServer () { Host = txtServerName.Text };
                    server.Insert ();
                    serverId = server.Id;
                }
                // You will always need to supply the user's email address.
                appDelegate.Account = new McAccount () { EmailAddr = txtUserName.Text };
                // The account object is the "top", pointing to credential, server, and opaque protocol state.
                appDelegate.Account.CredId = cred.Id;
                appDelegate.Account.ServerId = serverId;
                appDelegate.Account.Insert ();
            });
            BackEnd.Instance.Start (appDelegate.Account.Id);
        }

        public override void ViewDidLoad()
        {
            // By the time we get here, we have launched a BE via appdelegate, so we should always be able to update
            // our app's DB by referencing the appdelegate BE
            // we want to gather any changes here, if its first time through we will need to gather a full set of configuration parameters
            // any other times we will want to update the info on a given field.

            base.ViewDidLoad();
            stylizeFormControls();

            // Perform any additional setup after loading the view, typically from a nib.

            // TODO: Add reveal button
            // No going back from here.
            NavigationItem.SetHidesBackButton (true, true);

            // listen for changes here
            getServerName ();
            getUserName ();
            getPassword ();
            // logic neeeded here so that any change in the fields kicks off a new update in the BE

        }
        partial void btnLaunchAcct (MonoTouch.Foundation.NSObject sender){
            EnterFullConfiguration();

            //[self dismissViewControllerAnimated:TRUE completion:nil];
            DismissViewController(true, null);
        }
    
        public void stylizeFormControls()
        {
            View.BackgroundColor = A.Color_NachoGreen;
            SignInView signInView = new SignInView(new System.Drawing.RectangleF(View.Frame.X, View.Frame.Y,View.Frame.Width, View.Frame.Height));

            addSplashTriangle (signInView);
            addNachoLogo (signInView);
            formatUserName (signInView);
            formatPassword (signInView);
            configureAndAddSubmitButton (signInView);
            signInView.addStartLabel ();
            configureAndAddAdvancedButton (signInView);
            View.Add (signInView);
            txtServerName.Hidden = true;
        }

        void addSplashTriangle(SignInView view)
        {
            UIImageView triangleSplash = new UIImageView (UIImage.FromBundle ("Splash-BG"));
            triangleSplash.Frame = new System.Drawing.RectangleF (0, View.Frame.Height - 693, triangleSplash.Frame.Width, triangleSplash.Frame.Height);
            view.Add (triangleSplash);
        }
       
        void addNachoLogo(SignInView view)
        {
            UIImageView nachoLogo = new UIImageView (UIImage.FromBundle ("iPhoneIcon"));
            nachoLogo.Frame = new System.Drawing.RectangleF (View.Frame.Width/2 - 43f, 34, 86, 86);
            nachoLogo.Alpha = 1;
            nachoLogo.Layer.CornerRadius = 86 / 2f;
            nachoLogo.Layer.MasksToBounds = true;
            nachoLogo.Layer.BorderColor = UIColor.LightGray.CGColor;
            nachoLogo.Layer.BorderWidth = .15f;
            nachoLogo.Layer.ShadowRadius = 8;
            nachoLogo.Layer.ShadowOffset = new System.Drawing.SizeF (0, 1);
            nachoLogo.Layer.ShadowColor = UIColor.Black.CGColor;
            view.Add (nachoLogo);
        }
        void formatUserName(SignInView view)
        {
            txtUserName.BorderStyle = UITextBorderStyle.None;
            txtUserName.TextAlignment = UITextAlignment.Left;
            view.AddEmailField (txtUserName);
        }

        void formatPassword(SignInView view)
        {
            txtPassword.BorderStyle = UITextBorderStyle.None;
            txtPassword.TextAlignment = UITextAlignment.Left;
            view.AddPasswordField (txtPassword);
        }

        void configureAndAddSubmitButton(SignInView view)
        {
            UIButton submitButton = new UIButton (new System.Drawing.RectangleF (25, View.Frame.Height / 2 + 11, View.Frame.Width - 50, 45));
            submitButton.TouchUpInside += delegate {

                if(txtUserName.Text.Length == 0 || txtPassword.Text.Length == 0)
                {
                    if(txtUserName.Text.Length == 0){
                        txtUserName.Text = "Required";
                        txtUserName.TextColor = A.Color_NachoRed;
                    }
                    if(txtPassword.Text.Length == 0){
                    }
                }else{
                    EnterFullConfiguration();
                    DismissViewController(true, null);
                }
            };
            view.configureSubmitButton (submitButton);
        }

        void configureAndAddAdvancedButton(SignInView view)
        {
            UIButton advancedSignInButton = new UIButton ();
            view.configureAdvancedButton (advancedSignInButton);
            advancedSignInButton.TouchUpInside += (object sender, EventArgs e) => {
                //FIXME add segue to Storyboard
                //PerformSegue("LaunchToAdvancedLogin", this);
            };
        }

        void getServerName(){
            // add all logic to ensure that any change in the field is updated .. unless cancel hit
            this.txtServerName.ShouldReturn += (textField) => { 
                if (txtServerName.Text.Contains("Hello")){
                    Log.Info (Log.LOG_UI, "Hello"); 
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
                    Log.Info (Log.LOG_UI, "Hello"); 
                }
                //ncUserName = txtUserName.Text;
                //Console.WriteLine(ncPassword);
                if(textField.Text != "Required"){
                    textField.TextColor = UIColor.Black;
                }
                textField.ResignFirstResponder(); 
                return true;
            };
        }
        void getPassword(){
            this.txtPassword.ShouldReturn += (textField) => { 

                if (txtPassword.Text.Contains("Hello")){
                    Log.Info (Log.LOG_UI, "Hello"); 
                }
                //ncPassword = txtPassword.Text;
                //Console.WriteLine(ncPassword);
                textField.ResignFirstResponder(); 
                return true;
            };
        }
          //FIXME need to add back in once AdvancedLogin has been merged to Master
//        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
//        {
//            if (segue.Identifier == "StartupToAdvancedLogin") {
//                var AdvancedView = (AdvancedLoginViewController)segue.DestinationViewController; //our destination
//                AdvancedView.setBEState (false);
//            }
//        }
        public LaunchViewController (IntPtr handle) : base (handle)
        {
            appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
        }
    }
}
