//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoPlatform;
using NachoCore.Utils;
using NachoCore.Model;
using Android.Views;
using NachoCore;

namespace NachoClient.AndroidClient
{
    public class HotEventAdapter : Android.Widget.BaseAdapter<McEvent>
    {
        protected McEvent currentEvent;
        protected NcTimer eventEndTimer = null;

        public HotEventAdapter ()
        {
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            Configure ();
        }

        public void Configure ()
        {
            DateTime timerFireTime;
            currentEvent = CalendarHelper.CurrentOrNextEvent (out timerFireTime);

            if (null != eventEndTimer) {
                eventEndTimer.Dispose ();
                eventEndTimer = null;
            }

            // Set a timer to fire at the end of the currently displayed event, so the view can
            // be reconfigured to show the next event.
            if (null != currentEvent) {
                TimeSpan timerDuration = timerFireTime - DateTime.UtcNow;
                if (timerDuration < TimeSpan.Zero) {
                    // The time to reevaluate the current event was in the very near future, and that time was reached in between
                    // CurrentOrNextEvent() and now.  Configure the timer to fire immediately.
                    timerDuration = TimeSpan.Zero;
                }
                eventEndTimer = new NcTimer ("HotEventView", (state) => {
                    InvokeOnUIThread.Instance.Invoke (() => {
                        Configure ();
                    });
                }, null, timerDuration, TimeSpan.Zero);
            }
        }

        public override long GetItemId (int position)
        {
            return 1;
        }

        public override int Count {
            get {
                return (null == currentEvent ? 0 : 1);
            }
        }

        public override McEvent this [int position] {  
            get { return currentEvent; }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.HotEventCell, parent, false);
            }
            Bind.BindHotEvent (currentEvent, view);

            return view;
        }


        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var statusEvent = (StatusIndEventArgs)e;

            switch (statusEvent.Status.SubKind) {

            case NcResult.SubKindEnum.Info_EventSetChanged:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                Configure ();
                NotifyDataSetChanged ();
                break;

            case NcResult.SubKindEnum.Info_ExecutionContextChanged:
                // When the app goes into the background, eventEndTimer might get cancelled, but ViewWillAppear
                // won't get called when the app returns to the foreground.  That might leave the view displaying
                // an old event.  Watch for foreground events and refresh the view.
                if (NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {
                    Configure ();
                    NotifyDataSetChanged ();
                }
                break;
            }
        }

    }
}

