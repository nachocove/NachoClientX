// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

using System.IO;
using System.Drawing;
using System.Collections.Generic;


namespace NachoClient.iOS
{
    public partial class AlertChooserViewController : NcUIViewController
    {
        public AlertChooserViewController (IntPtr handle) : base (handle)
        {
        }

        public uint Reminder;
        List<uint> minValues = new List<uint>(new uint[] { 0, 1, 5, 15, 30, 60, 120, 1440, 2880, 10080 });

        UIColor separatorColor = A.Color_NachoBorderGray;
        protected static float SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;
        protected int LINE_OFFSET = 30;
        protected int CELL_HEIGHT = 44;
        protected float TEXT_LINE_HEIGHT = 19.124f;
        UIColor solidTextColor = A.Color_NachoBlack;

        protected UIView line1;
        protected UIView line2;
        protected UIView line3;
        protected UIView line4;
        protected UIView line5;
        protected UIView line6;
        protected UIView line7;
        protected UIView line8;
        protected UIView line9;
        protected UIView line10;
        protected UIView line11;

        protected UIView noneView;
        protected UIView atTimeView;
        protected UIView fiveMinView;
        protected UIView fifteenMinView;
        protected UIView thirtyMinView;
        protected UIView oneHourView;
        protected UIView twoHourView;
        protected UIView oneDayView;
        protected UIView twoDayView;
        protected UIView oneWeekView;


        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            CreateAlertView ();
            ConfigureAlertView ();

        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return this.NavigationController.TopViewController == this;
            }
        }

        public void SetReminder (uint reminder)
        {
            this.Reminder = reminder;

        }

        public uint GetReminder ()
        {

            return this.Reminder;
        }

        protected void CreateAlertView ()
        {
            NavigationItem.Title = "Alert";
            Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);

            //None
            noneView = MakeCheckCell (101, "None", 0f, LINE_OFFSET, SCREEN_WIDTH, CELL_HEIGHT);
            atTimeView = MakeCheckCell (102, "At time of event", 0f, LINE_OFFSET + CELL_HEIGHT, SCREEN_WIDTH, CELL_HEIGHT);
            fiveMinView = MakeCheckCell (103, "5 minutes before", 0f, LINE_OFFSET + (CELL_HEIGHT * 2), SCREEN_WIDTH, CELL_HEIGHT);
            fifteenMinView = MakeCheckCell (104, "15 minutes before", 0f, LINE_OFFSET + (CELL_HEIGHT * 3), SCREEN_WIDTH, CELL_HEIGHT);
            thirtyMinView = MakeCheckCell (105, "30 minutes before", 0f, LINE_OFFSET + (CELL_HEIGHT * 4), SCREEN_WIDTH, CELL_HEIGHT);
            oneHourView = MakeCheckCell (106, "1 hour before", 0f, LINE_OFFSET + (CELL_HEIGHT * 5), SCREEN_WIDTH, CELL_HEIGHT);
            twoHourView = MakeCheckCell (107, "2 hours before", 0f, LINE_OFFSET + (CELL_HEIGHT * 6), SCREEN_WIDTH, CELL_HEIGHT);
            oneDayView = MakeCheckCell (108, "1 day before", 0f, LINE_OFFSET + (CELL_HEIGHT * 7), SCREEN_WIDTH, CELL_HEIGHT);
            twoDayView = MakeCheckCell (109, "2 days before", 0f, LINE_OFFSET + (CELL_HEIGHT * 8), SCREEN_WIDTH, CELL_HEIGHT);
            oneWeekView = MakeCheckCell (110, "1 week before", 0f, LINE_OFFSET + (CELL_HEIGHT * 9), SCREEN_WIDTH, CELL_HEIGHT);


            //LO
            line1 = AddLine (0, LINE_OFFSET, SCREEN_WIDTH, separatorColor);
            line2 = AddLine (15, LINE_OFFSET + CELL_HEIGHT, SCREEN_WIDTH, separatorColor);
            line3 = AddLine (15, LINE_OFFSET + (CELL_HEIGHT * 2), SCREEN_WIDTH, separatorColor);
            line4 = AddLine (15, LINE_OFFSET + (CELL_HEIGHT * 3), SCREEN_WIDTH, separatorColor);
            line5 = AddLine (15, LINE_OFFSET + (CELL_HEIGHT * 4), SCREEN_WIDTH, separatorColor);
            line6 = AddLine (15, LINE_OFFSET + (CELL_HEIGHT * 5), SCREEN_WIDTH, separatorColor);
            line7 = AddLine (15, LINE_OFFSET + (CELL_HEIGHT * 6), SCREEN_WIDTH, separatorColor);
            line8 = AddLine (15, LINE_OFFSET + (CELL_HEIGHT * 7), SCREEN_WIDTH, separatorColor);
            line9 = AddLine (15, LINE_OFFSET + (CELL_HEIGHT * 8), SCREEN_WIDTH, separatorColor);
            line10 = AddLine (15, LINE_OFFSET + (CELL_HEIGHT * 9), SCREEN_WIDTH, separatorColor);
            line11 = AddLine (0, LINE_OFFSET + (CELL_HEIGHT * 10), SCREEN_WIDTH, separatorColor);

            //View
            contentView.AddSubviews (new UIView[] {
                noneView,
                atTimeView,
                fiveMinView,
                fifteenMinView,
                thirtyMinView,
                oneHourView,
                twoHourView,
                oneDayView,
                twoDayView,
                oneWeekView
               
            }); 

            contentView.AddSubviews (new UIView[] {
                line1,
                line2,
                line3,
                line4,
                line5,
                line6,
                line7,
                line8,
                line9,
                line10,
                line11
            }); 


            //Content View

            var bottom = oneWeekView.Frame.Bottom + CELL_HEIGHT + (LINE_OFFSET * 2);
            contentView.Frame = new RectangleF (0, 0, SCREEN_WIDTH, bottom);
            contentView.BackgroundColor = A.Color_NachoNowBackground;

            //Scroll View
            scrollView.BackgroundColor = A.Color_NachoNowBackground;
            scrollView.ContentSize = new SizeF (SCREEN_WIDTH, (LINE_OFFSET * 2) + (CELL_HEIGHT * 10));


        }

        protected void ConfigureAlertView ()
        {
            var index = UIntToIndex (Reminder);
            contentView.ViewWithTag (101 + index).ViewWithTag (201 + index).Hidden = false;

        }

        public UIView AddLine (float offset, float yVal, float width, UIColor color)
        {
            var lineUIView = new UIView (new RectangleF (offset, yVal, width, .5f));
            lineUIView.BackgroundColor = color;
            return (lineUIView);
        }

        public UIView MakeCheckCell (int tag, string label, float X, float Y, float Width, float Height)
        {
            UIView CheckCell = new UIView (new RectangleF (X, Y, Width, Height));
            CheckCell.BackgroundColor = UIColor.White;


            UILabel textLabel = new UILabel (new RectangleF (15, 12.438f, SCREEN_WIDTH / 2, TEXT_LINE_HEIGHT));
            textLabel.Text = label;
            textLabel.Font = A.Font_AvenirNextRegular14;
            textLabel.TextColor = solidTextColor;
            CheckCell.AddSubview (textLabel);

            UIImageView cellImage = new UIImageView (new RectangleF (SCREEN_WIDTH - 30, 14.5f, 15, 15));
            cellImage.Image = Util.MakeCheckmark (A.Color_NachoBlue);
            CheckCell.AddSubview (cellImage);
            cellImage.Tag = tag + 100;
            CheckCell.Tag = tag;

            var Tap = new UITapGestureRecognizer ();
            Tap.AddTarget (() => {
                if (cellImage.Hidden) {
                    ToggleChecks ();
                    cellImage.Hidden = false;
                    SetReminderValue (CheckCell.Tag);
                } 
                NavigationController.PopViewControllerAnimated (true);
            });
            CheckCell.AddGestureRecognizer (Tap);
            cellImage.Hidden = true;


            return CheckCell;
        }

        public void ToggleChecks ()
        {
            int i = 0;
            while (i < 10) {
                contentView.ViewWithTag (101 + i).ViewWithTag (201 + i).Hidden = true;
                i++;
            }
        }

        public void SetReminderValue (int tag)
        {
            var min = minValues [tag - 101];
            Reminder = min;
        }

        public int UIntToIndex (uint min)
        {
            return minValues.IndexOf (min);
        }
    }
}
