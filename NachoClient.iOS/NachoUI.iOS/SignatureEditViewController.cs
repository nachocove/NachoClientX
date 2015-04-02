// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using UIKit;
using CoreGraphics;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class SignatureEditViewController : NcUIViewControllerNoLeaks
    {
        UINavigationBar navbar = new UINavigationBar ();
        protected UIBarButtonItem saveButton;
        protected UIBarButtonItem cancelButton;

        protected const int SIGNATURE_TEXT_VIEW_TAG = 100;

        protected const int DISMISS_CHANGES_ALERT_VIEW_TAG = 200;

        protected string originalSignature;

        public SignatureEditViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidAppear (bool animated)
        {
            CaptureOriginalSignature ();
            base.ViewDidAppear (animated);
        }

        protected void CaptureOriginalSignature ()
        {
            McAccount theAccount = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
            originalSignature = theAccount.Signature ?? "";
        }

        protected bool DidUserEditSignature ()
        {
            var signatureTextView = (UITextView)View.ViewWithTag (SIGNATURE_TEXT_VIEW_TAG);
            return (signatureTextView.Text != originalSignature);
        }

        protected override void CreateViewHierarchy ()
        {
            navbar.Frame = new CGRect (0, 0, View.Frame.Width, 64);
            View.Add (navbar);
            navbar.BackgroundColor = A.Color_NachoGreen;
            navbar.Translucent = false;
            UINavigationItem title = new UINavigationItem ("Signature");
            navbar.SetItems (new UINavigationItem[]{ title }, false);
            cancelButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (cancelButton, "icn-close");

            navbar.TopItem.LeftBarButtonItem = cancelButton;
            cancelButton.Clicked += CancelButtonClicked;

            saveButton = new UIBarButtonItem ();
            saveButton.Style = UIBarButtonItemStyle.Done;
            saveButton.Title = "Done";
            saveButton.Clicked += SaveButtonClicked;

            navbar.TopItem.RightBarButtonItem = saveButton;

            View.BackgroundColor = A.Color_NachoBackgroundGray;

            nfloat yOffset = navbar.Frame.Bottom + 20;
            UITextView signatureTextView = new UITextView (new CGRect (0, yOffset, View.Frame.Width, 150));
            signatureTextView.Font = A.Font_AvenirNextRegular14;
            signatureTextView.Tag = SIGNATURE_TEXT_VIEW_TAG;

            View.Add (signatureTextView);

            signatureTextView.BecomeFirstResponder ();
        }

        protected override void ConfigureAndLayout ()
        {
            McAccount theAccount = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
            var signatureTextView = (UITextView)View.ViewWithTag (SIGNATURE_TEXT_VIEW_TAG);
            signatureTextView.Text = theAccount.Signature ?? "";
        }

        protected override void Cleanup ()
        {
            saveButton.Clicked -= SaveButtonClicked;
            cancelButton.Clicked -= CancelButtonClicked;

            saveButton = null;
            cancelButton = null;
        }

        protected void SaveButtonClicked (object sender, EventArgs e)
        {
            McAccount theAccount = McAccount.QueryById <McAccount> (LoginHelpers.GetCurrentAccountId ());
            var signatureTextView = (UITextView)View.ViewWithTag (SIGNATURE_TEXT_VIEW_TAG);

            theAccount.Signature = signatureTextView.Text.Trim ();
            theAccount.Update ();
            DismissViewController (true, null);
        }

        protected void CancelButtonClicked (object sender, EventArgs e)
        {
            if (!DidUserEditSignature ()) {
                DismissViewController (true, null);
                return;
            }
            // Make sure user wants to abandon changes
            NcAlertView.Show (this, "Dismiss Changes", "If you leave this screen, your changes will not be saved.",
                new NcAlertAction ("OK", NcAlertActionStyle.Destructive, () => {
                    DismissViewController (true, null);
                }),
                new NcAlertAction ("Cancel", NcAlertActionStyle.Cancel, null));
        }

    }
}
