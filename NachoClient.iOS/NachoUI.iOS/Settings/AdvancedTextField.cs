//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class AdvancedTextField : UIView
    {
        protected nfloat INSET = 15;

        public UITextField textField;

        public delegate void EditingChanged (UITextField textField);

        public EditingChanged EditingChangedCallback;

        public AdvancedTextField (string labelText, string placeHolder, bool hasBorder, CGRect rect, UIKeyboardType keyboardType = UIKeyboardType.Default) : base (rect)
        {
            this.BackgroundColor = UIColor.White;
            if (hasBorder) {
                this.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
                this.Layer.BorderWidth = .4f;
            }

            UILabel cellLefthandLabel = new UILabel (new CGRect (INSET, 0, 80, rect.Height));
            cellLefthandLabel.Text = labelText;
            cellLefthandLabel.BackgroundColor = UIColor.White;
            cellLefthandLabel.TextColor = A.Color_NachoGreen;
            cellLefthandLabel.Font = A.Font_AvenirNextMedium14;
            this.Add (cellLefthandLabel);

            textField = new UITextField ();
            textField.Frame = new CGRect (120, 0, this.Frame.Width - 120 - 1, rect.Height);
            textField.ClearButtonMode = UITextFieldViewMode.WhileEditing;
            textField.BackgroundColor = UIColor.White;
            textField.Placeholder = placeHolder;
            textField.Font = A.Font_AvenirNextRegular14;
            textField.AutocapitalizationType = UITextAutocapitalizationType.None;
            textField.AutocorrectionType = UITextAutocorrectionType.No;
            textField.AccessibilityLabel = labelText;
            textField.KeyboardType = keyboardType;
            this.Add (textField);

            textField.EditingDidEnd += TextField_EditingDidEnd;
            textField.ShouldReturn += TextField_ShouldReturn;
            textField.ShouldChangeCharacters += TextField_ShouldChangeCharacters;
            textField.EditingChanged += TextField_EditingChanged;
        }

        public bool IsNullOrEmpty ()
        {
            return String.IsNullOrEmpty (textField.Text);
        }

        void TextField_EditingChanged (object sender, EventArgs e)
        {
            if (null != EditingChangedCallback) {
                EditingChangedCallback (textField);
            }
        }

        void TextField_EditingDidEnd (object sender, EventArgs e)
        {
            // Dummy event for triggering UI monitoring
        }

        bool TextField_ShouldReturn (UITextField field)
        {
            field.TextColor = UIColor.Black;
            this.EndEditing (true);
            return true;
        }

        // Jeff prefers that we override iOS's removal of a secure string when editing begins.
        bool TextField_ShouldChangeCharacters (UITextField textField, NSRange range, string replacementString)
        {
            if (!textField.SecureTextEntry) {
                return true;
            }
            var updatedString = textField.Text.Substring (0, (int)range.Location) + replacementString + textField.Text.Substring ((int)(range.Location + range.Length));
            textField.Text = updatedString;
            if (null != EditingChangedCallback) {
                EditingChangedCallback (textField);
            }
            return false;
        }

    }
}

