//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
namespace NachoClient.iOS
{
    public class ButtonCell : SwipeTableViewCell, ThemeAdopter
    {

        public static nfloat PreferredHeight = 44.0f;

        public ButtonCell (IntPtr handle) : base (handle)
        {
        }

        public void AdoptTheme (Theme theme)
        {
            TextLabel.Font = theme.DefaultFont.WithSize (14.0f);
            TextLabel.TextColor = theme.TableViewCellMainLabelTextColor;
            var accessory = AccessoryView;
            if (accessory != null) {
                accessory.TintColor = theme.TableViewCellActionAccessoryColor;
            }
            SetNeedsLayout ();
        }
    }
}
