﻿using System;
using System.Drawing;
using System.Collections.Generic;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

using MimeKit;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    /// <summary>
    /// Address block is a widget that manages email addresses. It allows user
    /// to edit a list of email address and presents them.
    /// 
    /// An address block consists of a top left label (e.g., "To:") and a list
    /// of address fields (UcAddressField) that it is responsible for layout
    /// and a button to starting the contact chooser.
    /// 
    /// There are 3 types of fields - text, gap, and entry. A text field holds
    /// the display name or an email address (if the contact is not available)
    /// of a contact. Gap field presents white space between text fields. It
    /// also present the caret when in editing mode. Entry field is a text
    /// field that allows users to enter new addresses at the end of the list.
    /// 
    /// A typical layout of these fields are as such:
    /// 
    /// Label  [GAP1] [TEXT1] [GAP2] [TEXT2] [GAP3]
    ///               [TEXT3] [GAP4]
    ///               [TEXT4] [GAP5] [TEXT5] [ENTRY]
    /// 
    /// There is only one entry field at the end. Text and gap fields are
    /// interleaved. When a user taps on a text field, it finds the closest
    /// gap field. This means if he taps the left half of TEXT5, the caret
    /// will activate on GAP5. If he taps on the right half, ENTRY will be
    /// activated. Users can selet the gap where they want to insert addresses.
    /// GAP1 is added to allow users to insert an address at the front of the
    /// list.
    /// 
    /// Because GAP1 takes up space, the left margin of the first row is
    /// more to the left than the rest of the rows.
    /// </summary>
    public class UcAddressBlock : UIView
    {
        protected int isActive;
        protected bool isCompact;
        protected bool isEditable;

        protected float parentWidth;
        protected string topLeftString;
        protected IUcAddressBlockDelegate owner;

        protected int suppliedCount;
        protected UILabel moreLabel;
        protected UILabel topLeftLabel;
        protected UIButton chooserButton;
        protected UcAddressField entryTextField;

        protected List<UcAddressField> list;

        protected float LINE_HEIGHT = 42;
        protected float LEFT_LABEL_INDENT = 15;
        protected float LEFT_ADDRESS_INDENT = 57;
        protected float RIGHT_INDENT = 15;

        protected UcAddressField currentAddressField;

        public enum TagType {
            TEXT_FIELD_TAG = UcAddressField.TEXT_FIELD,
            GAP_FIELD_TAG = UcAddressField.GAP_FIELD,
            ENTRY_FIELD_TAG = UcAddressField.ENTRY_FIELD,
            TOPLEFT_LABEL_TAG,
            MORE_LABEL_TAG,
            CHOOSER_BUTTON_TAG,
        };

        public UcAddressBlock (IUcAddressBlockDelegate owner, string label, float width)
        {
            this.owner = owner;
            this.topLeftString = label;
            this.parentWidth = width;
            this.BackgroundColor = UIColor.White;
            this.list = new List<UcAddressField> ();
            this.isEditable = true;

            this.AutoresizingMask = UIViewAutoresizing.None;
            this.AutosizesSubviews = false;
            this.currentAddressField = null;

            CreateView ();
        }

        public void SetCompact (bool isCompact, int moreCount)
        {
            this.isCompact = isCompact;
            this.suppliedCount = moreCount;
        }

        public void SetEditable (bool isEditable)
        {
            this.isEditable = isEditable;
        }

        public void SetLineHeight (float height)
        {
            this.LINE_HEIGHT = height;
        }

        public void SetAddressIndentation (float width)
        {
            this.LEFT_ADDRESS_INDENT = width;
        }

        public void SetCurrentAddressField (UcAddressField addressField)
        {
            NcAssert.True ((0 <= list.IndexOf (addressField)) ||
            (addressField == entryTextField) ||
            (null == addressField));
            this.currentAddressField = addressField;
        }

        public List<NcEmailAddress> AddressList {
            get {
                var l = new List<NcEmailAddress> ();
                foreach (var address in list) {
                    if (UcAddressField.TEXT_FIELD == address.type) {
                        l.Add (address.address);
                    }
                }
                return l;
            }
        }

        protected void InsertInternal (int index, string text, NcEmailAddress address, int type)
        {
            var a = new UcAddressField (type);
            a.UserInteractionEnabled = isEditable;
            a.ContentMode = UIViewContentMode.Left;
            a.Font = A.Font_AvenirNextRegular14;
            a.TextColor = A.Color_154750;
            a.Text = text;
            a.address = address;
            var aSize = a.StringSize (a.Text, a.Font);
            if (UcAddressField.TEXT_FIELD == type) {
                // extra space for rounded corners
                aSize.Width += 14; // FIXME - see if there is a way to derive this value from dimension of the text view
            }
            a.Frame = new RectangleF (PointF.Empty, aSize);
            a.Delegate = new UcAddressFieldDelegate (this);
            this.AddSubview (a);
            list.Insert (index, a);
        }

        public void Clear ()
        {
            list.Clear ();
        }

        public void Append (NcEmailAddress address)
        {
            int index;
            if (null == currentAddressField) {
                index = list.Count;
            } else {
                index = list.IndexOf (currentAddressField);
                if (-1 == index) {
                    index = list.Count;
                }
            }

            InsertInternal (index, " ", null, UcAddressField.GAP_FIELD);

            if (null == address.contact) {
                string text;
                InternetAddress parsedAddress;
                if (!InternetAddress.TryParse (address.address, out parsedAddress)) {
                    text = address.address; // can't parse the string. just display verbatim
                } else {
                    if ((null != parsedAddress.Name) && (0 < parsedAddress.Name.Length)) {
                        text = parsedAddress.Name; // prefer display name
                    } else {
                        text = (parsedAddress as MailboxAddress).Address; // fallback to email address
                    }
                }
                InsertInternal (index + 1, text, address, UcAddressField.TEXT_FIELD);
            } else {
                InsertInternal (index + 1, address.contact.GetDisplayNameOrEmailAddress (),
                    address, UcAddressField.TEXT_FIELD);
            }

            // Calling layout now will make the animation look
            // better because the initial location will be correct.
            Layout ();

            // Notifies the owner
            ConfigureView ();
        }

        protected void CreateView ()
        {
            this.BackgroundColor = UIColor.White;

            moreLabel = new UILabel ();
            moreLabel.Tag = (int)TagType.MORE_LABEL_TAG;
            moreLabel.Font = A.Font_AvenirNextRegular12;
            moreLabel.TextColor = A.Color_808080;

            var moreTapGestureRecognizer = new UITapGestureRecognizer ();
            moreTapGestureRecognizer.NumberOfTapsRequired = 1;
            moreTapGestureRecognizer.AddTarget (this, new MonoTouch.ObjCRuntime.Selector ("MoreLabelTapSelector:"));
            moreLabel.AddGestureRecognizer (moreTapGestureRecognizer);
            moreLabel.UserInteractionEnabled = true;

            topLeftLabel = new UILabel ();
            topLeftLabel.Tag = (int)TagType.TOPLEFT_LABEL_TAG;
            topLeftLabel.Font = A.Font_AvenirNextMedium14;
            topLeftLabel.TextColor = A.Color_NachoDarkText;

            chooserButton = UIButton.FromType (UIButtonType.System);
            Util.SetOriginalImagesForButton (chooserButton, "email-add", "email-add-active");
            chooserButton.Tag = (int)TagType.CHOOSER_BUTTON_TAG;
            chooserButton.SizeToFit ();
            chooserButton.Frame = new RectangleF (parentWidth - chooserButton.Frame.Width - RIGHT_INDENT, 0, chooserButton.Frame.Width, chooserButton.Frame.Height);
            chooserButton.Hidden = !isEditable;
            chooserButton.TouchUpInside += (object sender, EventArgs e) => {
                if (null != owner) {
                    owner.AddressBlockAddContactClicked (this, null);
                }
            };

            if (isEditable) {
                entryTextField = new UcAddressField (UcAddressField.ENTRY_FIELD);
                entryTextField.Font = A.Font_AvenirNextRegular14;
                entryTextField.TextColor = A.Color_808080;
                entryTextField.Text = " ";
                entryTextField.SizeToFit ();
                entryTextField.Delegate = new UcAddressFieldDelegate (this);

                this.AddSubviews (new UIView[] { topLeftLabel, moreLabel, chooserButton, entryTextField });
            } else {
                this.AddSubviews (new UIView[] { topLeftLabel, moreLabel, chooserButton });
            }

//            entryTextField.BackgroundColor = UIColor.Yellow;
        }

        [MonoTouch.Foundation.Export ("MoreLabelTapSelector:")]
        public void MoreLabelTapSelector (UIGestureRecognizer sender)
        {
            if (!isEditable) {
                return;
            }
            if (null != owner) {
                owner.AddressBlockWillBecomeActive (this);
            }
            entryTextField.BecomeFirstResponder ();
            ConfigureView ();
        }

        public void ConfigureView ()
        {
            topLeftLabel.Text = topLeftString;
            var topLeftLabelSize = topLeftLabel.StringSize (topLeftString, topLeftLabel.Font);
            topLeftLabel.Frame = new RectangleF (topLeftLabel.Frame.Location, topLeftLabelSize);

            if (null != owner) {
                owner.AddressBlockNeedsLayout (this);
            }
        }

        public void SetEditFieldAsFirstResponder()
        {
            this.entryTextField.BecomeFirstResponder ();
        }

        /// Adjusts x & y on the top line of a view
        protected void AdjustXY (UIView view, float X, float Y)
        {
            view.Center = new PointF (X + (view.Frame.Width / 2), Y + LINE_HEIGHT / 2);
        }

        public void Layout ()
        {
            if (isCompact) {
                LayoutCompactView ();
            } else {
                LayoutExpandedView ();
            }
        }

        protected void LayoutCompactView ()
        {
            float yOffset = 0;
            float xOffset = 15;
            float xLimit = parentWidth;

            if (null == topLeftString) {
                topLeftLabel.Hidden = true;
            } else {
                topLeftLabel.Hidden = false;
                AdjustXY (topLeftLabel, xOffset, yOffset);
                xOffset += topLeftLabel.Frame.Width;
            }

            chooserButton.Hidden = true;

            for (int i = 1; i < list.Count; i++) {
                list [i].Hidden = true;
            }

            xOffset = LEFT_ADDRESS_INDENT;

            if (0 < list.Count) {
                NcAssert.True (1 < list.Count); // must have at least 2 since the first one is a gap
                var firstAddress = list [1];
                firstAddress.Hidden = false;
                AdjustXY (firstAddress, xOffset, yOffset);
                xOffset += firstAddress.Frame.Width;
            }

            var moreCount = suppliedCount;
            if (moreCount < 0) {
                moreCount = list.Count / 2;  // count of text fields, assuming gap+text.
                moreCount -= 1; // We are showing one address already
            }

            if (1 > moreCount) {
                moreLabel.Hidden = true;
            } else {
                moreLabel.Text = String.Format (" +{0} more", moreCount);
                moreLabel.SizeToFit ();
                moreLabel.Hidden = false;
                var remainingSpace = xLimit - xOffset;
                if (moreLabel.Frame.Width >= remainingSpace) {
                    yOffset += LINE_HEIGHT;
                    xOffset = LEFT_LABEL_INDENT;
                    xLimit = parentWidth;
                }
                var yMoreLabel = yOffset + (LINE_HEIGHT / 2) - (moreLabel.Frame.Height / 2);
                moreLabel.Frame = new RectangleF (xOffset, yMoreLabel, (xLimit - xOffset), moreLabel.Frame.Height);
                xOffset += moreLabel.Frame.Width;
            }
            if (isEditable) {
                entryTextField.Frame = new RectangleF (xOffset, 0, (xLimit - xOffset), LINE_HEIGHT);
                xOffset += entryTextField.Frame.Width;
            }
            yOffset += LINE_HEIGHT;
            // Size the whole control
            this.Frame = new RectangleF (this.Frame.X, this.Frame.Y, parentWidth, yOffset);
        }

        protected void LayoutExpandedView ()
        {
            float yOffset = 0;
            float xOffset = LEFT_LABEL_INDENT;
            float xLimit = parentWidth;
           
            moreLabel.Hidden = true;

            if (null == topLeftString) {
                topLeftLabel.Hidden = true;
            } else {
                topLeftLabel.Hidden = false;
                AdjustXY (topLeftLabel, xOffset, yOffset);
                xOffset += topLeftLabel.Frame.Width;
            }

            if (0 == isActive) {
                chooserButton.Hidden = true;
            } else {
                chooserButton.Hidden = false;
                AdjustXY (chooserButton, parentWidth - chooserButton.Frame.Width - RIGHT_INDENT, yOffset);
                xLimit = chooserButton.Frame.X;
            }

            bool firstLine = true;
            xOffset = LEFT_ADDRESS_INDENT;
            if (0 < list.Count) {
                xOffset -= list [0].Frame.Width;
            }
                                
            for (int i = 0; i < list.Count; i++) {
                var address = list [i];
                var remainingSpace = xLimit - xOffset;
                var requestedSpace = address.Frame.Width;
                // If there's a comma, add it into the request
                if ((i + 1) < list.Count) {
                    var c = list [i + 1];
                    if (c.IsGapField ()) {
                        requestedSpace += c.Frame.Width;
                    }
                }
                // Force new row, except for commas, ...
                if (!address.IsGapField ()) {
                    if (requestedSpace >= remainingSpace) {
                        // ..or the line is too long
                        if (firstLine || (LEFT_ADDRESS_INDENT != xOffset)) {
                            firstLine = false;
                            yOffset += LINE_HEIGHT;
                            xOffset = LEFT_ADDRESS_INDENT;
                            xLimit = parentWidth;
                        }
                    }
                }
                address.Hidden = false;
                var yAddress = yOffset + (LINE_HEIGHT / 2) - (address.Frame.Height / 2);
                address.Frame = new RectangleF (xOffset, yAddress, address.Frame.Width, address.Frame.Height);
                xOffset += address.Frame.Width;
            }
            // Put the new entry placeholder at the end.
            if (10 >= (xLimit - xOffset)) {
                yOffset += LINE_HEIGHT;
                xOffset = LEFT_ADDRESS_INDENT;
                xLimit = parentWidth;
            }
            if (isEditable) {
                entryTextField.Frame = new RectangleF (xOffset, yOffset, (xLimit - xOffset), LINE_HEIGHT);
                xOffset += entryTextField.Frame.Width;
            }
            yOffset += LINE_HEIGHT;
            // Size the whole control
            this.Frame = new RectangleF (this.Frame.X, this.Frame.Y, parentWidth, yOffset);
        }

        public UcAddressField AddressFieldSuccessor (UcAddressField addressField)
        {
            var index = list.IndexOf (addressField);
            if (-1 == index) {
                return null; // address field not in this address block
            }
            var lastIndex = list.Count - 1;
            return (lastIndex == index ? entryTextField : list [index + 1]);
        }

        public UcAddressField AddressFieldPredecessor (UcAddressField addressField)
        {
            if (entryTextField == addressField) {
                return (0 < list.Count ? list[list.Count - 1]: null);
            }
            var index = list.IndexOf (addressField);
            if (-1 == index) {
                return null; // address field not in this address block
            }
            if (0 == index) {
                return null; // first field has no predecessor
            }
            return list [index - 1];
        }

        public class UcAddressFieldDelegate : UITextFieldDelegate
        {
            UcAddressBlock outer;

            public UcAddressFieldDelegate (UcAddressBlock outer)
            {
                this.outer = outer;
            }

            public override bool ShouldBeginEditing (UITextField textField)
            {
                var addressField = textField as UcAddressField;
                if (addressField.IsTextField ()) {
                    // Text fields are not editable and tap gesture recognizer will activate
                    // the gap in front or behind the field.
                    return false;
                }
                if ((0 == outer.isActive) && (null != outer.owner)) {
                    outer.owner.AddressBlockWillBecomeActive (outer);
                }
                outer.isActive += 1;
                outer.ConfigureView ();
                outer.SetCurrentAddressField (addressField);

                return true;
            }

            public override void EditingEnded (UITextField textField)
            {
                outer.isActive -= 1;
                if ((0 == outer.isActive) && (null != outer.owner)) {
                    outer.owner.AddressBlockWillBecomeInactive (outer);
                }
                outer.ConfigureView ();
            }

                
            /// We never want to change the characters.
            /// We are either deleting fields or popping up a chooser view.
            public override bool ShouldChangeCharacters (UITextField textField, NSRange range, string replacementString)
            {
                var addressField = textField as UcAddressField;

                // Check for delete key
                if (0 == replacementString.Length) {
                    ProcessDeleteKey (addressField);
                    return false;
                }

                switch (addressField.type) {
                case UcAddressField.ENTRY_FIELD:
                    if (null != outer.owner) {
                        outer.owner.AddressBlockAddContactClicked (outer, replacementString);
                    }
                    break;
                case UcAddressField.GAP_FIELD:
                    if (null != outer.owner) {
                        outer.owner.AddressBlockAddContactClicked (outer, replacementString);
                    }
                    break;
                case UcAddressField.TEXT_FIELD:
                    if (null != outer.owner) {
                        outer.owner.AddressBlockAddContactClicked (outer, replacementString);
                    }
                    break;
                default:
                    NcAssert.CaseError ();
                    break;
                }

                return false;
            }

            protected void Remove (UcAddressField address)
            {
                if (null == address) {
                    return;
                }
                address.Hidden = true;
                outer.list.Remove (address);
                address.RemoveFromSuperview ();
            }

            protected void ProcessDeleteKey (UcAddressField addressField)
            {
                NcAssert.True (!addressField.IsTextField ());
                var predecessor = outer.AddressFieldPredecessor (addressField);
                Remove (outer.AddressFieldPredecessor (predecessor));
                Remove (predecessor);
                outer.ConfigureView ();
            }
        }
    }
}

