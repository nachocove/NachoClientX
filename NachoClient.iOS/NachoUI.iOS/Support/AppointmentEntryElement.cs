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
    public class AppointmentEntryView : UIView
    {
        static UIFont Font = UIFont.SystemFontOfSize (17.0f);

        public DateTime StartDateTime { get; private set; }

        public DateTime EndDateTime { get; private set; }

        public bool AllDayEvent { get; private set; }

        static AppointmentEntryView ()
        {
        }

        public AppointmentEntryView ()
        {
            BackgroundColor = UIColor.White;
        }

        public void Update (DateTime startTime, DateTime endTime, bool allDayEvent)
        {
            this.AllDayEvent = allDayEvent;
            this.StartDateTime = startTime;
            this.EndDateTime = endTime;
            SetNeedsDisplay ();
        }

        public override void Draw (RectangleF rect)
        {
            const int padright = 2;

            var startString = StartDateTime.ToString ("ddd, MMM d - h:mm tt");
            var endString = EndDateTime.ToString ("ddd, MMM d - h:mm tt");
           
            UIColor.Gray.SetColor ();
            DrawString ("Start", new RectangleF (50.0f, 7.0f, 100.0f, 22.0f), Font, UILineBreakMode.Clip, UITextAlignment.Left);
            if (!AllDayEvent) {
                DrawString ("End", new RectangleF (50.0f, 49.0f, 100.0f, 22.0f), Font, UILineBreakMode.Clip, UITextAlignment.Left);
            }

            UIColor.Black.SetColor ();
            DrawString (startString, new RectangleF (0.0f, 7.0f, rect.Width - padright, 22.0f), Font, UILineBreakMode.Clip, UITextAlignment.Right);
            if (AllDayEvent) {
                DrawString ("ALL DAY", new RectangleF (0.0f, 49.0f, rect.Width - padright, 22.0f), Font, UILineBreakMode.Clip, UITextAlignment.Right);
            } else {
                DrawString (endString, new RectangleF (0.0f, 49.0f, rect.Width - padright, 22.0f), Font, UILineBreakMode.Clip, UITextAlignment.Right);
            }
        }
    }

    public class AppointmentEntryElement : Element, IElementSizing
    {
        static NSString mKey = new NSString ("AppointmentElement");
        public DateTime startDateTime;
        public DateTime endDateTime;
        public bool allDayEvent;

        class AppointmentCell : UITableViewCell
        {
            AppointmentEntryView view;

            public AppointmentCell () : base (UITableViewCellStyle.Default, mKey)
            {
                view = new AppointmentEntryView ();
                ContentView.Add (view);
                Accessory = UITableViewCellAccessory.DisclosureIndicator;
                ImageView.Image = UIImage.FromBundle ("ic_action_time").Scale(new SizeF (22.0f, 22.0f));
            }

            public void Update (AppointmentEntryElement ae)
            {
                view.Update (ae.startDateTime, ae.endDateTime, ae.allDayEvent);
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                view.Frame = ContentView.Bounds;
                view.SetNeedsDisplay ();
            }
        }

        public AppointmentEntryElement (DateTime startDateTime, DateTime endDateTime) : base ("")
        {
            this.startDateTime = startDateTime;
            this.endDateTime = endDateTime;
        }

        public AppointmentEntryElement (Action<DialogViewController,UITableView,NSIndexPath> tapped) : base ("")
        {
            Tapped += tapped;
        }

        public override UITableViewCell GetCell (UITableView tv)
        {
            var cell = tv.DequeueReusableCell (mKey) as AppointmentCell;
            if (cell == null) {
                cell = new AppointmentCell ();
            }
            cell.Update (this);
            return cell;
        }

        public float GetHeight (UITableView tableView, NSIndexPath indexPath)
        {
            return 78;
        }

        public event Action<DialogViewController, UITableView, NSIndexPath> Tapped;

        public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
        {
            if (Tapped != null) {
                Tapped (dvc, tableView, path);
            }
        }
    }
}

