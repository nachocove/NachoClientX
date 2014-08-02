//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using UIImageEffectsBinding;
using MonoTouch.CoreGraphics;

namespace NachoClient.iOS
{
    [Register ("SignInView")]

    public class SignInView: UIView 
    {
        public SignInView ()
        {

        }

        public SignInView (RectangleF frame)
        {
            this.Frame = frame;
        }

        public SignInView (IntPtr handle) : base (handle)
        {

        }

        public void AddEmailField (UITextField emailField)//, RectangleF theFrame)
        {
            UIView emailBox = new UIView(new RectangleF(25, Frame.Height / 2 - 100f, Frame.Width - 50, 44));
            emailBox.BackgroundColor = UIColor.White;

            emailField.Frame = new RectangleF (100, 0, emailBox.Frame.Width - 100, emailBox.Frame.Height);
            emailField.BackgroundColor = UIColor.White;
            emailField.Placeholder = "email@company.com";
            emailField.Font = A.Font_AvenirNextRegular14;
            emailBox.Add (emailField);

            UILabel emailLabel = new UILabel (new RectangleF(10, 0, 60, 44));
            emailLabel.Text = "Email";
            emailLabel.BackgroundColor = UIColor.White;
            emailLabel.TextColor = A.Color_NachoGreen;
            emailLabel.Font = A.Font_AvenirNextMedium14;
            emailBox.Add (emailLabel);

            this.Add (emailBox);
        }

        public void AddPasswordField (UITextField passwordField)
        {
            UIView passwordBox = new UIView(new RectangleF(25, Frame.Height / 2 - 54.5f, Frame.Width - 50, 44));
            passwordBox.BackgroundColor = UIColor.White;

            passwordField.Frame = new RectangleF (100, 0, passwordBox.Frame.Width - 100, passwordBox.Frame.Height);
            passwordField.BackgroundColor = UIColor.White;
            passwordField.Placeholder = "Required";
            passwordField.Font = A.Font_AvenirNextRegular14;
            passwordBox.Add (passwordField);

            UILabel passwordLabel = new UILabel (new RectangleF(10, 0, 80, 44));
            passwordLabel.Text = "Password";
            passwordLabel.BackgroundColor = UIColor.White;
            passwordLabel.TextColor = A.Color_NachoGreen;
            passwordLabel.Font = A.Font_AvenirNextMedium14;;
            passwordBox.Add (passwordLabel);

            this.Add (passwordBox);
        }

        public void configureSubmitButton(UIButton submitButton)
        {
            submitButton.BackgroundColor = A.Color_NachoBlue;
            submitButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            submitButton.SetTitle ("Submit", UIControlState.Normal);
            submitButton.TitleLabel.TextColor = UIColor.White;
            submitButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            this.Add (submitButton);
        }

        public void configureAdvancedButton(UIButton advancedButton)
        {
            advancedButton.Frame = new RectangleF (25, Frame.Height - 50, Frame.Width - 50, 44);
            advancedButton.BackgroundColor = A.Color_NachoGreen;
            advancedButton.SetTitle ("Advanced Sign In", UIControlState.Normal);
            advancedButton.TitleLabel.TextColor = A.Color_NachoYellow;
            advancedButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            this.Add (advancedButton);
        }

        public void addStartLabel()
        {
            UILabel startLabel = new UILabel (new RectangleF(30, Frame.Height/2 - 160, Frame.Width - 60, 60));
            startLabel.Text = "Start by entering your Exchange email address and password.";
            startLabel.Lines = 2;
            startLabel.BackgroundColor = A.Color_NachoGreen;
            startLabel.TextColor = UIColor.White;
            startLabel.Font = A.Font_AvenirNextMedium14;
            startLabel.TextAlignment = UITextAlignment.Center;
            this.Add (startLabel);
        }
    }
}

