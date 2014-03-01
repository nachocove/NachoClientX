//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using MonoTouch.CoreGraphics;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class RootElementWithIcon : RootElement
    {
        public RootElementWithIcon (string caption, Group group) : base (caption, group)
        {
        }

        public RootElementWithIcon (string caption) : base (caption, 0, 0)
        {
        }

        protected override NSString CellKey {
            get {
                return new NSString ("Nacho.RootElementWithIcon");
            }
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            var c = base.GetCell (tv);
            c.ImageView.Image = UIImage.FromBundle ("ic_action_alarms").Scale (new SizeF (22.0f, 22.0f));
            c.TextLabel.TextColor = UIColor.Gray;
            c.DetailTextLabel.TextColor = UIColor.Black;
            return c;
        }
    }

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
            this.Image = CalendarItemViewController.DotWithColor (UIColor.Blue);
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
            this.Image = CalendarItemViewController.DotWithColor (UIColor.Clear);
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
        public StyledStringElementWithIcon (string caption, string value, string icon) : base (caption, value)
        {
            var image = UIImage.FromBundle (icon);
            this.Image = image.Scale (new SizeF (22.0f, 22.0f));
            this.Font = UIFont.SystemFontOfSize (15.0f);
            this.TextColor = UIColor.LightGray;
            this.DetailColor = UIColor.Black;
        }

        public StyledStringElementWithIcon (string caption, string icon) : this (caption, "", icon)
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
        public AttendeeElement (string name, string email, NcAttendeeStatus status) : base (name, email, UITableViewCellStyle.Value2)
        {
            switch (status) {
            case NcAttendeeStatus.Accept:
                this.Image = CalendarItemViewController.DotWithColor (UIColor.Green);
                break;
            case NcAttendeeStatus.Decline:
                this.Image = CalendarItemViewController.DotWithColor (UIColor.Red);
                break;
            case NcAttendeeStatus.NotResponded:
                this.Image = CalendarItemViewController.DotWithColor (UIColor.Gray);
                break;
            case NcAttendeeStatus.ResponseUnknown:
                this.Image = CalendarItemViewController.DotWithColor (UIColor.LightGray);
                break;
            case NcAttendeeStatus.Tentative:
                this.Image = CalendarItemViewController.DotWithColor (UIColor.Yellow);
                break;
            }
        }
    }
}

