// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;
using NachoPlatform;
using NachoClient.iOS;

namespace NachoClient.iOS
{
    public partial class SupportMessageViewController : NcUIViewController
    {
        protected float yOffset;
        protected static float CELL_HEIGHT = 44f;
        protected static float LINE_OFFSET = 30f;
        protected static float KEYBOARD_HEIGHT = 216f;
        protected static float MESSAGEBODY_TEXTVIEW_HEIGHT = UIScreen.MainScreen.Bounds.Height - KEYBOARD_HEIGHT - LINE_OFFSET - 10;

        public SupportMessageViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            CreateView ();
            LayoutView ();
            ConfigureView ();
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

        protected void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_TelemerySupportMessageReceived == s.Status.SubKind) {
                MessageReceived ();
            }
        }

        protected const int MESSAGEBODY_VIEW_TAG = 100;
        protected const int CONTACT_TEXTFIELD_TAG = 101;
        public void CreateView ()
        {
            UITextField contactTextField = new UITextField ();
            UITextView messageBodyTextView = new UITextView (new RectangleF (10, 5, View.Frame.Width - 30, MESSAGEBODY_TEXTVIEW_HEIGHT));

            yOffset = 0;

            navigationBar.Frame = new RectangleF (0, 0, View.Frame.Width, 64);
            navigationBar.Alpha = 1.0f;
            navigationBar.Opaque = true;
            navigationBar.BackgroundColor = A.Color_NachoGreen.ColorWithAlpha (1.0f);
            navigationBar.BarTintColor = A.Color_NachoGreen;
            UINavigationItem navItems = new UINavigationItem ("Contact Us");
            cancelButton.Title = "Back";
            cancelButton.Clicked += (object sender, EventArgs e) => {
                this.DismissViewController (true, null);
            };
            navItems.LeftBarButtonItem = cancelButton;
            sendButton.Title = "Submit";


            sendButton.Clicked += (object sender, EventArgs e) => {
                Dictionary<string,string> supportInfo = new Dictionary<string, string> ();
                supportInfo.Add ("ContactInfo", contactTextField.Text);
                supportInfo.Add ("Message", messageBodyTextView.Text);
                Telemetry.RecordSupport (supportInfo, () => {
                    NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                        Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_TelemerySupportMessageReceived),
                        Account = ConstMcAccount.NotAccountSpecific,
                    });
                });
            };
            navItems.RightBarButtonItem = sendButton;
            navigationBar.Items = new UINavigationItem[]{ navItems };
            View.Add (navigationBar);

            yOffset = navigationBar.Frame.Bottom;

            UIView sectionOne = new UIView (new RectangleF (0, yOffset, View.Frame.Width, 35));
            sectionOne.BackgroundColor = A.Color_NachoNowBackground;

            UILabel sectionOneHeader = new UILabel (new RectangleF (15, 0, sectionOne.Frame.Width - 15, sectionOne.Frame.Height));
            sectionOneHeader.Font = A.Font_AvenirNextRegular14;
            sectionOneHeader.TextColor = A.Color_NachoBlack;
            sectionOneHeader.Text = "How can we reach you?";
            sectionOneHeader.TextAlignment = UITextAlignment.Left;
            sectionOne.Add (sectionOneHeader);
            contentView.AddSubview (sectionOne);

            yOffset = sectionOne.Frame.Bottom;

            contactTextField.Frame = new RectangleF (15, yOffset, View.Frame.Width - 15, CELL_HEIGHT);
            contactTextField.Placeholder = "Email address or phone number...";
            contactTextField.BackgroundColor = UIColor.White;
            contactTextField.Font = A.Font_AvenirNextRegular14;
            contactTextField.KeyboardType = UIKeyboardType.EmailAddress;
            contactTextField.AutocapitalizationType = UITextAutocapitalizationType.None;
            contactTextField.AutocorrectionType = UITextAutocorrectionType.No;
            contactTextField.Tag = CONTACT_TEXTFIELD_TAG;
            contentView.AddSubview (contactTextField);

            yOffset = contactTextField.Frame.Bottom;

            UIView horizontalBottomBorder = new UIView (new RectangleF (0, yOffset - .5f, View.Frame.Width, .5f));
            horizontalBottomBorder.BackgroundColor = A.Color_NachoSeparator;
            contentView.AddSubview (horizontalBottomBorder);

            UIView sectionTwo = new UIView (new RectangleF (0, yOffset, View.Frame.Width, 35));
            sectionTwo.BackgroundColor = A.Color_NachoNowBackground;

            UILabel sectionTwoHeader = new UILabel (new RectangleF (15, 0, sectionTwo.Frame.Width - 15, sectionTwo.Frame.Height));
            sectionTwoHeader.Font = A.Font_AvenirNextRegular14;
            sectionTwoHeader.TextColor = A.Color_NachoBlack;
            sectionTwoHeader.Text = "What can we help you with?";
            sectionTwoHeader.TextAlignment = UITextAlignment.Left;
            sectionTwo.Add (sectionTwoHeader);
            contentView.AddSubview (sectionTwo);

            yOffset = sectionTwo.Frame.Bottom;

            UIView messageBodyView = new UIView (new RectangleF (0, yOffset, View.Frame.Width, MESSAGEBODY_TEXTVIEW_HEIGHT + 200));
            messageBodyView.Tag = MESSAGEBODY_VIEW_TAG;
            messageBodyView.BackgroundColor = UIColor.White;
            messageBodyTextView.Font = A.Font_AvenirNextRegular14;
            messageBodyTextView.TextColor = UIColor.LightGray;
            messageBodyTextView.Text = "Briefly describe what's going on...";
            messageBodyTextView.BackgroundColor = UIColor.White;
            var beginningRange = new NSRange (0, 0);
            messageBodyTextView.SelectedRange = beginningRange;
            messageBodyTextView.Changed += (object sender, EventArgs e) => {
                MessageBodySelectionChanged (messageBodyTextView);
            };
            messageBodyView.Add (messageBodyTextView);
            contentView.AddSubview (messageBodyView);

            yOffset = messageBodyTextView.Frame.Bottom;

            contactTextField.ShouldReturn += ((textField) => {
                messageBodyTextView.BecomeFirstResponder ();
                return true;
            });

            messageBodyTextView.ShouldBeginEditing += ((textView) => {
                if (textView.TextColor == UIColor.LightGray) {
                    textView.Text = "";
                    textView.TextColor = A.Color_NachoBlack;
                }
                return true;
            });

            messageBodyTextView.ShouldEndEditing += ((textView) => {
                if (0 == textView.Text.Trim ().Length) {
                    messageBodyTextView.TextColor = UIColor.LightGray;
                    messageBodyTextView.Text = "Briefly describe what's going on...";
                }
                return true;
            });

            scrollView.BackgroundColor = A.Color_NachoNowBackground;
        }

        protected void LayoutView ()
        {
            scrollView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height - KEYBOARD_HEIGHT);
            var contentFrame = new RectangleF (0, 0, View.Frame.Width, MESSAGEBODY_TEXTVIEW_HEIGHT + LINE_OFFSET + 10);
            contentView.Frame = contentFrame;
            scrollView.ContentSize = contentFrame.Size;
        }

        protected void ConfigureView ()
        {
            UITextField contactText = (UITextField)contentView.ViewWithTag (CONTACT_TEXTFIELD_TAG);
            contactText.Text = GetEmailAddress ();
        }

        protected string GetEmailAddress ()
        {
            if (LoginHelpers.IsCurrentAccountSet ()) {
                McAccount Account = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
                return Account.EmailAddr;
            } else {
                return "";
            }
        }

        protected void MessageBodySelectionChanged (UITextView textView)
        {
            // We want to scroll the caret rect into view
            var caretRect = textView.GetCaretRectForPosition (textView.SelectedTextRange.end);
            caretRect.Size = new SizeF (caretRect.Size.Width, caretRect.Size.Height + textView.TextContainerInset.Bottom);
            // Make sure our textview is big enough to hold the text
            var frame = textView.Frame;
            frame.Size = new SizeF (textView.ContentSize.Width, textView.ContentSize.Height);
            textView.Frame = frame;
            var notesView = (UIView)contentView.ViewWithTag (MESSAGEBODY_VIEW_TAG);
            var newNotesViewFrame = notesView.Frame;
            newNotesViewFrame.Size = new SizeF (notesView.Frame.Width, textView.ContentSize.Height + 250);
            notesView.Frame = newNotesViewFrame;
            // And update our enclosing scrollview for the new content size
            scrollView.ContentSize = new SizeF (scrollView.ContentSize.Width, textView.Frame.Height + notesView.Frame.Y + 30);
            // Adjust the caretRect to be in our enclosing scrollview, and then scroll it
            caretRect.Y += notesView.Frame.Y + 30;
            scrollView.ScrollRectToVisible (caretRect, true);
        }

        public void MessageReceived()
        {
            UIAlertView confirmSent = new UIAlertView();
            confirmSent.Title = "Message Successfully Sent";
            confirmSent.Message = "We have received your message and will respond as quickly as possible. Thank you for your feedback.";
            confirmSent.AddButton("Close");
            confirmSent.Clicked += (object sender, UIButtonEventArgs e) => {
                this.DismissViewController (true, null);
            };
            confirmSent.Show ();
        }
    }
}
