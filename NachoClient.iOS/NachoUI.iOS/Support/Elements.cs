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

namespace NachoClient.iOS
{
    public class RootElementWithIcon : RootElement
    {
        protected string name;

        /// <summary>
        /// Create a root element that displays an icon.
        /// </summary>
        /// <param name="name">The resource name of the icon.</param>
        public RootElementWithIcon (string name, string caption, Group group) : base (caption, group)
        {
            this.name = name;
        }

        public RootElementWithIcon (string name, string caption) : base (caption, 0, 0)
        {
            this.name = name;
        }

        protected override NSString CellKey {
            get {
                return new NSString ("Nacho.RootElementWithIcon");
            }
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            var c = base.GetCell (tv);
            c.ImageView.Image = UIImage.FromBundle (name).Scale (new SizeF (22.0f, 22.0f));
            c.TextLabel.TextColor = UIColor.Gray;
            c.DetailTextLabel.TextColor = UIColor.Black;
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
            this.HeaderView = new UIView (new RectangleF (0.0f, 0.0f, 1.0f, 15.0f));
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

            custom = new NumericEntryElementWithCheckmark ("Custom", "", "", false);
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
    }

    public class EntryElementWithIcon : EntryElement
    {
        protected UIImage icon { get; private set; }

        public EntryElementWithIcon (UIImage icon, string placeholder, string value) : base ("", placeholder, value)
        {
            this.icon = icon;
        }

        protected override NSString CellKey {
            get {
                return new NSString ("Nacho.EntryElementWithIcon");
            }
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            var cell = base.GetCell (tv);
            var textField = cell.ContentView.ViewWithTag (1);
            var textFieldframe = textField.Frame;
            textFieldframe.Location = new PointF (50.0f, textFieldframe.Location.Y);
            textField.Frame = textFieldframe;
            cell.ImageView.Image = icon;
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
            return tf;
        }
    }

    public class SubjectElement : StyledMultilineElement
    {
        public SubjectElement (string caption) : base (caption)
        {
            this.Image = NachoClient.Util.DotWithColor (UIColor.Blue);
            this.Font = UIFont.SystemFontOfSize (17.0f);
        }
    }

    public class PeopleEntryElement : StyledStringElement
    {
        public PeopleEntryElement () : base ("People")
        {
            this.Accessory = UITableViewCellAccessory.DisclosureIndicator;
            var image = UIImage.FromBundle ("ic_action_group");
            this.Image = image.Scale (new SizeF (22.0f, 22.0f));
            this.Font = UIFont.SystemFontOfSize (17.0f);
            this.TextColor = UIColor.Gray;
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
            // Add (invisible) image to get the proper indentation
            this.Image = NachoClient.Util.DotWithColor (UIColor.Clear);
            this.Font = UIFont.SystemFontOfSize (15.0f);
        }
    }

    public class DurationElement : StyledStringElement
    {
        public DurationElement (string caption) : base (caption)
        {
            var image = UIImage.FromBundle ("ic_action_time");
            this.Image = image.Scale (new SizeF (22.0f, 22.0f));
            this.Font = UIFont.SystemFontOfSize (15.0f);
        }
    }

    public class StyledStringElementWithIcon : StyledStringElement
    {
        public StyledStringElementWithIcon (string caption, string value, UIImage icon) : base (caption, value)
        {
            this.Image = icon;
            this.Font = UIFont.SystemFontOfSize (15.0f);
            this.TextColor = UIColor.LightGray;
            this.DetailColor = UIColor.Black;
        }

        public StyledStringElementWithIcon (string caption, UIImage icon) : this (caption, "", icon)
        {
            this.Font = UIFont.SystemFontOfSize (15.0f);
            this.TextColor = UIColor.Black;
        }
    }

    public class LocationElement : StyledMultilineElement
    {
        public LocationElement (string caption) : base (caption)
        {
            var image = UIImage.FromBundle ("ic_action_place");
            this.Image = image.Scale (new SizeF (22.0f, 22.0f));
            this.Font = UIFont.SystemFontOfSize (17.0f);
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
}

