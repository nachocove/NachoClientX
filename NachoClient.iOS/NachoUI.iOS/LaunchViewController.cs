// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using MonoTouch.CoreGraphics;

namespace NachoClient.iOS
{
    public partial class LaunchViewController : NcUIViewControllerNoLeaks
    {
        AppDelegate appDelegate;

        float yOffset;
        float keyboardHeight;
        bool shortScreen;

        protected UIImageView circleMail;
        protected UILabel startLabel;
        protected UIView emailBox;
        protected UIView passwordBox;
        protected UITextField emailField;
        protected UITextField passwordField;
        protected UIButton submitButton;
        protected UIButton advancedButton;
        protected UIButton supportButton;
        protected UITableView emailServiceTableView;

        protected bool serviceTableExpanded;
        protected EmailServiceTableViewSource emailServices;
        protected McAccount.AccountServiceEnum selectedEmailService;

        protected UIImageView loginTriangles;
        protected PointF originalStartLabelCenter;

        protected const int EMAIL_TEXTFIELD_TAG = 101;
        protected const int PASSWORD_TEXTFIELD_TAG = 102;
        protected const int SUBMIT_BUTTON_TAG = 103;
        protected const int ADVANCED_SIGNIN_BUTTON_TAG = 104;
        protected const int CUSTOMER_SUPPORT_BUTTON_TAG = 105;

        protected bool hasCompletedInitialAnimation = false;

        public LaunchViewController (IntPtr handle) : base (handle)
        {
            appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
                this.NavigationController.SetNavigationBarHidden (true, false);
            }
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, OnKeyboardNotification);
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, OnKeyboardNotification);
                NSNotificationCenter.DefaultCenter.AddObserver (UITextField.TextFieldTextDidChangeNotification, OnTextFieldChanged);
            }

            if (!hasCompletedInitialAnimation) {
                UIView.AnimateKeyframes (1.6, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {

                    UIView.AddKeyframeWithRelativeStartTime (0, .5, () => {
                        circleMail.Transform = CGAffineTransform.MakeScale (2.0f / 3.0f, 2.0f / 3.0f);
                        circleMail.Center = new PointF (160, View.Frame.Height / 4.369f - (shortScreen ? 40 : 0));
                    });

                    UIView.AddKeyframeWithRelativeStartTime (.5, .5, () => {
                        startLabel.Alpha = 1.0f;
                        emailServiceTableView.Alpha = 1.0f;
                        advancedButton.Alpha = 1.0f;
                        supportButton.Alpha = 1.0f;
                    });

                }, ((bool finished) => {
                    hasCompletedInitialAnimation = true;
                }));
            }
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
                this.NavigationController.SetNavigationBarHidden (false, false);
            }
            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillHideNotification);
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillShowNotification);
                NSNotificationCenter.DefaultCenter.RemoveObserver (UITextField.TextFieldTextDidChangeNotification);
            }
        }

        public virtual bool HandlesKeyboardNotifications {
            get { return true; }
        }

        public void OnEmailServiceSelected (McAccount.AccountServiceEnum service, bool willExpand)
        {
            if (willExpand) {
                emailServices.Grow (emailServiceTableView);
                View.EndEditing (true);
            } else {
                emailServices.Shrink (emailServiceTableView);
            }

            // Gotta keep global state :(
            selectedEmailService = service;
            serviceTableExpanded = willExpand;
            ConfigureAndLayout ();

            // Hide email/password/submit before they've selected a service.
            // Show email/password/submit after they've selected a service.
            if (McAccount.AccountServiceEnum.None == service) {
                ;
            } else {
                emailBox.Alpha = 1.0f;
                passwordBox.Alpha = 1.0f;
                submitButton.Alpha = 1.0f;
                startLabel.Text = "Enter your account information to get started.";
            }
        }

        protected override void CreateViewHierarchy ()
        {
            View.BackgroundColor = A.Color_NachoGreen;
            scrollView.BackgroundColor = A.Color_NachoGreen;
            scrollView.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag;
            contentView.BackgroundColor = A.Color_NachoGreen;

            shortScreen = (500 > View.Frame.Height);

            circleMail = new UIImageView ();
            using (var circleImage = UIImage.FromBundle ("Bootscreen-1")) {
                circleMail.Image = circleImage;
            }

            circleMail.Frame = new RectangleF (View.Frame.Width / 2 - 60, (shortScreen ? 155 : 200), 120, 120);
            contentView.AddSubview (circleMail);

            yOffset = View.Frame.Height - 343;
            yOffset -= (shortScreen ? 20 : 40);

            startLabel = new UILabel (new RectangleF (30, yOffset, View.Frame.Width - 60, 50));
            originalStartLabelCenter = startLabel.Center;
            startLabel.Text = "Start by choosing your email service provider.";
            startLabel.Lines = 2;
            startLabel.BackgroundColor = A.Color_NachoGreen;
            startLabel.TextColor = UIColor.White;
            startLabel.Font = A.Font_AvenirNextRegular17;
            startLabel.TextAlignment = UITextAlignment.Center;
            startLabel.Alpha = 0.0f;
            contentView.AddSubview (startLabel);

            yOffset = startLabel.Frame.Bottom + 20;

            emailServices = new EmailServiceTableViewSource ();
            emailServices.SetSelectedItem (McAccount.AccountServiceEnum.None);
            emailServices.OnSelected = OnEmailServiceSelected;

            emailServiceTableView = new UITableView (new RectangleF (25, yOffset, View.Frame.Width - 50, emailServices.GetTableHeight ()));
            emailServiceTableView.BackgroundColor = UIColor.White;
            emailServiceTableView.ScrollEnabled = false;
            emailServiceTableView.Alpha = 0f;
            contentView.AddSubview (emailServiceTableView);

            emailServiceTableView.Source = emailServices;

            yOffset = emailServiceTableView.Frame.Bottom + 4f;

            emailBox = new UIView (new RectangleF (25, yOffset, View.Frame.Width - 50, 46));
            emailBox.BackgroundColor = UIColor.White;
            emailBox.Alpha = 0.0f;

            emailField = new UITextField (new RectangleF (45, 0, emailBox.Frame.Width - 50, emailBox.Frame.Height));
            emailField.BackgroundColor = UIColor.White;
            emailField.Placeholder = "Email Address";
            emailField.Font = A.Font_AvenirNextRegular17;
            emailField.BorderStyle = UITextBorderStyle.None;
            emailField.TextAlignment = UITextAlignment.Left;
            emailField.KeyboardType = UIKeyboardType.EmailAddress;
            emailField.AutocapitalizationType = UITextAutocapitalizationType.None;
            emailField.AutocorrectionType = UITextAutocorrectionType.No;
            emailField.Tag = EMAIL_TEXTFIELD_TAG;
            emailField.ShouldReturn += TextFieldShouldReturn;
            emailBox.AddSubview (emailField);

            UIImageView mailImage = new UIImageView ();
            using (var loginImageTwo = UIImage.FromBundle ("Loginscreen-2")) {
                mailImage.Image = loginImageTwo;
            }
            mailImage.Frame = new RectangleF (15, 15, 16, 11);
            emailBox.AddSubview (mailImage);

            contentView.AddSubview (emailBox);

            yOffset = emailBox.Frame.Bottom + 4f;

            passwordBox = new UIView (new RectangleF (25, yOffset, View.Frame.Width - 50, 46));
            passwordBox.BackgroundColor = UIColor.White;
            passwordBox.Alpha = 0.0f;

            passwordField = new UITextField (new RectangleF (45, 0, passwordBox.Frame.Width - 50, passwordBox.Frame.Height));
            passwordField.BackgroundColor = UIColor.White;
            passwordField.Placeholder = "Password";
            passwordField.Font = A.Font_AvenirNextRegular17;
            passwordField.BorderStyle = UITextBorderStyle.None;
            passwordField.TextAlignment = UITextAlignment.Left;
            passwordField.SecureTextEntry = true;
            passwordField.KeyboardType = UIKeyboardType.Default;
            passwordField.AutocapitalizationType = UITextAutocapitalizationType.None;
            passwordField.AutocorrectionType = UITextAutocorrectionType.No;
            passwordField.Tag = PASSWORD_TEXTFIELD_TAG;
            passwordField.ShouldReturn += TextFieldShouldReturn;
            passwordBox.AddSubview (passwordField);
            passwordBox.UserInteractionEnabled = true;

            UIImageView lockImage = new UIImageView ();
            using (var loginImageThree = UIImage.FromBundle ("Loginscreen-3")) {
                lockImage.Image = loginImageThree;
            }
            lockImage.Frame = new RectangleF (15, 15, 14, 15);
            passwordBox.AddSubview (lockImage);

            contentView.AddSubview (passwordBox);

            yOffset = passwordBox.Frame.Bottom + 20f;

            submitButton = new UIButton (new System.Drawing.RectangleF (25, yOffset, View.Frame.Width - 50, 46));
            submitButton.BackgroundColor = A.Color_NachoSubmitButton;
            submitButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            submitButton.SetTitle ("Submit", UIControlState.Normal);
            submitButton.TitleLabel.TextColor = UIColor.White;
            submitButton.TitleLabel.Font = A.Font_AvenirNextDemiBold17;
            submitButton.Layer.CornerRadius = 4f;
            submitButton.Layer.MasksToBounds = true;
            submitButton.Tag = SUBMIT_BUTTON_TAG;
            submitButton.Alpha = 0.0f;
            submitButton.TouchUpInside += SubmitButtonTouchUpInside;
            contentView.AddSubview (submitButton);

            yOffset = submitButton.Frame.Bottom + 20f;

            advancedButton = new UIButton (new RectangleF (0, yOffset, View.Frame.Width, 20));
            advancedButton.BackgroundColor = A.Color_NachoGreen;
            advancedButton.SetTitle ("Advanced Sign In", UIControlState.Normal);
            advancedButton.TitleLabel.TextColor = A.Color_NachoYellow;
            advancedButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            advancedButton.Tag = ADVANCED_SIGNIN_BUTTON_TAG;
            advancedButton.Alpha = 0.0f;
            contentView.AddSubview (advancedButton);
            advancedButton.TouchUpInside += AdvancedLoginTouchUpInside;

            yOffset = advancedButton.Frame.Bottom + 20;

            supportButton = new UIButton (new RectangleF (0, yOffset, View.Frame.Width, 20));
            supportButton.BackgroundColor = A.Color_NachoGreen;
            supportButton.SetTitle ("Customer Support", UIControlState.Normal);
            supportButton.TitleLabel.TextColor = A.Color_NachoYellow;
            supportButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            supportButton.Tag = CUSTOMER_SUPPORT_BUTTON_TAG;
            supportButton.Alpha = 0.0f;
            contentView.AddSubview (supportButton);
            supportButton.TouchUpInside += SupportButtonTouchUpInside;

            yOffset = supportButton.Frame.Bottom;

            // bottom padding
            yOffset += 20;

            contentView.BringSubviewToFront (emailServiceTableView);

            // Anchor loginTriangles on the bottom

            loginTriangles = new UIImageView ();
            using (var bootImage = UIImage.FromBundle ("Bootscreen-5")) {
                loginTriangles.Image = bootImage;
            }
            loginTriangles.Frame = new RectangleF (0, View.Frame.Height - 39, 320, 39);
            View.AddSubview (loginTriangles);
        }

        /// <summary>
        /// Hides the Advances & Customer Support buttons when the service table is visible
        /// </summary>
        protected override void ConfigureAndLayout ()
        {
            UIView.AnimateKeyframes (1, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {
                UIView.AddKeyframeWithRelativeStartTime (0, 1, () => {
                    circleMail.Alpha = (keyboardHeight == 0 ? 1.0f : 0.0f);
                    supportButton.Alpha = (serviceTableExpanded ? 0.0f : 1.0f);
                    advancedButton.Alpha = (serviceTableExpanded ? 0.0f : 1.0f);
                    ViewFramer.Create (emailServiceTableView).Height (emailServices.GetTableHeight ());
                    scrollView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height - keyboardHeight);
                    RectangleF contentFrame;
                    contentFrame = new RectangleF (0, 0, View.Frame.Width, yOffset);
                    contentView.Frame = contentFrame;
                    scrollView.ContentSize = contentFrame.Size;

                    if (startLabel.Center == originalStartLabelCenter) {
                        //Raise Keyboard
                        if (keyboardHeight > 0) {
                            if (!shortScreen) {
                                KeyboardRaisedLarge ();
                            } else {
                                KeyboardRaisedSmall ();
                            }
                        }
                    } else {
                        //Dismiss Keyboard
                        if (!shortScreen) {
                            KeyboardDismissedLarge ();
                        } else {
                            KeyboardDismissedSmall ();
                        }
                    }
                });
            }, ((bool finished) => {
                ;
            }));
        }

        protected void KeyboardRaisedLarge ()
        {
            startLabel.Center = new PointF (startLabel.Center.X, startLabel.Center.Y - 40);
        }

        protected void KeyboardDismissedLarge ()
        {
            startLabel.Center = new PointF (startLabel.Center.X, startLabel.Center.Y + 40);
        }

        protected void KeyboardRaisedSmall ()
        {
            startLabel.Center = new PointF (startLabel.Center.X, startLabel.Center.Y - 40);
            submitButton.Center = new PointF (submitButton.Center.X, submitButton.Center.Y - 15);
        }

        protected void KeyboardDismissedSmall ()
        {
            startLabel.Center = new PointF (startLabel.Center.X, startLabel.Center.Y + 40);
            submitButton.Center = new PointF (submitButton.Center.X, submitButton.Center.Y + 15);
        }

        private void MaybeStartLogin ()
        {
            var emailAddress = emailField.Text.Trim ();

            string serviceName;
            if (EmailHelper.IsServiceUnsupported (emailAddress, out serviceName)) {
                var nuance = String.Format ("Nacho Mail does not support {0} yet.", serviceName);
                Complain ("Nacho Mail", nuance);
                return;
            }

            if (!emailAddress.Contains ("@")) {
                emailField.TextColor = A.Color_NachoRed;
                Complain ("Nacho Mail", "Your email address must contain an '@'.\nFor example, username@company.com");
                return;
            }

            if (!EmailHelper.IsValidEmail (emailField.Text)) {
                emailField.TextColor = A.Color_NachoRed;
                Complain ("Nacho Mail", "Your email address is not valid.\nFor example, username@company.com");
                return;
            }

            if (EmailHelper.IsHotmailServiceAddress (emailAddress)) {
                if (!emailServices.IsHotmailServiceSelected ()) {
                    ConfirmBeforeStarting ("Confirm Email", "Your email address does not match the selected service.\nUse it anyway?");
                    return;
                }
            } else {
                if (emailServices.IsHotmailServiceSelected ()) {
                    ConfirmBeforeStarting ("Confirm Email", "Your email address does not match the selected service.\nUse it anyway?");
                    return;
                }
            }

            StartLoginProcess ();
        }

        private void Complain (string title, string message)
        {
            var alert = new UIAlertView (title, message, null, "OK", null);
            alert.Show ();        
        }

        /// <summary>
        /// Confirms something the before starting the login process
        /// </summary>
        private void ConfirmBeforeStarting (string title, string message)
        {
            var alert = new UIAlertView (title, message, null, "OK", new string[] { "Cancel" });
            alert.Clicked += (s, b) => {
                if (0 == b.ButtonIndex) {
                    StartLoginProcess ();
                }
            };
            alert.Show ();
        }

        private void StartLoginProcess ()
        {
            NcModel.Instance.RunInTransaction (() => {
                // Need to regex-validate UI inputs.
                // You will always need to supply user credentials (until certs, for sure).
                // You will always need to supply the user's email address.
                appDelegate.Account = new McAccount () { EmailAddr = emailField.Text };
                appDelegate.Account.Signature = "Sent from Nacho Mail";
                appDelegate.Account.AccountService = selectedEmailService;
                appDelegate.Account.DisplayName = McAccount.AccountServiceName(selectedEmailService);
                appDelegate.Account.Insert ();
                var cred = new McCred () { 
                    AccountId = appDelegate.Account.Id,
                    Username = emailField.Text,
                };
                cred.Insert ();
                cred.UpdatePassword (passwordField.Text);
                Telemetry.RecordAccountEmailAddress (appDelegate.Account);
                // Maintain the state of our progress
                LoginHelpers.SetHasProvidedCreds (appDelegate.Account.Id, true);
            });
            BackEnd.Instance.Start (appDelegate.Account.Id);
            PerformSegue (StartupViewController.NextSegue (), this);
        }

        public void maybeEnableConnect ()
        {
            var shouldWe = ((0 < emailField.Text.Length) && (0 < passwordField.Text.Length));

            var submitButton = (UIButton)contentView.ViewWithTag (SUBMIT_BUTTON_TAG);
            submitButton.Enabled = shouldWe;
            submitButton.Alpha = (shouldWe ? 1.0f : 0.5f);
        }

        private void OnTextFieldChanged (NSNotification notification)
        {
            maybeEnableConnect ();
        }

        private void OnKeyboardNotification (NSNotification notification)
        {
            if (IsViewLoaded) {
                //Check if the keyboard is becoming visible
                bool visible = notification.Name == UIKeyboard.WillShowNotification;
                //Start an animation, using values from the keyboard
                UIView.BeginAnimations ("AnimateForKeyboard");
                UIView.SetAnimationBeginsFromCurrentState (true);
                UIView.SetAnimationDuration (UIKeyboard.AnimationDurationFromNotification (notification));
                UIView.SetAnimationCurve ((UIViewAnimationCurve)UIKeyboard.AnimationCurveFromNotification (notification));
                //Pass the notification, calculating keyboard height, etc.
                bool landscape = InterfaceOrientation == UIInterfaceOrientation.LandscapeLeft || InterfaceOrientation == UIInterfaceOrientation.LandscapeRight;
                if (visible) {
                    var keyboardFrame = UIKeyboard.FrameEndFromNotification (notification);
                    OnKeyboardChanged (visible, landscape ? keyboardFrame.Width : keyboardFrame.Height);
                } else {
                    var keyboardFrame = UIKeyboard.FrameBeginFromNotification (notification);
                    OnKeyboardChanged (visible, landscape ? keyboardFrame.Width : keyboardFrame.Height);
                }

                UIView.CommitAnimations ();
            }
        }

        /// <summary>
        /// Override this method to apply custom logic when the keyboard is shown/hidden
        /// </summary>
        /// <param name='visible'>
        /// If the keyboard is visible
        /// </param>
        /// <param name='height'>
        /// Calculated height of the keyboard (width not generally needed here)
        /// </param>
        protected virtual void OnKeyboardChanged (bool visible, float height)
        {
            var newHeight = (visible ? height : 0);

            if (newHeight == keyboardHeight) {
                return;
            }
            keyboardHeight = newHeight;

            ConfigureAndLayout ();

            if (!shortScreen) {
                scrollView.ScrollRectToVisible (new RectangleF (supportButton.Frame.X, supportButton.Frame.Y + 10, supportButton.Frame.Width, supportButton.Frame.Height), false);
            } else {
                scrollView.ScrollRectToVisible (new RectangleF (submitButton.Frame.X, submitButton.Frame.Y + 10, submitButton.Frame.Width, submitButton.Frame.Height), false);
            }
        }

        public bool TextFieldShouldReturn (UITextField whatField)
        {
            switch (whatField.Tag) {
            case EMAIL_TEXTFIELD_TAG:
                passwordField.BecomeFirstResponder ();
                break;
            case PASSWORD_TEXTFIELD_TAG:
                View.EndEditing (true);
                break;
            }
            return true;
        }

        protected void SubmitButtonTouchUpInside (object sender, EventArgs e)
        {
            MaybeStartLogin ();
        }

        protected void AdvancedLoginTouchUpInside (object sender, EventArgs e)
        {
            View.EndEditing (true);
            PerformSegue ("SegueToAdvancedLogin", this);
        }

        protected void SupportButtonTouchUpInside (object sender, EventArgs e)
        {
            View.EndEditing (true);
            PerformSegue ("SegueToSupport", this);
        }

        protected void OnKeyboardChangeCompleted ()
        {

        }

        protected override void Cleanup ()
        {
            submitButton.TouchUpInside -= SubmitButtonTouchUpInside;
            advancedButton.TouchUpInside -= AdvancedLoginTouchUpInside;
            supportButton.TouchUpInside -= SupportButtonTouchUpInside;

            submitButton = null;
            advancedButton = null;
            supportButton = null;

            emailField.ShouldReturn -= TextFieldShouldReturn;
            passwordField.ShouldReturn -= TextFieldShouldReturn;

            emailField = null;
            passwordField = null;
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("SegueToSupport")) {
                return;
            }

            if (segue.Identifier.Equals ("SegueToAdvancedLogin")) {
                // TODO: How can account be set?
                if (!LoginHelpers.IsCurrentAccountSet ()) {
                    var vc = (AdvancedLoginViewController)segue.DestinationViewController;
                    vc.SetAdvanced (emailField.Text, passwordField.Text);
                }
                return;
            }

        }

    }
}
