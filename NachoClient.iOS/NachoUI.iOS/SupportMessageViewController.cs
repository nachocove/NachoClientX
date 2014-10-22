// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class SupportMessageViewController : NcUIViewController
    {
        protected float yOffset;
        protected static float CELL_HEIGHT = 44f;
        protected static float LINE_OFFSET = 30f;
        protected static float KEYBOARD_HEIGHT = 216f;
        protected const float HORIZONTAL_PADDING = 15f;
        protected const float VERTICAL_PADDING = 20f;
        protected float keyboardHeight;

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

            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, OnKeyboardNotification);
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, OnKeyboardNotification);
            }
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;

            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillHideNotification);
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillShowNotification);
            }
        }

        public virtual bool HandlesKeyboardNotifications {
            get { return true; }
        }

        private void OnKeyboardNotification (NSNotification notification)
        {
            if (IsViewLoaded) {
                //Check if the keyboard is becoming visible
                bool visible = notification.Name == UIKeyboard.WillShowNotification;

                bool landscape = InterfaceOrientation == UIInterfaceOrientation.LandscapeLeft || InterfaceOrientation == UIInterfaceOrientation.LandscapeRight;
                if (visible) {
                    var keyboardFrame = UIKeyboard.FrameEndFromNotification (notification);
                    OnKeyboardChanged (visible, landscape ? keyboardFrame.Width : keyboardFrame.Height);
                } else {
                    var keyboardFrame = UIKeyboard.FrameBeginFromNotification (notification);
                    OnKeyboardChanged (visible, landscape ? keyboardFrame.Width : keyboardFrame.Height);
                }
            }
        }

        protected virtual void OnKeyboardChanged (bool visible, float height)
        {
            var newHeight = (visible ? height : 0);

            if (newHeight == keyboardHeight) {
                return;
            }
            keyboardHeight = newHeight;

            LayoutView ();
        }

        protected void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_TelemetrySupportMessageReceived == s.Status.SubKind) {
                MessageReceived ();
            }
        }

        protected const int MESSAGEBODY_VIEW_TAG = 100;
        protected const int CONTACT_TEXTFIELD_TAG = 101;

        public void CreateView ()
        {
            View.BackgroundColor = A.Color_NachoNowBackground;
            contentView.BackgroundColor = A.Color_NachoNowBackground;

            navigationBar.Frame = new RectangleF (0, 0, View.Frame.Width, 64);
            navigationBar.Alpha = 1.0f;
            navigationBar.Opaque = true;
            navigationBar.BackgroundColor = A.Color_NachoGreen.ColorWithAlpha (1.0f);
            navigationBar.BarTintColor = A.Color_NachoGreen;
            navigationBar.Translucent = false;

            yOffset = navigationBar.Frame.Bottom + VERTICAL_PADDING;

            UIView sectionOneView = new UIView (new RectangleF (HORIZONTAL_PADDING, yOffset, View.Frame.Width - (HORIZONTAL_PADDING * 2), CELL_HEIGHT * 2));
            sectionOneView.Layer.BorderWidth = .5f;
            sectionOneView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            sectionOneView.BackgroundColor = UIColor.White;
            sectionOneView.Layer.CornerRadius = 4;

            UILabel sectionOneHeader = new UILabel (new RectangleF (HORIZONTAL_PADDING, 0, sectionOneView.Frame.Width - HORIZONTAL_PADDING, CELL_HEIGHT));
            sectionOneHeader.Font = A.Font_AvenirNextRegular14;
            sectionOneHeader.TextColor = UIColor.DarkGray;
            sectionOneHeader.Text = "How can we reach you?";
            sectionOneHeader.TextAlignment = UITextAlignment.Left;
            sectionOneView.AddSubview (sectionOneHeader);

            UIView sectionOneHR = new UIView (new RectangleF (HORIZONTAL_PADDING, sectionOneHeader.Frame.Bottom - .5f, sectionOneView.Frame.Width - HORIZONTAL_PADDING, .5f));
            sectionOneHR.BackgroundColor = A.Color_NachoBorderGray;
            sectionOneView.AddSubview (sectionOneHR);

            UITextField sectionOneTextField = new UITextField (new RectangleF (HORIZONTAL_PADDING, sectionOneHR.Frame.Bottom, sectionOneView.Frame.Width - HORIZONTAL_PADDING, CELL_HEIGHT));
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

            UIView sectionTwoView = new UIView (new RectangleF (HORIZONTAL_PADDING, yOffset, View.Frame.Width - (HORIZONTAL_PADDING * 2), View.Frame.Height - yOffset - VERTICAL_PADDING));
            sectionTwoView.Layer.BorderWidth = .5f;
            sectionTwoView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            sectionTwoView.BackgroundColor = UIColor.White;
            sectionTwoView.Layer.CornerRadius = 4;

            UILabel sectionTwoHeader = new UILabel (new RectangleF (HORIZONTAL_PADDING, 0, sectionTwoView.Frame.Width - HORIZONTAL_PADDING, CELL_HEIGHT));
            sectionTwoHeader.Font = A.Font_AvenirNextRegular14;
            sectionTwoHeader.TextColor = UIColor.DarkGray;
            sectionTwoHeader.Text = "What can we help you with?";
            sectionTwoHeader.TextAlignment = UITextAlignment.Left;
            sectionTwoView.AddSubview (sectionTwoHeader);

            UIView sectionTwoHR = new UIView (new RectangleF (HORIZONTAL_PADDING, sectionTwoHeader.Frame.Bottom - .5f, sectionTwoHeader.Frame.Width - HORIZONTAL_PADDING, .5f));
            sectionTwoHR.BackgroundColor = A.Color_NachoBorderGray;
            sectionTwoView.AddSubview (sectionTwoHR);

            UITextView sectionTwoTextView = new UITextView (new RectangleF (HORIZONTAL_PADDING - 4, sectionTwoHR.Frame.Bottom, sectionTwoView.Frame.Width - HORIZONTAL_PADDING, sectionTwoView.Frame.Height - CELL_HEIGHT));
            sectionTwoTextView.Font = A.Font_AvenirNextMedium14;
            sectionTwoTextView.TextColor = UIColor.LightGray;
            sectionTwoTextView.Text = "Briefly describe what's going on";
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
                }
                return true;
            });

            sectionTwoTextView.ShouldEndEditing += ((textView) => {
                if (0 == textView.Text.Trim ().Length) {
                    sectionTwoTextView.TextColor = UIColor.LightGray;
                    sectionTwoTextView.Text = "Briefly describe what's going on...";
                }
                textView.ResignFirstResponder();
                return true;
            });

            scrollView.BackgroundColor = A.Color_NachoNowBackground;

            UINavigationItem navItems = new UINavigationItem ("Support");

            using (var image = UIImage.FromBundle ("nav-backarrow")) {
                UIBarButtonItem backButton = new UIBarButtonItem (image, UIBarButtonItemStyle.Plain, (sender, args) => {
                    this.DismissViewController (true, null);
                });
                backButton.Title = "Back";
                backButton.TintColor = A.Color_NachoBlue;
                navItems.SetLeftBarButtonItem (backButton, true);
            }

            Util.SetOriginalImageForButton (sendButton, "icn-send");

            sendButton.Clicked += (object sender, EventArgs e) => {
                Dictionary<string,string> supportInfo = new Dictionary<string, string> ();
                supportInfo.Add ("ContactInfo", sectionOneTextField.Text);
                supportInfo.Add ("Message", sectionTwoTextView.Text);
                Telemetry.RecordSupport (supportInfo, () => {
                    NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                        Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_TelemetrySupportMessageReceived),
                        Account = ConstMcAccount.NotAccountSpecific,
                    });
                });
            };
            navItems.RightBarButtonItem = sendButton;
            navigationBar.Items = new UINavigationItem[]{ navItems };
            View.AddSubview (navigationBar);
        }

        protected void LayoutView ()
        {
            scrollView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height - keyboardHeight);
            var contentFrame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height - VERTICAL_PADDING);
            contentView.Frame = contentFrame;
            scrollView.ContentSize = contentFrame.Size;
        }

        protected void ConfigureView ()
        {
            UITextField contactText = (UITextField)View.ViewWithTag (CONTACT_TEXTFIELD_TAG);
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
            var caretRect = textView.GetCaretRectForPosition (textView.SelectedTextRange.End);
            caretRect.Size = new SizeF (caretRect.Size.Width, caretRect.Size.Height);

            var notesView = (UIView)contentView.ViewWithTag (MESSAGEBODY_VIEW_TAG);
            caretRect.Y += notesView.Frame.Y + KEYBOARD_HEIGHT;
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
