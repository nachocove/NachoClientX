// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.Model;
using NachoCore;
using NachoCore.Brain;

namespace NachoClient.iOS
{
    public partial class MessagePriorityViewController : BlurryViewController, INachoMessageController
    {
        public McEmailMessageThread thread;
        protected INachoMessageControllerDelegate owner;

        public MessagePriorityViewController (IntPtr handle) : base (handle)
        {
        }

        public void SetOwner(INachoMessageControllerDelegate o)
        {
            owner = o;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            DateTime earliestDelay = DateTime.MaxValue;
            foreach (var message in thread) {
                if (earliestDelay > message.FlagUtcDeferUntil) {
                    earliestDelay = message.FlagUtcDeferUntil;
                }
            }

            if (DateTime.UtcNow > earliestDelay) {
                nowButton.Hidden = true;
                currentDelayLabel.Text = ""; // Delay period has ended
            } else if (1 == thread.Count) {
                nowButton.Hidden = false;
                currentDelayLabel.Text = String.Format ("Deferred until {0}.", earliestDelay);
            } else {
                nowButton.Hidden = false;
                currentDelayLabel.Text = String.Format ("Visible after {0}.", earliestDelay);
            }

            dismissButton.TouchUpInside += (object sender, EventArgs e) => {
                DismissViewController (true, null);
            };
            nowButton.TouchUpInside += (object sender, EventArgs e) => {
                NcMessageDeferral.UndeferThread (thread);
                owner.DismissMessageViewController (this);
            };

            customDateButton.TouchUpInside += (object sender, EventArgs e) => {
                DelayRequest (MessageDeferralType.Custom);
            };
            foreverButton.TouchUpInside += (object sender, EventArgs e) => {
                DelayRequest (MessageDeferralType.Forever);
            };
            laterButton.TouchUpInside += (object sender, EventArgs e) => {
                DelayRequest (MessageDeferralType.Later);
            };
            monthEndButton.TouchUpInside += (object sender, EventArgs e) => {
                DelayRequest (MessageDeferralType.MonthEnd);
            };
            nextMonthButton.TouchUpInside += (object sender, EventArgs e) => {
                DelayRequest (MessageDeferralType.NextMonth);
            };
            nextWeekButton.TouchUpInside += (object sender, EventArgs e) => {
                DelayRequest (MessageDeferralType.NextWeek);
            };
            scheduleMeetingButton.TouchUpInside += (object sender, EventArgs e) => {
                DelayRequest (MessageDeferralType.Meeting);
            };
            tomorrowButton.TouchUpInside += (object sender, EventArgs e) => {
                DelayRequest (MessageDeferralType.Tomorrow);
            };
            tonightButton.TouchUpInside += (object sender, EventArgs e) => {
                DelayRequest (MessageDeferralType.Tonight);
            };

        }

        /// Touch anywhere else, and we'll close this view
        public override void TouchesBegan (NSSet touches, UIEvent evt)
        {
            DismissViewController (true, null);
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            var blurry = segue.DestinationViewController as BlurryViewController;
            if (null != blurry) {
                blurry.CaptureView (this.View);
            }

            if (segue.Identifier == "MessagePriorityToDatePicker") {
                var vc = (DatePickerViewController)segue.DestinationViewController;
                vc.owner = this;
            }
        }

        public void DismissDatePicker (DatePickerViewController vc, DateTime deferUntil)
        {
            if (DateTime.UtcNow > deferUntil) {
                // TODO -- Can go back in time
                return;
            } 
            NcMessageDeferral.DeferThread (thread, deferUntil);
            vc.owner = null;
            vc.DismissViewController (false, new NSAction (delegate {
                owner.DismissMessageViewController (this);
            }));
        }

        void DelayRequest (MessageDeferralType request)
        {
            DateTime now = DateTime.Now;

            switch (request) {
            case MessageDeferralType.Later:
            case MessageDeferralType.Tonight:
            case MessageDeferralType.Tomorrow:
            case MessageDeferralType.NextWeek:
            case MessageDeferralType.MonthEnd:
            case MessageDeferralType.NextMonth:
            case MessageDeferralType.Forever:
                NcMessageDeferral.DeferThread (thread, request);
                owner.DismissMessageViewController (this);
                return;
            case MessageDeferralType.Meeting:
                new UIAlertView ("Meeting Scheduler", "Calendar is not yet implemented.", null, "Bummer").Show ();
                break;
            case MessageDeferralType.Custom:
                PerformSegue ("MessagePriorityToDatePicker", this);
                break;
            case MessageDeferralType.None:
            default:
                NachoCore.NachoAssert.CaseError ();
                return;
            }
        }
    }
}
