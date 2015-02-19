// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using UIKit;
using CoreGraphics;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public partial class SignatureEditViewController : NcUIViewControllerNoLeaks
    {
        UINavigationBar navbar = new UINavigationBar();
        protected UIBarButtonItem saveButton;
        protected UIBarButtonItem cancelButton;

        protected const int SIGNATURE_TEXT_VIEW_TAG = 100;

        protected const int DISMISS_CHANGES_ALERT_VIEW_TAG = 200;

        protected string ORIGINAL_SIGNATURE_VALUE = "";

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
            McAccount theAccount = McAccount.QueryById<McAccount>(LoginHelpers.GetCurrentAccountId());

            if (!string.IsNullOrEmpty (theAccount.Signature)) {
                ORIGINAL_SIGNATURE_VALUE = theAccount.Signature;
            }
        }

        protected bool DidUserEditSignature ()
        {
            var signatureTextView = (UITextView)View.ViewWithTag (SIGNATURE_TEXT_VIEW_TAG);

            return (signatureTextView.Text != ORIGINAL_SIGNATURE_VALUE);
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

            navbar.TopItem.RightBarButtonItem = saveButton;

            View.BackgroundColor = A.Color_NachoBackgroundGray;

            nfloat yOffset = navbar.Frame.Bottom + 20;
            UITextView signatureTextView = new UITextView (new CGRect (0, yOffset, View.Frame.Width, 150));
            signatureTextView.Font = A.Font_AvenirNextRegular14;
            signatureTextView.Tag = SIGNATURE_TEXT_VIEW_TAG;

            saveButton.Clicked += SaveButtonClicked;

            View.Add (signatureTextView);

            signatureTextView.BecomeFirstResponder ();
        }

        protected override void Cleanup ()
        {
            saveButton.Clicked -= SaveButtonClicked;
            cancelButton.Clicked -= CancelButtonClicked;

            saveButton = null;
            cancelButton = null;

            var dismissChangesAlertView = (UIAlertView)View.ViewWithTag (DISMISS_CHANGES_ALERT_VIEW_TAG);
            if (null != dismissChangesAlertView) {
                dismissChangesAlertView.Clicked -= DismissChangesClicked;
                dismissChangesAlertView = null;
            }
        }

        protected void DismissChangesClicked (object sender, UIButtonEventArgs b)
        {
            if (b.ButtonIndex == 0) {
                DismissViewController (true, null);
            }
        }

        protected void SaveButtonClicked (object sender, EventArgs e)
        {
            McAccount theAccount = McAccount.QueryById <McAccount> (LoginHelpers.GetCurrentAccountId ());
            var signatureTextView = (UITextView)View.ViewWithTag (SIGNATURE_TEXT_VIEW_TAG);

            theAccount.Signature = signatureTextView.Text.Trim();
            theAccount.Update();
            DismissViewController (true, null);
        }

        protected void CancelButtonClicked (object sender, EventArgs e)
        {
            if (!DidUserEditSignature ()) {
                DismissViewController (true, null);
            } else {
                UIAlertView dismissChanges = new UIAlertView ("Dismiss Changes", "If you leave this screen your changes will not be saved.", null, "Ok", "Cancel");
                dismissChanges.Tag = DISMISS_CHANGES_ALERT_VIEW_TAG;
                dismissChanges.Clicked += DismissChangesClicked;
                dismissChanges.Show ();
            }
        }

        protected override void ConfigureAndLayout ()
        {
            var signatureTextView = (UITextView)View.ViewWithTag (SIGNATURE_TEXT_VIEW_TAG);
            McAccount theAccount = McAccount.QueryById<McAccount>(LoginHelpers.GetCurrentAccountId());

            if (!string.IsNullOrEmpty (theAccount.Signature)) {
                signatureTextView.Text = theAccount.Signature;
            }
        }
    }
}
