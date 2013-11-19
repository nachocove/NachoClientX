// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public partial class ComposeViewController : UIViewController
    
    {
        /*
         * This is the basic compose email viewer code. It will be reachable via a number of paths (compose buttons). It
         * is a navigation controller (ie. every segue will be a "push".
         * FIXME- Need to store curent email message to drafts folder if send button not hit, but cancel is.
         *     - no dialogue button the storage to drafts, clean out drafts every 48 hours or some such in backend.
         * FIXME - need to do parsing for valid email address formats on txtToField
         * FIXME - need to ensure that screen scrolls on long email messages
         * FIXME - need to have "default text in compose message be a "format, and not actually in text field
         * 
         * /
         */

        private string ncToList;
        AppDelegate appDelegate { get; set; }


            

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            getToList();
            getSubject ();

        }
        void getToList(){
            // add all logic to ensure that any change in the field is updated .. unless cancel hit
            txtToField.EditingDidBegin += delegate {
                txtComposeMsg.ResignFirstResponder ();
            };
            this.txtToField.ShouldReturn += (textField) => { 
                if (txtToField.Text.Contains ("Hello")) {
                    Console.WriteLine ("Hello");    
                }
                Console.WriteLine (txtToField.Text);

                // Add checks to make sure these are valid email address formats...
                // evntually add syn to contacts DB to get more info and "help with autocomplete

                textField.ResignFirstResponder (); 
                return true;
            };

        }

        public override void TouchesBegan (NSSet touches, UIEvent evt)
        {
            this.ResignFirstResponder();
        }

        /*
         * protected override void OnKeyboardChanged (bool visible, float height)
        {
            //We "center" the popup when the keyboard appears/disappears
            var frame = container.Frame;
            if (visible)
                frame.Y -= height / 2;
            else
                frame.Y += height / 2;
            container.Frame = frame;
        }*/
        /*
            // to get keyboard up...
            txtMfg.EditingDidBegin += delegate {
                 txtDescription.ResignFirstResponder();
            }


         */


        void getSubject(){
            // add all logic to ensure that any change in the field is updated .. unless cancel hit
            txtSubjectField.EditingDidBegin += delegate {
                txtComposeMsg.ResignFirstResponder ();
            };
            this.txtSubjectField.ShouldReturn += (textField) => { 
                if (txtSubjectField.Text.Contains ("Hello")) {
                    Console.WriteLine ("Hello");    
                }
                Console.WriteLine (txtSubjectField.Text);

                //make sure autocorrect is on in this field

                textField.ResignFirstResponder (); 
                return true;
            };
        
        }

        void getMessage(){
            // add all logic to ensure that any change in the field is updated .. unless cancel hit

            this.txtComposeMsg.ShouldBeginEditing += (textField) => { 
                if (txtSubjectField.Text.Contains ("Hello")) {
                    Console.WriteLine ("Hello");    
                }
                Console.WriteLine (txtComposeMsg.Text);

                //make sure autocorrect is on in this field

                textField.ResignFirstResponder (); 
                return true;
            };

        }

        partial void btnSendEmail (MonoTouch.Foundation.NSObject sender){
            Console.WriteLine("Sending email");
            txtComposeMsg.ResignFirstResponder ();
        

            var email = new NcEmailMessage () {
                AccountId = appDelegate.Account.Id,
                To = txtToField.Text,
                From = appDelegate.Account.EmailAddr,

                Subject = txtSubjectField.Text,
                Body = txtComposeMsg.Text,
                IsAwatingSend = true
            };
            appDelegate.Be.Db.Insert(BackEnd.DbActors.Ui, email);
            // close this view and go back
            //
            this.NavigationController.PopViewControllerAnimated(true);

        }


        public ComposeViewController (IntPtr handle) : base (handle)
        {
            appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
        }
    }
}
