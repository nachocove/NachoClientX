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

namespace NachoClient.iOS
{
    public partial class NachoNowViewController : UIViewController, INachoMessageControllerDelegate
    {
        public bool wrap = true;

        public NachoNowViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            View.BackgroundColor = UIColor.LightGray;

//            tableView.WeakDataSource = new NachoNowDataSource (this);
//            tableView.Source = new NachoNowDataSource (this);

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();
            this.View.AddGestureRecognizer (this.RevealViewController ().PanGestureRecognizer);

            // Toolbar buttons
            emailNowButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("NachoNowToMessageList", new SegueHolder (NcEmailManager.Inbox ()));
            };
            calendarNowButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("NachoNowToCalendar", new SegueHolder (null));
            };
            tasksNowButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("NachoNowToMessageList", new SegueHolder (new NachoDeferredEmailMessages ()));
            };
            contactsNowButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("NachoNowToContacts", new SegueHolder (null));
            };
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            this.NavigationController.ToolbarHidden = false;

            UpdateHotList ();

            // configure carousel
            carouselView.DataSource = new CarouselDataSource (this);
            carouselView.Delegate = new CarouselDelegate (this);  
            carouselView.Type = iCarouselType.Wheel;
            carouselView.Vertical = true;
            carouselView.ContentOffset = new SizeF (0f, 60f);
            carouselView.BackgroundColor = UIColor.LightTextColor;
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }
        //        NachoNowToCalendar(null)
        //        NachoNowToCalendarItem (index path)
        //        NachoNowToContacts (null)
        //        NachoNowToMessageAction (index path)
        //        NachoNowToMessageList (inbox folder)
        //        NachoNowToMessageList(deferred folder)
        //        NachoNowToMessagePriority  (index path)
        //        NachoNowToMessageView (index path)
        //
        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "NachoNowToCalendar") {
                return; // Nothing to do
            }
            if (segue.Identifier == "NachoNowToCalendarItem") {
                var indexPath = (NSIndexPath)sender;
                McCalendar calendarItem = (McCalendar)hotList [indexPath.Row];
                CalendarItemViewController destinationController = (CalendarItemViewController)segue.DestinationViewController;
                destinationController.calendarItem = calendarItem;
                destinationController.Title = Pretty.SubjectString (calendarItem.Subject);
                return;
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
                vc.thread = (List<McEmailMessage>)hotList [indexPath.Row];
                return;
            }
            if (segue.Identifier == "NachoNowToMessagePriority") {
                var vc = (MessagePriorityViewController)segue.DestinationViewController;
                var indexPath = (NSIndexPath)sender;
                vc.thread = messageThreads.GetEmailThread (indexPath.Row);
                vc.SetOwner (this);
                return;
            }

            Log.Info ("Unhandled segue identifer %s", segue.Identifier);
            NachoAssert.CaseError ();
        }

        public void DismissMessageViewController (INachoMessageController vc)
        {
            vc.SetOwner (null);
            vc.DismissViewController (false, null);
        }

        INachoEmailMessages messageThreads;
        INachoEmailMessages taskThreads;
        INachoCalendar calendar;
        List<object> hotList;

        protected void UpdateHotList ()
        {
            messageThreads = NcEmailManager.Inbox ();
            taskThreads = new NachoDeferredEmailMessages ();
            calendar = NcCalendarManager.Instance;
            hotList = new List<object> ();

            for (int i = 0; (i < messageThreads.Count ()) && (i < 3); i++) {
                hotList.Add (messageThreads.GetEmailThread (i));
            }
            for (int i = 0; (i < taskThreads.Count ()) && (i < 3); i++) {
                hotList.Add (taskThreads.GetEmailThread (i));
            }

            int day = calendar.IndexOfDate (DateTime.UtcNow.Date);
            for (int i = 0; i < calendar.NumberOfItemsForDay (day); i++) {
                hotList.Add (calendar.GetCalendarItem (day, i));
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
                return (uint)(5 * owner.hotList.Count ());
            }

            protected void adjustIndex (ref uint index)
            {
                index = (uint)(index % owner.hotList.Count ());
            }

            public override UIView ViewForItemAtIndex (iCarousel carousel, uint index, UIView view)
            {
                adjustIndex (ref index);

                // Create new view if no view is available for recycling
                if (view == null) {
                    var v = new UIImageView (new RectangleF (0f, 0f, 300.0f, 200.0f));
                    v.ContentMode = UIViewContentMode.Center;
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

                var item = owner.hotList [(int)index];

                var messageThread = item as List<McEmailMessage>;
                if (null != messageThread) {
                    var message = messageThread.First ();
                    label.Text = message.Subject;
                }
                var calendarItem = item as McCalendar;
                if (null != calendarItem) {
                    label.Text = calendarItem.Subject;
                }

                return view;
            }

            public override uint NumberOfPlaceholdersInCarousel (iCarousel carousel)
            {
                return 20;
            }

            public override UIView PlaceholderViewAtIndex (iCarousel carousel, uint index, UIView view)
            {
                adjustIndex (ref index);

                //create new view if no view is available for recycling
                if (null == view) {
                    var v = new UIImageView (new RectangleF (0f, 0f, 300.0f, 200.0f));
                    v.ContentMode = UIViewContentMode.Center;
                    v.Layer.CornerRadius = 5;
                    v.Layer.MasksToBounds = true;
                    var l = new UILabel (v.Bounds);
                    l.BackgroundColor = UIColor.White;
                    l.TextAlignment = UITextAlignment.Center;
                    l.Font = l.Font.WithSize (20f);
                    l.Tag = 1;
                    v.AddSubview (l);
                    view = v;
                }
                var label = (UILabel)view.ViewWithTag (1);
                label.Text = "Placeholder";

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
            //            public override MonoTouch.CoreAnimation.CATransform3D ItemTransformForOffset (iCarousel carousel, float offset, MonoTouch.CoreAnimation.CATransform3D transform)
            //            {
            //                // implement 'flip3D' style carousel
            //                transform = CATransform3D.MakeRotation (((float)Math.PI) / 8.0f, 0.0f, 1.0f, 0.0f);
            //                return CATransform3D.MakeTranslation (0f, 0f, offset * carousel.ItemWidth);
            //            }
            /// <summary>
            /// Called when the Item is touched.
            /// </summary>
            public override void DidSelectItemAtIndex (iCarousel carousel, int index)
            {
                index = index % owner.hotList.Count;

                object item = owner.hotList [index];

                var indexPath = NSIndexPath.FromItemSection (index, 0);
 
                var messageThread = item as List<McEmailMessage>;
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

            public override void CarouselWillBeginDragging (iCarousel carousel)
            {
                UIView.BeginAnimations (null);
                UIView.SetAnimationDuration (0.5f);
                UIView.CommitAnimations ();
            }

            public override void CarouselDidEndDragging (iCarousel carousel, bool decelerate)
            {
                UIView.BeginAnimations (null);
                UIView.SetAnimationCurve (UIViewAnimationCurve.EaseIn);
                UIView.SetAnimationDuration (0.5f);
                UIView.CommitAnimations ();
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
