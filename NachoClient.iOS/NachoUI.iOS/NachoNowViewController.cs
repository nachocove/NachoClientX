// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using iCarouselBinding;
using SWRevealViewControllerBinding;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using MCSwipeTableViewCellBinding;
using MonoTouch.Dialog;

namespace NachoClient.iOS
{
    public partial class NachoNowViewController : UIViewController, INachoMessageControllerDelegate
    {
        List<object> hotList;
        INachoEmailMessages messageThreads;
        INachoEmailMessages taskThreads;
        INachoCalendar calendar;
        McCalendar currentEvent;
        public bool wrap = false;

        public NachoNowViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();
            this.View.AddGestureRecognizer (this.RevealViewController ().PanGestureRecognizer);


            // Multiple buttons on the left side
            using (var nachoImage = UIImage.FromBundle ("Nacho-Cove-Icon")) {
                nachoButton.Image = nachoImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal);
            }
            NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { revealButton, nachoButton };

            nachoButton.Clicked += (object sender, EventArgs e) => {
                carouselView.ScrollToItemAtIndex (0, true);
            };

            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] { composeButton, contactButton };

            contactButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("NachoNowToContacts", new SegueHolder (null));
            };

            var currentEventTouched = new UITapGestureRecognizer ();
            currentEventTouched.NumberOfTapsRequired = 1;
            currentEventTouched.AddTarget (this, new MonoTouch.ObjCRuntime.Selector ("CurrentEventTapSelector"));
            currentEventTouched.ShouldRecognizeSimultaneously = delegate {
                return true;
            };
            currentEventView.AddGestureRecognizer (currentEventTouched);

            var carouselTouched = new UITapGestureRecognizer ();
            carouselTouched.NumberOfTapsRequired = 1;
            carouselTouched.AddTarget (this, new MonoTouch.ObjCRuntime.Selector ("CarouselTapSelector"));
            carouselTouched.ShouldRecognizeSimultaneously = delegate {
                return true;
            };
            carouselView.AddGestureRecognizer (carouselTouched);

            // Toolbar buttons
            emailNowButton.Clicked += (object sender, EventArgs e) => {
                EmailHotList ();
                Dissolve ();
            };
            calendarNowButton.Clicked += (object sender, EventArgs e) => {
                CalendarHotList ();
                Dissolve ();
            };
            tasksNowButton.Clicked += (object sender, EventArgs e) => {
                TasksHotList ();
                Dissolve ();
            };

            // configure carousel
            carouselView.DataSource = new CarouselDataSource (this);
            carouselView.Delegate = new CarouselDelegate (this);  
            carouselView.Type = iCarouselType.CoverFlow2;
            carouselView.Vertical = false;
            carouselView.ContentOffset = new SizeF (0f, 0f);
            carouselView.BackgroundColor = UIColor.LightGray;
            View.BackgroundColor = UIColor.LightGray;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = false;
            }
            EmailHotList ();
            carouselView.ReloadData ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }

        void Dissolve ()
        {
            UIView.Animate (0.5, () => {
                carouselView.Alpha = 0;
            }, () => {
                carouselView.ReloadData ();
                UIView.Animate (0.5, () => {
                    carouselView.Alpha = 1;
                }, () => {
                });
            });
        }

        [MonoTouch.Foundation.Export ("CarouselTapSelector")]
        public void OnDoubleTapCarousel (UIGestureRecognizer sender)
        {
            // FIXME: What to do on double tap?
        }

        [MonoTouch.Foundation.Export ("CurrentEventTapSelector")]
        public void OnTapCurrentEvent(UIGestureRecognizer sender)
        {
            if (null != currentEvent) {
                PerformSegue ("NachoNowToCalendarItem", new SegueHolder (currentEvent));
            }
        }

        ///        NachoNowToCalendar(null)
        ///        NachoNowToCalendarItem (index path)
        ///        NachoNowToCompose (null)
        ///        NachoNowToContacts (null)
        ///        NachoNowToMessageAction (index path)
        ///        NachoNowToMessageList (inbox folder)
        ///        NachoNowToMessageList(deferred folder)
        ///        NachoNowToMessagePriority  (index path)
        ///        NachoNowToMessageView (index path)
        ///
        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "NachoNowToCalendar") {
                return; // Nothing to do
            }
            if (segue.Identifier == "NachoNowToCalendarItem") {
                var holder = sender as SegueHolder;
                if (null != holder) {
                    var c = holder.value as McCalendar;
                    if (null != c) {
                        CalendarItemViewController dvc = (CalendarItemViewController)segue.DestinationViewController;
                        dvc.calendarItem = c;
                        dvc.Title = Pretty.SubjectString (c.Subject);
                    }
                    return;
                }
                var indexPath = (NSIndexPath)sender;
                McCalendar calendarItem = (McCalendar)hotList [indexPath.Row];
                CalendarItemViewController destinationController = (CalendarItemViewController)segue.DestinationViewController;
                destinationController.calendarItem = calendarItem;
                destinationController.Title = Pretty.SubjectString (calendarItem.Subject);
                return;
            }
            if (segue.Identifier == "NachoNowToCompose") {
                return; // Nothing to do
            }
            if (segue.Identifier == "NachoNowToContacts") {
                return; // Nothing to do
            }
            if (segue.Identifier == "NachoNowToMessageAction") {
                var vc = (MessageActionViewController)segue.DestinationViewController;
                var indexPath = (NSIndexPath)sender;
                vc.thread = messageThreads.GetEmailThread (indexPath.Row);
                vc.SetOwner (this);
                return;
            }
            if (segue.Identifier == "NachoNowToMessageList") {
                var holder = (SegueHolder)sender;
                var messageList = (INachoEmailMessages)holder.value;
                var messageListViewController = (MessageListViewController)segue.DestinationViewController;
                messageListViewController.SetEmailMessages (messageList);
                return;
            }
            if (segue.Identifier == "NachoNowToMessageView") {
                var indexPath = (NSIndexPath)sender;
                var vc = (MessageViewController)segue.DestinationViewController;
                vc.thread = (McEmailMessageThread)hotList [indexPath.Row];
                return;
            }
            if (segue.Identifier == "NachoNowToMessagePriority") {
                var vc = (MessagePriorityViewController)segue.DestinationViewController;
                var indexPath = (NSIndexPath)sender;
                vc.thread = messageThreads.GetEmailThread (indexPath.Row);
                vc.SetOwner (this);
                return;
            }

            Log.Info ("Unhandled segue identifer {0}", segue.Identifier);
            NachoAssert.CaseError ();
        }

        public void DismissMessageViewController (INachoMessageController vc)
        {
            vc.SetOwner (null);
            vc.DismissViewController (false, null);
        }

        protected void UpdateHotLists ()
        {
            hotList = new List<object> ();
            messageThreads = NcEmailManager.PriorityInbox ();
            taskThreads = new NachoDeferredEmailMessages ();
            calendar = NcCalendarManager.Instance;

            var i = calendar.IndexOfDate (DateTime.UtcNow.Date);
            if ((i >= 0) && (calendar.NumberOfItemsForDay (i) > 0)) {
                currentEvent = calendar.GetCalendarItem (i, 0);
                UpdateCurrentEventView (currentEvent);
            } else {
                UpdateCurrentEventView (null);
            }

        }

        protected void UpdateCurrentEventView (McCalendar c)
        {
            UILabel startLabel = (UILabel)currentEventView.ViewWithTag (1);
            UILabel durationLabel = (UILabel)currentEventView.ViewWithTag (2);
            UIImageView calendarImage = (UIImageView)currentEventView.ViewWithTag (3);
            UILabel titleLabel = (UILabel)currentEventView.ViewWithTag (4);
            UILabel noEventLabel = (UILabel)currentEventView.ViewWithTag (5);

            string title;

            if (null == c) {
                startLabel.Text = "";
                durationLabel.Text = "";
                calendarImage.Image = NachoClient.Util.DotWithColor (UIColor.Clear);
                noEventLabel.Text = "No events in the near future.";
                titleLabel.Text = "";
                return;
            }

            if (c.AllDayEvent) {
                startLabel.Text = "ALL DAY";
                durationLabel.Text = "";
            } else {
                startLabel.Text = Pretty.ShortTimeString (c.StartTime);
                durationLabel.Text = Pretty.CompactDuration (c);
            }
            calendarImage.Image = NachoClient.Util.DotWithColor (UIColor.Green);
            title = Pretty.SubjectString (c.Subject);
            noEventLabel.Text = "";
            var titleLabelFrame = titleLabel.Frame;
            titleLabelFrame.Width = currentEventView.Frame.Width - titleLabel.Frame.Left;
            titleLabel.Frame = titleLabelFrame;
            titleLabel.Text = title;
            titleLabel.SizeToFit ();
        }

        protected UIView CalendarView (McCalendar c)
        {
            var root = new RootElement (c.Subject);
            root.UnevenRows = true;

            Section section = null;

            section = new SuperThinSection (UIColor.White);
            section.Add (new SubjectElement (c.Subject));
            section.Add (new StartTimeElementWithIconIndent (Pretty.FullDateString (c.StartTime)));
            if (c.AllDayEvent) {
                section.Add (new DurationElement (Pretty.AllDayStartToEnd (c.StartTime, c.EndTime)));
            } else {
                section.Add (new DurationElement (Pretty.EventStartToEnd (c.StartTime, c.EndTime)));
            }
            root.Add (section);
            var dvc = new DialogViewController (root);
            dvc.View.UserInteractionEnabled = false;
            return dvc.View;
        }

        protected void EmailHotList ()
        {
            UpdateHotLists ();
            for (int i = 0; (i < messageThreads.Count ()) && (i < 8); i++) {
                hotList.Add (messageThreads.GetEmailThread (i));
            }
        }

        protected void CalendarHotList ()
        {
            UpdateHotLists ();
            int day = calendar.IndexOfDate (DateTime.UtcNow.Date);
            if (day >= 0) {
                for (int i = 0; i < calendar.NumberOfItemsForDay (day); i++) {
                    hotList.Add (calendar.GetCalendarItem (day, i));
                }
            }
        }

        protected void TasksHotList ()
        {
            UpdateHotLists ();
            for (int i = 0; (i < taskThreads.Count ()) && (i < 8); i++) {
                hotList.Add (taskThreads.GetEmailThread (i));
            }
        }

        public class CarouselDataSource : iCarouselDataSource
        {
            NachoNowViewController owner;

            public CarouselDataSource (NachoNowViewController o)
            {
                owner = o;
            }

            public override uint NumberOfItemsInCarousel (iCarousel carousel)
            {
                if (null == owner.hotList) {
                    return 0;
                } else {
                    return (uint)owner.hotList.Count;
                }
            }

            public override UIView ViewForItemAtIndex (iCarousel carousel, uint index, UIView view)
            {
                // Create new view if no view is available for recycling
                if (view == null) {
                    var f = carousel.Frame;
                    var frame = new RectangleF (f.X, f.Y, f.Width - 30.0f, f.Height - 30.0f);
                    var v = new UIView (frame);
                    v.AutoresizingMask = UIViewAutoresizing.None;
                    v.ContentMode = UIViewContentMode.Center;
                    v.Layer.CornerRadius = 5;
                    v.Layer.MasksToBounds = true;
                    v.Layer.BorderColor = UIColor.DarkGray.CGColor;
                    v.Layer.BorderWidth = 1;
                    view = v;
                }

                // Start fresh
                var subviews = view.Subviews;
                foreach (var s in subviews) {
                    s.RemoveFromSuperview ();
                }

                var item = owner.hotList [(int)index];

                var messageThread = item as McEmailMessageThread;
                if (null != messageThread) {
                    var message = messageThread.SingleMessageSpecialCase ();
                    view.AddSubview (EmailView (message));
                }
                var calendarItem = item as McCalendar;
                if (null != calendarItem) {
                    view.AddSubview (CalendarView (calendarItem));
                }

                return view;
            }

            protected UIView EmailView (McEmailMessage m)
            {
                if (null == m.Summary) {
                    MimeHelpers.UpdateDbWithSummary (m);
                }

                var root = new RootElement (m.Subject);
                root.UnevenRows = true;

                Section section = null;

                section = new SuperThinSection (UIColor.White);
                section.Add (new StyledStringElementWithIndent (m.From));
                section.Add (new SubjectElement (m.Subject));
                section.Add (new StyledMultilineElementWithIndent (m.Summary));
                root.Add (section);
                var dvc = new DialogViewController (root);
                dvc.View.UserInteractionEnabled = false;
                dvc.View.BackgroundColor = UIColor.White;
                return dvc.View;
            }

            protected UIView CalendarView (McCalendar c)
            {
                var root = new RootElement (c.Subject);
                root.UnevenRows = true;

                Section section = null;

                section = new SuperThinSection (UIColor.White);
                section.Add (new SubjectElement (c.Subject));
                section.Add (new StartTimeElementWithIconIndent (Pretty.FullDateString (c.StartTime)));
                if (c.AllDayEvent) {
                    section.Add (new DurationElement (Pretty.AllDayStartToEnd (c.StartTime, c.EndTime)));
                } else {
                    section.Add (new DurationElement (Pretty.EventStartToEnd (c.StartTime, c.EndTime)));
                }
                root.Add (section);
                var dvc = new DialogViewController (root);
                dvc.View.UserInteractionEnabled = false;
                dvc.View.BackgroundColor = UIColor.White;
                return dvc.View;
            }

            public override uint NumberOfPlaceholdersInCarousel (iCarousel carousel)
            {
                if (0 == NumberOfItemsInCarousel (carousel)) {
                    return 1;
                } else {
                    return 0;
                }
            }

            public override UIView PlaceholderViewAtIndex (iCarousel carousel, uint index, UIView view)
            {
                //create new view if no view is available for recycling
                if (null == view) {
                    var f = carousel.Frame;
                    var frame = new RectangleF (f.X, f.Y, f.Width - 30.0f, f.Height - 30.0f);
                    var v = new UIView (frame);
                    v.ContentMode = UIViewContentMode.Center;
                    v.BackgroundColor = UIColor.Blue;
                    v.Layer.CornerRadius = 5;
                    v.Layer.MasksToBounds = true;
                    v.Layer.BorderColor = UIColor.DarkGray.CGColor;
                    v.Layer.BorderWidth = 1;
                    var l = new UILabel (v.Bounds);
                    l.BackgroundColor = UIColor.White;
                    l.TextAlignment = UITextAlignment.Center;
                    l.Font = l.Font.WithSize (20f);
                    l.Tag = 1;
                    v.AddSubview (l);
                    view = v;
                }
                var label = (UILabel)view.ViewWithTag (1);
                label.Text = "No hot items!";
            
                return view;
            }
        }

        public class CarouselDelegate : iCarouselDelegate
        {
            NachoNowViewController owner;

            public CarouselDelegate (NachoNowViewController o)
            {
                owner = o;
            }

            public override void DidSelectItemAtIndex (iCarousel carousel, int index)
            {
                // Ignore placeholders
                if ((index < 0) || (index >= owner.hotList.Count)) {
                    return;
                }

                object item = owner.hotList [index];

                var indexPath = NSIndexPath.FromItemSection (index, 0);
 
                var messageThread = item as McEmailMessageThread;
                if (null != messageThread) {
                    owner.PerformSegue ("NachoNowToMessageView", indexPath);
                    return;
                }

                var calendarItem = item as McCalendar;
                if (null != calendarItem) {
                    owner.PerformSegue ("NachoNowToCalendarItem", indexPath);
                    return;
                }

                NachoAssert.CaseError ();
            }

            /// <summary>
            /// Values for option.
            /// </summary>
            public override float ValueForOption (iCarousel carousel, iCarouselOption option, float value)
            {
                // customize carousel display
                switch (option) {
                case iCarouselOption.Wrap:
                    // normally you would hard-code this to true or false
                    return (owner.wrap ? 1.0f : 0.0f);
//                case iCarouselOption.Spacing:
//                    // add a bit of spacing between the item views
//                    return value * 1.05f;
                case iCarouselOption.FadeMax:
                    if (iCarouselType.Custom == carousel.Type) {
                        return 0.0f;
                    }
                    return value;
                default:
                    return value;
                }

            }
        }
    }
}
