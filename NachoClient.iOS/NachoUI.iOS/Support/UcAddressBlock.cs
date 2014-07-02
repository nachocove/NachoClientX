using System;
using System.Drawing;
using System.Collections.Generic;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class UcAddressBlock : UIView
    {
        protected int isActive;
        protected bool isCompact;

        protected float parentWidth;
        protected string topLeftString;
        protected IUcAddressBlockDelegate owner;

        protected int suppliedCount;
        protected UILabel moreLabel;
        protected UILabel topLeftLabel;
        protected UIButton chooserButton;
        protected UcAddressField entryTextField;

        protected List<UcAddressField> list;

        protected float LINE_HEIGHT = 30;
        protected float LEFT_LABEL_INDENT = 15;
        protected float LEFT_ADDRESS_INDENT = 57;
        protected float RIGHT_INDENT = 15;

        public UcAddressBlock (IUcAddressBlockDelegate owner, string label, float width)
        {
            this.owner = owner;
            this.topLeftString = label;
            this.parentWidth = width;
            this.BackgroundColor = UIColor.White;
            this.list = new List<UcAddressField> ();

            this.AutoresizingMask = UIViewAutoresizing.None;
            this.AutosizesSubviews = false;

            CreateView ();
        }

        public void SetCompact (bool isCompact, int moreCount)
        {
            this.isCompact = isCompact;
            this.suppliedCount = moreCount;
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

        protected void AppendInternal (string text, NcEmailAddress address, int type)
        {
            var a = new UcAddressField (type);
            a.ContentMode = UIViewContentMode.Left;
            a.Font = A.Font_AvenirNextRegular14;
            a.TextColor = A.Color_154750;
            a.Text = text;
            a.address = address;
            var aSize = a.StringSize (a.Text, a.Font);
            a.Frame = new RectangleF (PointF.Empty, aSize);
            a.Delegate = new UcAddressFieldDelegate (this);
            this.AddSubview (a);
            list.Add (a);
        }

        public void Append (NcEmailAddress address)
        {
            if (0 < list.Count) {
                AppendInternal (",", null, UcAddressField.COMMA_FIELD);
            }

            if (null == address.contact) {
                AppendInternal (address.address, address, UcAddressField.TEXT_FIELD);
            } else {
                AppendInternal (address.contact.DisplayName, address, UcAddressField.TEXT_FIELD);
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
            moreLabel.Font = A.Font_AvenirNextRegular12;
            moreLabel.TextColor = A.Color_808080;

            var moreTapGestureRecognizer = new UITapGestureRecognizer ();
            moreTapGestureRecognizer.NumberOfTapsRequired = 1;
            moreTapGestureRecognizer.AddTarget (this, new MonoTouch.ObjCRuntime.Selector ("MoreLabelTapSelector:"));
            moreLabel.AddGestureRecognizer (moreTapGestureRecognizer);
            moreLabel.UserInteractionEnabled = true;

            topLeftLabel = new UILabel ();
            topLeftLabel.Font = A.Font_AvenirNextRegular14;
            topLeftLabel.TextColor = A.Color_0B3239;

            chooserButton = UIButton.FromType (UIButtonType.ContactAdd);
            chooserButton.SizeToFit ();
            chooserButton.Frame = new RectangleF (parentWidth - chooserButton.Frame.Width - RIGHT_INDENT, 0, chooserButton.Frame.Width, chooserButton.Frame.Height);

            chooserButton.TouchUpInside += (object sender, EventArgs e) => {
                if (null != owner) {
                    owner.AddressBlockAddContactClicked (this, null);
                }
            };

            entryTextField = new UcAddressField (UcAddressField.ENTRY_FIELD);
            entryTextField.Font = A.Font_AvenirNextRegular14;
            entryTextField.TextColor = A.Color_808080;
            entryTextField.Text = " ";
            entryTextField.SizeToFit ();
            entryTextField.Delegate = new UcAddressFieldDelegate (this);

            this.AddSubviews (new UIView[] { topLeftLabel, moreLabel, chooserButton, entryTextField });

//            entryTextField.BackgroundColor = UIColor.Yellow;
        }

        [MonoTouch.Foundation.Export ("MoreLabelTapSelector:")]
        public void MoreLabelTapSelector (UIGestureRecognizer sender)
        {
            if (null != owner) {
                owner.AddressBlockWillBecomeActive (this);
            }
            entryTextField.selected = true;
            entryTextField.BecomeFirstResponder ();
            ConfigureView ();
        }

        public void ConfigureView ()
        {
            topLeftLabel.Text = topLeftString;
            var topLeftLabelSize = topLeftLabel.StringSize (topLeftString, topLeftLabel.Font);
            topLeftLabel.Frame = new RectangleF (topLeftLabel.Frame.Location, topLeftLabelSize);

            foreach (var address in list) {
                if (address.selected) {
                    if (UcAddressField.TEXT_FIELD == address.type) {
                        address.BackgroundColor = UIColor.Cyan;
                    }
                } else {
                    address.BackgroundColor = UIColor.White;
                }
            }
            if (null != owner) {
                owner.AddressBlockNeedsLayout (this);
            }
        }

        /// Adjusts x & y on the top line of a view
        protected void AdjustXY (UIView view, float X, float Y)
        {
            view.Center = new PointF (X + (view.Frame.Width / 2), LINE_HEIGHT / 2);
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
                var firstAddress = list [0];
                firstAddress.Hidden = false;
                AdjustXY (firstAddress, xOffset, yOffset);
                xOffset += firstAddress.Frame.Width;
            }

            var moreCount = suppliedCount;
            if (moreCount < 0) {
                moreCount = (list.Count + 1) / 2;  // count of text fields, assuming text+comma.
            }

            if (1 >= moreCount) {
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
            var yEntryTextField = yOffset + (LINE_HEIGHT / 2) - (entryTextField.Frame.Height / 2);
            entryTextField.Frame = new RectangleF (xOffset, yEntryTextField, (xLimit - xOffset), entryTextField.Frame.Height);
            xOffset += entryTextField.Frame.Width;
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
                                
            for (int i = 0; i < list.Count; i++) {
                var address = list [i];
                var remainingSpace = xLimit - xOffset;
                var requestedSpace = address.Frame.Width;
                // If there's a comma, add it into the request
                if ((i + 1) < list.Count) {
                    var c = list [i + 1];
                    if (UcAddressField.COMMA_FIELD == c.type) {
                        requestedSpace += c.Frame.Width;
                    }
                }
                // Force new row, except for commas, ...
                if (UcAddressField.COMMA_FIELD != address.type) {
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
            var yEntryTextField = yOffset + (LINE_HEIGHT / 2) - (entryTextField.Frame.Height / 2);
            entryTextField.Frame = new RectangleF (xOffset, yEntryTextField, (xLimit - xOffset), entryTextField.Frame.Height);
            xOffset += entryTextField.Frame.Width;
            yOffset += LINE_HEIGHT;
            // Size the whole control
            this.Frame = new RectangleF (this.Frame.X, this.Frame.Y, parentWidth, yOffset);
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
                addressField.selected = true;
                if ((0 == outer.isActive) && (null != outer.owner)) {
                    outer.owner.AddressBlockWillBecomeActive (outer);
                }
                outer.isActive += 1;
                outer.ConfigureView ();

                return true;
            }

            public override void EditingEnded (UITextField textField)
            {
                var addressField = textField as UcAddressField;
                addressField.selected = false;
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
                case UcAddressField.COMMA_FIELD:
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

            protected void Select (UcAddressField address)
            {
                address.selected = true;
                address.BecomeFirstResponder ();
            }

            protected void Remove (UcAddressField address)
            {
                outer.list.Remove (address);
                address.RemoveFromSuperview ();
            }

            protected void ProcessDeleteKey (UcAddressField addressField)
            {
                addressField.selected = false;

                switch (addressField.type) {
                case UcAddressField.TEXT_FIELD:
                    if (1 == outer.list.Count) {
                        Remove (addressField);
                        Select (outer.entryTextField);
                    } else {
                        var index = outer.list.IndexOf (addressField);
                        // Removing the last entry in the list?
                        if (0 != index) {
                            Remove (outer.list [index]); // Text field
                            Remove (outer.list [index - 1]); // Leading comma
                            Select (outer.list [index - 2]); // Select the new end of list
                        } else { // Removing from middle of the list
                            Remove (outer.list [index]); // Text field
                            Remove (outer.list [index]); // Following comma
                            Select (outer.list [index]); // Select the next item on the list
                        }
                    }
                    break;
                case UcAddressField.COMMA_FIELD:
                    var commaIndex = outer.list.IndexOf (addressField);
                    Select (outer.list [commaIndex - 1]);
                    break;
                case UcAddressField.ENTRY_FIELD:
                    if (0 != outer.list.Count) {
                        Select (outer.list [outer.list.Count - 1]);
                    } else {
                        Select (addressField);
                    }
                    break;
                default:
                    NcAssert.CaseError ();
                    break;
                }
                outer.ConfigureView ();
            }
        }


    }
}

