//#define DEBUG_UI
using System;
using CoreGraphics;
using System.Collections.Generic;

using Foundation;
using UIKit;
using ObjCRuntime;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class UcAddressField : UITextField
    {
        // Shows the contact
        public const int TEXT_FIELD = 1;
        // Shows the white space between text fields
        public const int GAP_FIELD = 2;
        // Text field at the end of the list
        public const int ENTRY_FIELD = 3;

        public int type;
        public NcEmailAddress address;

        public UcAddressField (int type)
        {
            this.type = type;
            switch (this.type) {
            case GAP_FIELD:
                #if (DEBUG_UI)
                BackgroundColor = UIColor.Red;
                #else
                BackgroundColor = UIColor.White;
                #endif
                break;
            case TEXT_FIELD:
                #if (DEBUG_UI)
                BackgroundColor = UIColor.Yellow;
                #else
                BackgroundColor = A.Color_NachoLightGrayBackground;
                #endif
                BorderStyle = UITextBorderStyle.RoundedRect;

                // Short taps on the text field activate the closest gap or entry field.
                var shortTap = new UITapGestureRecognizer ();
                shortTap.NumberOfTapsRequired = 1;
                shortTap.AddTarget (this, new Selector ("onShortTap:"));
                shortTap.ShouldRecognizeSimultaneously = delegate {
                    return true;
                };
                AddGestureRecognizer (shortTap);

                // TODO - If we want to support copy-n-paste of email addresses, we can
                // add a long tap gesture recognizer to pop up the copy menu.
                UserInteractionEnabled = true;
                break;
            case ENTRY_FIELD:
                #if (DEBUG_UI)
                BackgroundColor = UIColor.Orange;
                #else
                BackgroundColor = UIColor.White;
                #endif
                break;
            }
            AutocapitalizationType = UITextAutocapitalizationType.None;
            AutocorrectionType = UITextAutocorrectionType.No;
            SpellCheckingType = UITextSpellCheckingType.No;
        }

        // Return null to hide the caret.  We want it hidden in a text field.
        public override CGRect GetCaretRectForPosition (UITextPosition position)
        {
            if (TEXT_FIELD == type) {
                return CGRect.Empty;
            } else if (null == position) {
                return CGRect.Empty;
            } else {
                return base.GetCaretRectForPosition (position);
            }
        }

        public bool IsTextField ()
        {
            return (TEXT_FIELD == type);
        }

        public bool IsGapField ()
        {
            return (GAP_FIELD == type);
        }

        [Foundation.Export ("onShortTap:")]
        public void OnShortTap (UIGestureRecognizer sender)
        {
            var addressField = sender.View as UcAddressField;
            CGPoint touch = sender.LocationInView (addressField);
            var addressBlock = addressField.Superview as UcAddressBlock;
            // Depnding on whether the touch is on the left half or right half
            // of the text field, we make either the gap before or after this
            // text field active.
            var field = (touch.X > (Frame.Width / 2) ? 
                addressBlock.AddressFieldSuccessor (addressField) : 
                addressBlock.AddressFieldPredecessor (addressField));
            NcAssert.True (null != field);
            field.BecomeFirstResponder ();
        }

        public override void DictationRecordingDidEnd ()
        {
            this.Delegate.ShouldChangeCharacters (this, new NSRange (0, this.Text.Length), this.Text);
            this.Text = "";
        }
    };
}

