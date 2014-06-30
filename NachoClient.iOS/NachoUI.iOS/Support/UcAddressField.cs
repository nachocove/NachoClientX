using System;
using System.Drawing;
using System.Collections.Generic;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class UcAddressField : UITextField
    {
        // Shows the contact
        public const int TEXT_FIELD = 1;
        // Shows the separating comma
        public const int COMMA_FIELD = 2;
        // Text field at the end of the list
        public const int ENTRY_FIELD = 3;

        public int type;
        public bool selected;
        public NcEmailAddress address;

        public UcAddressField (int type)
        {
            this.type = type;
        }

        // Return null to hide the caret.  We want it hidden in a text field.
        public override RectangleF GetCaretRectForPosition (UITextPosition position)
        {
            if (TEXT_FIELD == type) {
                return RectangleF.Empty;
            } else if (null == position) {
                return RectangleF.Empty;
            } else {
                return base.GetCaretRectForPosition (position);
            }
        }
         
    };
}

