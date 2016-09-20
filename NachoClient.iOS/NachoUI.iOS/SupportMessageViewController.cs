// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using Foundation;
using UIKit;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using NachoPlatform;

namespace NachoClient.iOS
{
    public partial class SupportMessageViewController : NcUIViewControllerNoLeaks
    {

        UIScrollView scrollView;
        UIView contentView;
        NcUIBarButtonItem sendButton;

        protected nfloat yOffset;
        protected static nfloat CELL_HEIGHT = 44f;
        protected static nfloat LINE_OFFSET = 30f;
        protected static nfloat KEYBOARD_HEIGHT = 216f;
        protected static nfloat HORIZONTAL_PADDING = 12f;
        protected static nfloat INDENT = 18f;
        protected static nfloat VERTICAL_PADDING = 20f;

        protected const int MESSAGEBODY_VIEW_TAG = 100;
        protected const int CONTACT_TEXTFIELD_TAG = 101;

        protected const int GRAY_BACKGROUND_VIEW_TAG = 200;
        protected const int SENDING_SPINNER_TAG = 201;
        protected const double WAIT_TIMER_LENGTH = 12;

        protected NSTimer sendMessageTimer;
        protected bool hasDisplayedStatusMessage = false;
        protected bool problemWasChanged = false;

        public SupportMessageViewController () : base ()
        {
        }

        public SupportMessageViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        protected override void OnKeyboardChanged ()
        {
            LayoutView ();
        }

        protected void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_TelemetrySupportMessageReceived == s.Status.SubKind) {
                MessageReceived (true);
            }
        }

        public override void ViewDidLoad ()
        {
            scrollView = new UIScrollView (View.Bounds);
            contentView = new UIView (scrollView.Bounds);
            scrollView.AddSubview (contentView);
            View.AddSubview (scrollView);
            sendButton = new NcUIBarButtonItem ();
            base.ViewDidLoad ();
            NavigationItem.Title = "Contact Us";
        }

        protected override void CreateViewHierarchy ()
        {
            View.BackgroundColor = A.Color_NachoBackgroundGray;
            contentView.BackgroundColor = A.Color_NachoBackgroundGray;

            yOffset = VERTICAL_PADDING;

            UIView sectionOneView = new UIView (new CGRect (HORIZONTAL_PADDING, yOffset, View.Frame.Width - (HORIZONTAL_PADDING * 2), CELL_HEIGHT * 2));
            sectionOneView.Layer.BorderWidth = .5f;
            sectionOneView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            sectionOneView.BackgroundColor = UIColor.White;
            sectionOneView.Layer.CornerRadius = 4;

            UILabel sectionOneHeader = new UILabel (new CGRect (INDENT, 0, sectionOneView.Frame.Width - INDENT, CELL_HEIGHT));
            sectionOneHeader.Font = A.Font_AvenirNextRegular14;
            sectionOneHeader.TextColor = A.Color_NachoBlack;
            sectionOneHeader.Text = "How can we reach you?";
            sectionOneHeader.TextAlignment = UITextAlignment.Left;
            sectionOneView.AddSubview (sectionOneHeader);

            UIView sectionOneHR = new UIView (new CGRect (INDENT, sectionOneHeader.Frame.Bottom - .5f, sectionOneView.Frame.Width - INDENT, .5f));
            sectionOneHR.BackgroundColor = A.Color_NachoBorderGray;
            sectionOneView.AddSubview (sectionOneHR);

            UITextField sectionOneTextField = new UITextField (new CGRect (INDENT, sectionOneHR.Frame.Bottom, sectionOneView.Frame.Width - INDENT, CELL_HEIGHT));
            sectionOneTextField.Placeholder = "yourname@email.com";
            sectionOneTextField.BackgroundColor = sectionOneView.BackgroundColor;
            sectionOneTextField.Font = A.Font_AvenirNextMedium14;
            sectionOneTextField.KeyboardType = UIKeyboardType.EmailAddress;
            sectionOneTextField.AutocapitalizationType = UITextAutocapitalizationType.None;
            sectionOneTextField.AutocorrectionType = UITextAutocorrectionType.No;
            sectionOneTextField.Tag = CONTACT_TEXTFIELD_TAG;
            sectionOneTextField.Layer.CornerRadius = 4f;
            sectionOneView.AddSubview (sectionOneTextField);
            contentView.AddSubview (sectionOneView);

            yOffset = sectionOneView.Frame.Bottom + HORIZONTAL_PADDING;

            UIView sectionTwoView = new UIView (new CGRect (HORIZONTAL_PADDING, yOffset, View.Frame.Width - (HORIZONTAL_PADDING * 2), View.Frame.Height - yOffset - VERTICAL_PADDING));
            sectionTwoView.Layer.BorderWidth = .5f;
            sectionTwoView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            sectionTwoView.BackgroundColor = UIColor.White;
            sectionTwoView.Layer.CornerRadius = 4;

            UILabel sectionTwoHeader = new UILabel (new CGRect (INDENT, 0, sectionTwoView.Frame.Width - INDENT, CELL_HEIGHT));
            sectionTwoHeader.Font = A.Font_AvenirNextRegular14;
            sectionTwoHeader.TextColor = A.Color_NachoBlack;
            sectionTwoHeader.Text = "What can we help you with?";
            sectionTwoHeader.TextAlignment = UITextAlignment.Left;
            sectionTwoView.AddSubview (sectionTwoHeader);

            UIView sectionTwoHR = new UIView (new CGRect (INDENT, sectionTwoHeader.Frame.Bottom - .5f, sectionTwoView.Frame.Width - INDENT, .5f));
            sectionTwoHR.BackgroundColor = A.Color_NachoBorderGray;
            sectionTwoView.AddSubview (sectionTwoHR);

            UITextView sectionTwoTextView = new UITextView (new CGRect (INDENT - 4, sectionTwoHR.Frame.Bottom + 8, sectionTwoView.Frame.Width - INDENT, sectionTwoView.Frame.Height - CELL_HEIGHT - 8));
            sectionTwoTextView.Font = A.Font_AvenirNextMedium14;
            sectionTwoTextView.TextColor = UIColor.LightGray;
            sectionTwoTextView.Text = "Briefly describe what's going on.";
            sectionTwoTextView.Tag = MESSAGEBODY_VIEW_TAG;
            sectionTwoTextView.BackgroundColor = UIColor.White;
            sectionTwoTextView.ScrollEnabled = true;
            sectionTwoTextView.Changed += (object sender, EventArgs e) => {
                MessageBodySelectionChanged (sectionTwoTextView);
            };
            sectionTwoView.AddSubview (sectionTwoTextView);
            contentView.AddSubview (sectionTwoView);

            yOffset = sectionTwoTextView.Frame.Bottom;

            sectionOneTextField.ShouldReturn += ((textField) => {
                sectionTwoTextView.BecomeFirstResponder ();
                return true;
            });

            sectionTwoTextView.ShouldBeginEditing += ((textView) => {
                if (textView.TextColor == UIColor.LightGray) {
                    textView.Text = "";
                    textView.TextColor = A.Color_NachoBlack;
                    problemWasChanged = true;
                }
                return true;
            });

            sectionTwoTextView.ShouldEndEditing += ((textView) => {
                if (0 == textView.Text.Trim ().Length) {
                    sectionTwoTextView.TextColor = UIColor.LightGray;
                    sectionTwoTextView.Text = "Briefly describe what's going on.";
                    problemWasChanged = false;
                }
                textView.ResignFirstResponder ();
                return true;
            });

            scrollView.BackgroundColor = A.Color_NachoNowBackground;

            NavigationItem.Title = "Message Support";

            using (var image = UIImage.FromBundle ("modal-close")) {
                var DismissButton = new NcUIBarButtonItem (image, UIBarButtonItemStyle.Plain, (sender, args) => {
                    this.DismissViewController (true, null);
                });
                DismissButton.AccessibilityLabel = "Dismiss";
                NavigationItem.LeftBarButtonItem = DismissButton;
            }
          
            Util.SetAutomaticImageForButton (sendButton, "icn-send");
            sendButton.AccessibilityLabel = "Send";

            sendButton.Clicked += SendButtonClicked;

            NavigationItem.RightBarButtonItem = sendButton;

            UIView grayBackgroundView = new UIView (new CGRect (0, 0, View.Frame.Width, View.Frame.Height));
            grayBackgroundView.BackgroundColor = UIColor.DarkGray.ColorWithAlpha (.6f);
            grayBackgroundView.Tag = GRAY_BACKGROUND_VIEW_TAG;
            grayBackgroundView.Hidden = true;
            grayBackgroundView.Alpha = 0.0f;
            View.AddSubview (grayBackgroundView);

            UIView alertMimicView = new UIView (new CGRect (grayBackgroundView.Frame.Width / 2 - 90, grayBackgroundView.Frame.Height / 2 - 80, 180, 110));
            alertMimicView.BackgroundColor = UIColor.White;
            alertMimicView.Layer.CornerRadius = 6.0f;
            grayBackgroundView.AddSubview (alertMimicView);

            UILabel statusMessage = new UILabel (new CGRect (8, 10, alertMimicView.Frame.Width - 16, 25));
            statusMessage.BackgroundColor = UIColor.White;
            statusMessage.Alpha = 1.0f;
            statusMessage.Font = UIFont.SystemFontOfSize (17);
            statusMessage.TextColor = UIColor.Black;
            statusMessage.Text = "Sending Message";
            statusMessage.TextAlignment = UITextAlignment.Center;
            alertMimicView.AddSubview (statusMessage);

            UIActivityIndicatorView sendingActivityIndicator = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.WhiteLarge);
            sendingActivityIndicator.Frame = new CGRect (alertMimicView.Frame.Width / 2 - 20, statusMessage.Frame.Bottom + 15, 40, 40);
            sendingActivityIndicator.Color = A.Color_SystemBlue;
            sendingActivityIndicator.Alpha = 1.0f;
            sendingActivityIndicator.StartAnimating ();
            sendingActivityIndicator.Tag = SENDING_SPINNER_TAG;
            alertMimicView.AddSubview (sendingActivityIndicator);
        }

        protected void SendButtonClicked (object sender, EventArgs e)
        {
            View.EndEditing (true);

            UITextField contactInfoTextField = (UITextField)View.ViewWithTag (CONTACT_TEXTFIELD_TAG);
            UITextView messageInfoTextView = (UITextView)View.ViewWithTag (MESSAGEBODY_VIEW_TAG);

            if (!NachoCore.Utils.Network_Helpers.HasNetworkConnection ()) {
                NcAlertView.ShowMessage (this, "Network Error",
                    "A networking issue prevents this message from being sent. Please try again when you have a network connection.");
            } else if (string.IsNullOrEmpty (contactInfoTextField.Text)) {
                NcAlertView.ShowMessage (this, "Missing Contact Info",
                    "Please provide contact information, such as an email address.");
            } else if (!problemWasChanged || string.IsNullOrEmpty(messageInfoTextView.Text)) {
                NcAlertView.ShowMessage (this, "No Description",
                    "Please describe your reason for contacting Apollo Mail support, such as the problem that you encountered.");
            } else {
                sendMessageTimer = NSTimer.CreateScheduledTimer (WAIT_TIMER_LENGTH, delegate {
                    MessageReceived (false);
                });

                Dictionary<string,string> supportInfo = new Dictionary<string, string> ();
                supportInfo.Add ("ContactInfo", contactInfoTextField.Text);
                supportInfo.Add ("Message", messageInfoTextView.Text);
                supportInfo.Add ("BuildVersion", Build.BuildInfo.Version);
                supportInfo.Add ("BuildNumber", Build.BuildInfo.BuildNumber);

                NcApplication.Instance.TelemetryService.StartService ();
                // Close all JSON files so they can be immediately uploaded while the user enters the
                NcApplication.Instance.TelemetryService.FinalizeAll ();
                NcApplication.Instance.TelemetryService.RecordSupport (supportInfo, () => {
                    NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                        Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_TelemetrySupportMessageReceived),
                        Account = ConstMcAccount.NotAccountSpecific,
                    });
                });
                ToggleSpinnerView ();
            }
        }

        protected void ToggleSpinnerView ()
        {
            UIView grayBackgroundView = (UIView)View.ViewWithTag (GRAY_BACKGROUND_VIEW_TAG);
            UIActivityIndicatorView sendingActivityIndicator = (UIActivityIndicatorView)View.ViewWithTag (SENDING_SPINNER_TAG);

            grayBackgroundView.Hidden = !grayBackgroundView.Hidden;

            if (grayBackgroundView.Hidden) {
                sendingActivityIndicator.StopAnimating ();
                grayBackgroundView.Alpha = 0.0f;
            } else {
                UIView.Animate (.15, () => {
                    grayBackgroundView.Alpha = 1.0f;
                });
                sendingActivityIndicator.StartAnimating ();
            }
        }

        protected void LayoutView ()
        {
            scrollView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height - keyboardHeight);
            var contentFrame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height - VERTICAL_PADDING);
            contentView.Frame = contentFrame;
            scrollView.ContentSize = contentFrame.Size;
        }

        protected override void ConfigureAndLayout ()
        {
            UITextField contactText = (UITextField)View.ViewWithTag (CONTACT_TEXTFIELD_TAG);
            contactText.Text = GetEmailAddress ();

            LayoutView ();
        }

        protected string GetEmailAddress ()
        {
            var account = NcApplication.Instance.DefaultEmailAccount;
            if (account != null) {
                return account.EmailAddr;
            } else {
                return "";
            }
        }

        protected void MessageBodySelectionChanged (UITextView textView)
        {
            var caretRect = textView.GetCaretRectForPosition (textView.SelectedTextRange.End);
            caretRect.Size = new CGSize (caretRect.Size.Width, caretRect.Size.Height);

            var notesView = (UIView)contentView.ViewWithTag (MESSAGEBODY_VIEW_TAG);
            caretRect.Y += notesView.Frame.Y + KEYBOARD_HEIGHT;
            scrollView.ScrollRectToVisible (caretRect, true);
        }

        public void MessageReceived (bool didSend)
        {
            if (!hasDisplayedStatusMessage) {
                hasDisplayedStatusMessage = true;

                ToggleSpinnerView ();

                if (null != sendMessageTimer) {
                    sendMessageTimer.Dispose ();
                    sendMessageTimer = null;
                }

                if (didSend) {
                    NcAlertView.Show (this, "Message Sent",
                        "We have received your message and will respond shortly. Thank you for your feedback.",
                        new NcAlertAction ("Close", NcAlertActionStyle.Cancel, () => {
                            DismissViewController (true, null);
                        }));
                } else {
                    NcAlertView.Show (this, "Message Not Sent",
                        "There was a delay while sending the message. We will continue trying to send the message in the background.",
                        new NcAlertAction ("Close", NcAlertActionStyle.Cancel, () => {
                            DismissViewController (true, null);
                        }));
                }
            }
        }

        protected override void Cleanup ()
        {
            sendButton.Clicked -= SendButtonClicked;
            sendButton = null;
        }
    }
}
