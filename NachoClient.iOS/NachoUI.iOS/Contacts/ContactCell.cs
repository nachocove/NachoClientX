//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;

using Foundation;
using UIKit;
using CoreGraphics;

using NachoCore.Model;

namespace NachoClient.iOS
{
    public class ContactCell : SwipeTableViewCell, ThemeAdopter
    {
        PortraitView PortraitView;
        nfloat PortraitSize = 40.0f;

        #region Creating a Cell

        public ContactCell (IntPtr handle) : base (handle)
        {
            PortraitView = new PortraitView (new CGRect (0.0f, 0.0f, PortraitSize, PortraitSize));
            ContentView.AddSubview (PortraitView);
            SeparatorInset = new UIEdgeInsets (0.0f, 64.0f, 0.0f, 0.0f);
            HideDetailWhenEmpty = true;
        }

        #endregion

        #region Theme

        Theme adoptedTheme = null;

        public void AdoptTheme (Theme theme)
        {
            if (theme != adoptedTheme) {
                adoptedTheme = theme;
                TextLabel.Font = theme.BoldDefaultFont.WithSize (17.0f);
                TextLabel.TextColor = theme.TableViewCellMainLabelTextColor;
                DetailTextLabel.Font = theme.DefaultFont.WithSize (14.0f);
                DetailTextLabel.TextColor = theme.TableViewCellDetailLabelTextColor;
                //DateLabel.Font = theme.DefaultFont.WithSize (14.0f);
                //DateLabel.TextColor = theme.TableViewCellDateLabelTextColor;
                //PortraitView.AdoptTheme (theme);
                //if (_ThreadIndicator != null) {
                //    _ThreadIndicator.AdoptTheme (theme);
                //}
            }
        }

        #endregion

        #region Setting Data

        public void SetContact (McContact contact, string alternateEmail = null)
        {
            var name = contact.GetDisplayName ();
            var email = alternateEmail ?? contact.GetPrimaryCanonicalEmailAddress ();
            var phone = contact.GetPrimaryPhoneNumber ();

            var theme = adoptedTheme ?? Theme.Active;

            TextLabel.TextColor = theme.TableViewCellMainLabelTextColor;
            if (!String.IsNullOrEmpty (name)) {
                TextLabel.Text = name;

                if (!String.IsNullOrEmpty (email)) {
                    DetailTextLabel.Text = email;
                } else if (!String.IsNullOrEmpty (phone)) {
                    DetailTextLabel.Text = phone;
                } else {
                    DetailTextLabel.Text = "";
                }
            } else {
                if (!String.IsNullOrEmpty (email)) {
                    TextLabel.Text = email;
                    if (!String.IsNullOrEmpty (phone)) {
                        DetailTextLabel.Text = phone;
                    } else {
                        DetailTextLabel.Text = "";
                    }
                } else if (!String.IsNullOrEmpty (phone)) {
                    TextLabel.Text = phone;
                    DetailTextLabel.Text = "";
                } else {
                    TextLabel.Text = NSBundle.MainBundle.LocalizedString ("Unnamed (contact list)", "Fallback text for contact with no name");
                    TextLabel.TextColor = theme.DisabledTextColor;
                    DetailTextLabel.Text = "";
                }
            }

            PortraitView.SetPortrait (contact.PortraitId, contact.CircleColor, NachoCore.Utils.ContactsHelper.GetInitials (contact));
        }

        #endregion

        #region Layout

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            PortraitView.Center = new CGPoint (SeparatorInset.Left / 2.0f, 12.0f + PortraitView.Frame.Height / 2.0f);
        }

        #endregion
    }
}
