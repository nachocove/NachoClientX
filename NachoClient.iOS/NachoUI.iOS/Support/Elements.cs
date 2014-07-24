//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.CoreGraphics;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.iOS
{
    public class RootElementWithIcon : RootElement
    {
        protected string name;

        /// <summary>
        /// Create a root element that displays an icon.
        /// </summary>
        /// <param name="name">The resource name of the icon.</param>
        public RootElementWithIcon (string icon, string caption, Group group) : base (caption, group)
        {
            this.name = icon;
        }

        public RootElementWithIcon (string icon, string caption) : base (caption, 0, 0)
        {
            this.name = icon;
        }

        protected override NSString CellKey {
            get {
                return new NSString ("Nacho.RootElementWithIcon");
            }
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            var c = base.GetCell (tv);
            c.ImageView.Image = UIImage.FromBundle (name);
            c.TextLabel.TextColor = UIColor.Gray;
            c.DetailTextLabel.TextColor = UIColor.LightGray;
            c.TextLabel.Font = A.Font_AvenirNextRegular14;
            c.DetailTextLabel.Font = A.Font_AvenirNextRegular14;
            return c;
        }
    }

    /// <summary>
    /// A section that doesn't take up screen height
    /// </summary>
    public class ThinSection : Section
    {
        public ThinSection () : base ()
        {
            this.HeaderView = new UIView (new RectangleF (0.0f, 0.0f, 1.0f, 30.0f));
            this.FooterView = new UIView (new RectangleF (0.0f, 0.0f, 1.0f, 1.0f));
        }

        public ThinSection(UIColor color) : this()
        {
            this.HeaderView.BackgroundColor = color;
            this.FooterView.BackgroundColor = color;
        }
    }

    public class SuperThinSection : Section
    {
        public SuperThinSection () : base ()
        {
            this.HeaderView = new UIView (new RectangleF (0.0f, 0.0f, 1.0f, 1.0f));
            this.FooterView = new UIView (new RectangleF (0.0f, 0.0f, 1.0f, 1.0f));
        }

        public SuperThinSection(UIColor color) : this()
        {
            this.HeaderView.BackgroundColor = color;
            this.FooterView.BackgroundColor = color;
        }
    }

    public class LowerSection : Section
    {
        public LowerSection (string headerSize) : base (headerSize)
        {
            this.HeaderView = new UIView (new RectangleF (0.0f, 0.0f, 1.0f, float.Parse(headerSize)));
            this.FooterView = new UIView (new RectangleF (0.0f, 0.0f, 1.0f, 1.0f));
        }


    }

    /// <summary>
    /// A section that doesn't take up screen height
    /// </summary>
    public class SectionWithLineSeparator : Section
    {
        public SectionWithLineSeparator () : base ()
        {
            this.HeaderView = new UIView (new RectangleF (0.0f, 0.0f, 1.0f, 1.0f));
            this.FooterView = new UIView (new RectangleF (0.0f, 0.0f, 1.0f, 1.0f));
        }
    }

    /// <summary>
    /// Radio group with a text field for freestyle entry.
    /// </summary>
    public class ReminderSection : Section
    {
        List<CheckboxElementWithData> list;
        NumericEntryElementWithCheckmark custom;
        HiddenElement hidden;

        public ReminderSection (uint initialValue)
        {
            list = new List<CheckboxElementWithData> ();

            hidden = new HiddenElement ("");
            this.Add (hidden);

            CreateCheckboxElementWithData (Pretty.ReminderString (1), 1);
            CreateCheckboxElementWithData (Pretty.ReminderString (5), 5);
            CreateCheckboxElementWithData (Pretty.ReminderString (60), 60);
            CreateCheckboxElementWithData (Pretty.ReminderString (24 * 60), 24 * 60);
            CreateCheckboxElementWithData (Pretty.ReminderString (0), 0);

            custom = new NumericEntryElementWithCheckmark ("", "Custom", "", false);
            custom.ClearButtonMode = UITextFieldViewMode.WhileEditing;
            custom.KeyboardType = UIKeyboardType.Default;
            this.Add (custom);

            bool found = false;
            foreach (var l in list) {
                if (initialValue == l.Data) {
                    hidden.SetSummary (l.Summary (), l.Data);
                    l.Value = true;
                    found = true;
                    break;
                }
            }

            if (!found) {
                custom.checkmark = true;
                custom.Value = Pretty.ReminderString (initialValue);
                hidden.SetSummary (custom.Value, initialValue);
            }

            custom.EntryStarted += delegate {
                foreach (var l in list) {
                    l.Value = false;
                }
                custom.checkmark = true;
                custom.GetImmediateRootElement ().Reload (this, UITableViewRowAnimation.None);
                custom.BecomeFirstResponder (true);
            };
            custom.EntryEnded += delegate {
                if (custom.checkmark) {
                    if (String.IsNullOrEmpty (custom.Value)) {
                        hidden.SetSummary ("None", 0);
                    } else {
                        hidden.SetSummary (Pretty.ReminderString (custom.NumericValue), custom.NumericValue);
                    }
                }
                custom.GetImmediateRootElement ().Reload (this, UITableViewRowAnimation.None);
            };
        }





        protected CheckboxElementWithData CreateCheckboxElementWithData (string caption, uint data)
        {
            var c = new CheckboxElementWithData (caption, data);
            this.Add (c);
            list.Add (c);

            c.Tapped += () => {
                foreach (var l in list) {
                    l.Value = false;
                }
                c.Value = true;
                custom.checkmark = false;
                hidden.SetSummary (c.Summary (), c.Data);
                c.GetImmediateRootElement ().Reload (this, UITableViewRowAnimation.None);
            };

            return c;
        }
    }

    /// <summary>
    /// Hidden element useful holding summary data for a section.
    /// Section does allow Summary to override, not does it have a value.
    /// </summary>
    public class HiddenElement : OwnerDrawnElement
    {
        string summary;
        public uint Value;

        public HiddenElement (string summary) : base (UITableViewCellStyle.Default, "Nacho.HiddenElement")
        {
            this.summary = summary;
        }

        public void SetSummary (string summary, uint value)
        {
            this.summary = summary;
            this.Value = value;
        }

        public override string Summary ()
        {
            return summary;
        }

        public override void Draw (RectangleF bounds, CGContext context, UIView view)
        {
            UIColor.White.SetFill ();
            context.FillRect (bounds);
        }

        public override float Height (RectangleF bounds)
        {
            return 0.0f;
        }
    }

    /// <summary>
    /// Checkbox element with associated data item useful
    /// for getting a value associated with a selected item.
    /// </summary>
    public class CheckboxElementWithData : CheckboxElement
    {
        public uint Data { get; set; }

        public CheckboxElementWithData (string caption, uint data) : base (caption)
        {
            this.Data = data;
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            var cell = base.GetCell (tv);
            tv.SeparatorColor = A.Color_NachoSeparator;
            cell.TextLabel.Font = A.Font_AvenirNextRegular14;
            cell.TintColor = A.Color_NachoBlue;
            return cell;
        }
    }

    public class EntryElementWithIcon : EntryElement
    {
        protected UIImage icon { get; private set; }
        protected bool numericEntry { get; private set; }

        protected UITextField textField;

        protected override UITextField CreateTextField (RectangleF frame)
        {
            textField = base.CreateTextField (new RectangleF (45, 12, 260, 24));
            textField.Font = A.Font_AvenirNextRegular14;
            textField.TintColor = A.Color_NachoBlue;


//            textField.ShouldChangeCharacters = (textField, range, replacementString) => {
//                var newLength = textField.Text.Length + replacementString.Length - range.Length;
//                return newLength <= 25;
//            };

            if (numericEntry) {
                if (textField.Text.Length == 3) {
                    textField.Text = textField.Text + "-";
                }
                this.KeyboardType = UIKeyboardType.PhonePad;
            } else {
                this.KeyboardType = UIKeyboardType.Default;
            }
            return textField;
        }


        public EntryElementWithIcon (UIImage icon, string caption, string placeholder, string value, bool numericEntry) : base (caption, placeholder, value)
        {
            this.icon = icon;
            this.numericEntry = numericEntry;
        }

        protected override NSString CellKey {
            get {
                return new NSString ("Nacho.EntryElementWithIcon");
            }
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            var cell = base.GetCell (tv);
            cell.ImageView.Image = icon;
            return cell;
        }
    }

    public class EntryElementWithSettings : EntryElement
    {
        protected UIImage settingsIcon { get; private set; }

        protected UITextField textField;


        public EntryElementWithSettings (UIImage settingsIcon, string placeholder, string value) : base ("", placeholder, value)
        {
            this.settingsIcon = settingsIcon;
        }

        protected override UITextField CreateTextField (RectangleF frame)
        {
            textField = base.CreateTextField (new RectangleF (18, 12, 260, 24));
            textField.Font = A.Font_AvenirNextRegular14;
            textField.TintColor = A.Color_NachoBlue;

            return textField;
        }

        protected override NSString CellKey {
            get {
                return new NSString ("Nacho.EntryElementWithSettings");
            }
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            UIImageView accessoryImage = new UIImageView(new RectangleF(0, 0, 24, 24));
            accessoryImage.Image = settingsIcon;
            var cell = base.GetCell (tv);
            cell.AccessoryView = accessoryImage;
            return cell;
        }
    }

    public class EntryElementWithCheckmark: EntryElement
    {
        public bool checkmark{ get; set; }

        public EntryElementWithCheckmark (string caption, string placeholder, string value, bool checkmark) : base (caption, placeholder, value)
        {
            this.checkmark = checkmark;
        }

        protected override NSString CellKey {
            get {
                return new NSString ("Nacho.EntryElementWithCheckmark");
            }
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            var cell = base.GetCell (tv);
            if (checkmark) {
                cell.Accessory = UITableViewCellAccessory.Checkmark;
            } else {
                cell.Accessory = UITableViewCellAccessory.None;
            }
            return cell;
        }
    }

    public class NumericEntryElementWithCheckmark: EntryElementWithCheckmark
    {
        public uint NumericValue {
            get {
                if (String.IsNullOrEmpty (this.Value)) {
                    return 0;
                }
                ;
                uint result;
                if (uint.TryParse (this.Value, out result)) {
                    return result;
                }
                return 0;
            }
        }

        public NumericEntryElementWithCheckmark (string caption, string placeholder, string value, bool checkmark) : base (caption, placeholder, value, checkmark)
        {
            this.checkmark = checkmark;
        }

        protected override NSString CellKey {
            get {
                return new NSString ("Nacho.NumericEntryElementWithCheckmark");
            }
        }

        protected override UITextField CreateTextField (RectangleF frame)
        {
            UITextField tf = base.CreateTextField (frame);
            tf.ShouldChangeCharacters += delegate (UITextField textField, NSRange range, string replacementString) {
                if (String.IsNullOrEmpty (replacementString)) {
                    return true;
                }
                uint result;
                var testString = textField.Text.Remove (range.Location, range.Length);
                testString = testString.Insert (range.Location, replacementString);
                if (false == uint.TryParse (testString, out result)) {
                    return false;
                }
                return true;
            };
            tf.Font = A.Font_AvenirNextRegular14;
            tf.TintColor = A.Color_NachoBlue;
            return tf;
        }
    }

    public class SubjectElement : StyledMultilineElement
    {
        public SubjectElement (string caption) : base (caption)
        {
            // TODO: use color associated with calendar
            this.Image = NachoClient.Util.DotWithColor (UIColor.Blue);
            this.Font = A.Font_AvenirNextRegular14;
        }
    }

    public class PeopleEntryElement : StyledStringElement
    {
        public PeopleEntryElement () : base ("People")
        {
            this.Accessory = UITableViewCellAccessory.DisclosureIndicator;
            var image = UIImage.FromBundle ("icn-peoples");
            this.Image = image;
            this.Font = A.Font_AvenirNextRegular14;
            this.TextColor = UIColor.Gray;
        }
    }

    public class CustomEntryElement : StyledStringElement
    {
        public CustomEntryElement (UIImage image, string caption) : base (caption)
        {
            this.Accessory = UITableViewCellAccessory.DisclosureIndicator;
            this.Image = image;
            this.Font = A.Font_AvenirNextRegular14;
            this.TextColor = UIColor.Gray;
        }
    }

    public class CustomEntryElementDetail : StyledStringElement
    {
        public CustomEntryElementDetail (UIImage image, string caption, string detail) : base (caption, detail)
        {
            this.Accessory = UITableViewCellAccessory.DisclosureIndicator;
            this.Image = image;
            this.Font = A.Font_AvenirNextRegular14;
            this.TextColor = UIColor.Gray;
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            var cell = base.GetCell (tv);
            cell.TextLabel.Font = A.Font_AvenirNextRegular14;
            cell.TextLabel.TextColor = UIColor.Gray;
            cell.DetailTextLabel.Font = A.Font_AvenirNextRegular14;
            cell.DetailTextLabel.TextColor = UIColor.LightGray;
            return cell;
        }
    }

    public class SubjectEntryElement : EntryElement
    {
        public SubjectEntryElement (string value) : base ("Subject", "", value)
        {
            this.Value = value;
        }

        protected override NSString CellKey {
            get {
                return new NSString ("Nacho.SubjectEntryElement");
            }
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            var cell = base.GetCell (tv);
            cell.TextLabel.Font = UIFont.SystemFontOfSize (19.0f);
            cell.TextLabel.TextColor = UIColor.Gray;
            return cell;
        }
    }

    public class DateTimeEntryElement : DateTimeElement
    {
        public DateTimeEntryElement (string caption) : base (caption, DateTime.Now)
        {
        }
    }

    public class StartTimeElement : StyledStringElement
    {
        public StartTimeElement (string caption) : base (caption)
        {
            this.Font = A.Font_AvenirNextRegular14;
        }
    }

    public class StartTimeElementWithIconIndent : StyledStringElement
    {
        public StartTimeElementWithIconIndent (string caption) : base (caption)
        {
            // Add (invisible) image to get the proper indentation
            this.Image = NachoClient.Util.DotWithColor (UIColor.Clear);
            this.Font = A.Font_AvenirNextRegular14;
        }
    }

    public class DurationElement : StyledStringElement
    {
        public DurationElement (string caption) : base (caption)
        {
            using (var image = UIImage.FromBundle ("icn-mtng-time")) {
                this.Image = image;
                this.Font = A.Font_AvenirNextRegular14;
            }
        }
    }

    public class StyledStringElementWithIcon : StyledStringElement
    {
        public StyledStringElementWithIcon (string caption, string value, UIImage icon) : base (caption, value)
        {
            this.Image = icon;
            this.Font = A.Font_AvenirNextRegular14;
            this.TextColor = UIColor.LightGray;
            this.DetailColor = UIColor.Black;
        }

        public StyledStringElementWithIcon (string caption, UIImage icon) : this (caption, "", icon)
        {
            this.Font = A.Font_AvenirNextRegular14;
            this.TextColor = UIColor.Black;
        }
    }

    public class LocationElement : StyledMultilineElement
    {
        public LocationElement (string caption) : base (caption)
        {
            this.Image = UIImage.FromBundle ("icn-mtng-location");
            this.Font = A.Font_AvenirNextRegular14;
        }
    }

    class AttendeeElement : StyledStringElement
    {
        public AttendeeElement (string name, string email, NcAttendeeStatus status) : base (name, email, UITableViewCellStyle.Subtitle)
        {
            switch (status) {
            case NcAttendeeStatus.Accept:
                this.Image = NachoClient.Util.DotWithColor (UIColor.Green);
                break;
            case NcAttendeeStatus.Decline:
                this.Image = NachoClient.Util.DotWithColor (UIColor.Red);
                break;
            case NcAttendeeStatus.NotResponded:
                this.Image = NachoClient.Util.DotWithColor (UIColor.Gray);
                break;
            case NcAttendeeStatus.ResponseUnknown:
                this.Image = NachoClient.Util.DotWithColor (UIColor.LightGray);
                break;
            case NcAttendeeStatus.Tentative:
                this.Image = NachoClient.Util.DotWithColor (UIColor.Yellow);
                break;
            }
        }
    }

    class StyledStringElementWithDot : StyledStringElement
    {
        public StyledStringElementWithDot (string caption, string value, UIColor color) : base (caption, value)
        {
            this.Image = NachoClient.Util.DotWithColor (color);
            this.Font = A.Font_AvenirNextRegular14;
            this.TextColor = UIColor.LightGray;
            this.DetailColor = UIColor.Black;
        }

        public StyledStringElementWithDot (string caption, UIColor color) : this (caption, "", color)
        {
            this.Font = A.Font_AvenirNextRegular14;
            this.TextColor = UIColor.Black;
        }
    }

    public class StyledStringElementWithIndent : StyledStringElement
    {
        public StyledStringElementWithIndent (string caption) : base (caption)
        {
            // Add (invisible) image to get the proper indentation
            this.Image = NachoClient.Util.DotWithColor (UIColor.Clear);
            this.TextColor = UIColor.Gray;
            this.Font = A.Font_AvenirNextRegular14;
        }
    }

    public class CustomTextInputElement : StyledStringElement
    {
        string theDetail;
        UITextField inputText;

        public CustomTextInputElement (UIImage image, string caption, string detail, UITextField inputText) : base (caption)
        {
            this.Accessory = UITableViewCellAccessory.None;
            this.Image = image;
            this.Font = A.Font_AvenirNextRegular14;
            this.TextColor = UIColor.Black;
            this.theDetail = detail;
            this.inputText = inputText;
            inputText.TextColor = UIColor.Gray;
            inputText.Text = theDetail;
        }
        public override UITableViewCell GetCell (UITableView tv)
        {
            var cell = base.GetCell (tv);

            if (cell.ContentView.Subviews.Length > 1) {
                for (int i = 1; i < cell.ContentView.Subviews.Length; i++) {
                    UIView x = cell.ContentView.Subviews [i];
                    x.RemoveFromSuperview ();
                }
            }

            inputText.Font = A.Font_AvenirNextMedium14;
            inputText.Frame = new RectangleF (150, 0, cell.Frame.Width - 150, cell.Frame.Height);
            inputText.TextAlignment = UITextAlignment.Left;
            inputText.ReturnKeyType = UIReturnKeyType.Default;
            cell.ContentView.Add (inputText);

            return cell;
        }
    }    

    public class SignatureEntryElement : StyledStringElement
    {
        UILabel signatureText;

        public SignatureEntryElement (string caption, UILabel signatureText) : base (caption)
        {
            this.Accessory = UITableViewCellAccessory.DisclosureIndicator;
            this.Font = A.Font_AvenirNextRegular14;
            this.TextColor = UIColor.Black;
            this.signatureText = signatureText;
        }


        public override UITableViewCell GetCell (UITableView tv)
        {
            var cell = base.GetCell (tv);

            if (cell.ContentView.Subviews.Length > 1) {
                for (int i = 1; i < cell.ContentView.Subviews.Length; i++) {
                    UIView x = cell.ContentView.Subviews [i];
                    x.RemoveFromSuperview ();
                }
            }

            signatureText.Frame = new RectangleF (150, 0, cell.Frame.Width - 170, cell.Frame.Height);
            signatureText.Font = A.Font_AvenirNextMedium14;
            signatureText.TextColor = UIColor.Gray;
            signatureText.TextAlignment = UITextAlignment.Left;
            cell.ContentView.Add (signatureText);

            return cell;
        }
    }  

    public class StyledMultilineElementWithIndent : StyledMultilineElement
    {
        public StyledMultilineElementWithIndent (string caption) : base (caption)
        {
            // Add (invisible) image to get the proper indentation
            this.Image = NachoClient.Util.DotWithColor (UIColor.Clear);
            this.TextColor = UIColor.Gray;
            this.Font = A.Font_AvenirNextRegular14;
        }
    }

    public class StyledMultiLineTextInput : MultilineEntryElement
    {
        string theDetail;
        UITextView inputText;

        public StyledMultiLineTextInput (string caption, string detail, UITextView inputText) : base(caption, detail, inputText.Frame.Height, false)
        {

            this.theDetail = detail;
            this.inputText = inputText;
            this.inputText.Editable = true;
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            var cell = base.GetCell (tv);

            if (cell.ContentView.Subviews.Length > 1) {
                for (int i = 1; i < cell.ContentView.Subviews.Length; i++) {
                    UIView x = cell.ContentView.Subviews [i];
                    x.RemoveFromSuperview ();
                }
            }

            cell.Frame = new RectangleF (0,0,320,inputText.Frame.Height);
            tv.SeparatorColor = UIColor.White;
            inputText.Editable = true;
            inputText.Font = A.Font_AvenirNextMedium14;
            inputText.TextColor = UIColor.Gray;
            inputText.Text = theDetail;
            inputText.TextAlignment = UITextAlignment.Left;
            inputText.ReturnKeyType = UIReturnKeyType.Default;
            cell.ContentView.Add (inputText);

            return cell;
        }
    }

    class RadioElementWithDot : RadioElement
    {
        protected UIColor color;

        public RadioElementWithDot (string caption, UIColor color) : base (caption)
        {
            this.color = color;

        }

        protected override NSString CellKey {
            get {
                return new NSString ("Nacho.RadioElementWithDot");
            }
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            var cell = base.GetCell (tv);
            tv.SeparatorColor = A.Color_NachoSeparator;
            cell.ImageView.Image = NachoClient.Util.DrawCalDot (color);
            cell.TextLabel.Font = A.Font_AvenirNextRegular14;
            cell.TintColor = A.Color_NachoBlue;
            return cell;
        }
    }

    class CalendarRadioElementSection : Section
    {
        public CalendarRadioElementSection (NachoFolders calendars) : base ("")
        {
            // TODO: Arrange by account
            for (int i = 0; i < calendars.Count (); i++) {
                var c = calendars.GetFolder (i);
                // TODO: Get color from calendar
                var e = new RadioElementWithDot (c.DisplayName, UIColor.Green);
                this.Add (e);
            }

        }
            
    }

    class RadioElementWithData : RadioElement
    {
        public string data;

        public RadioElementWithData (string caption, string group, string data) : base (caption, group)
        {
            this.data = data;
        }

        public RadioElementWithData (string caption, string data) : base (caption)
        {
            this.data = data;
        }

        public static string SelectedData(RootElement root)
        {
            var section = root [0];
            var element = section [root.RadioSelected] as RadioElementWithData;
            return element.data;
        }

    }
}

