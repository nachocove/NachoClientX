
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;

namespace NachoClient.AndroidClient
{
    public class ChooseDeferralFragment : DialogFragment
    {
        DeferralAdapter deferralAdapter;
        McEmailMessageThread messageThread;
        public NcMessageDeferral.MessageDateType type = NcMessageDeferral.MessageDateType.Defer;

        public delegate void OnDeferralSelectedListener (MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate);

        OnDeferralSelectedListener mOnDeferralSelected;

        public static ChooseDeferralFragment newInstance (McEmailMessageThread messageThread)
        {
            var fragment = new ChooseDeferralFragment ();
            fragment.SetMessageThread (messageThread);
            return fragment;
        }

        protected void SetMessageThread (McEmailMessageThread messageThread)
        {
            this.messageThread = messageThread;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            Dialog.Window.RequestFeature (WindowFeatures.NoTitle);

            var view = inflater.Inflate (Resource.Layout.ChooseDeferralFragment, container, false);

            var tv = view.FindViewById<TextView> (Resource.Id.textview);
            if (messageThread != null) {
                var subject = messageThread.GetSubject ();
                if (null != subject) {
                    tv.Text = Pretty.SubjectString (subject);
                } else {
                    tv.Text = "";
                }
            }

            var gridview = view.FindViewById<GridView> (Resource.Id.gridview);
            var messageview = view.FindViewById<TextView> (Resource.Id.message);

            DeferralAdapter.Data[] data = null;

            switch (type) {
            case NcMessageDeferral.MessageDateType.Defer:
                data = DeferralAdapter.DeferralData;
                messageview.SetText (Resource.String.defer_message_until);
                break;
            case NcMessageDeferral.MessageDateType.Deadline:
            case NcMessageDeferral.MessageDateType.Intent:
                data = DeferralAdapter.DeadlineData;
                messageview.SetText (Resource.String.set_deadline);
                break;
            default:
                NcAssert.CaseError ();
                break;
            }
            deferralAdapter = new DeferralAdapter (view.Context, data);
            deferralAdapter.setOnDeferralSelected (OnDeferralSelected);

            gridview.Adapter = deferralAdapter;

            return view;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            // There isn't a good place to store messageThread across a configuration change.
            // So don't even try.  Always dismiss the dialog so Android doesn't try to
            // recreate it.
            Dismiss ();
        }

        public void setOnDeferralSelected (OnDeferralSelectedListener onDeferralSelected)
        {
            mOnDeferralSelected = onDeferralSelected;
        }

        void OnDeferralSelected (MessageDeferralType request, McEmailMessageThread thisIsNull, DateTime selectedDate)
        {
            Dismiss ();
            if (null != mOnDeferralSelected) {
                mOnDeferralSelected (request, messageThread, selectedDate);
            }
        }
    }

    public class DeferralAdapter : BaseAdapter
    {
        Context context;
        LayoutInflater inflater;

        ChooseDeferralFragment.OnDeferralSelectedListener mOnDeferralSelected;

        public DeferralAdapter (Context c, Data[] data)
        {
            this.data = data;
            context = c;
            inflater = (LayoutInflater)context.GetSystemService (Context.LayoutInflaterService);
        }

        public void setOnDeferralSelected (ChooseDeferralFragment.OnDeferralSelectedListener onDeferralSelected)
        {
            mOnDeferralSelected = onDeferralSelected;
        }

        public override int Count {
            get { return data.Length; }
        }

        public override Java.Lang.Object GetItem (int position)
        {
            return null;
        }

        public override long GetItemId (int position)
        {
            return 0;
        }

        // Create a new view for each item referenced by the Adapter
        // because re-using views adds += ClickView too many times.
        // Should be using gridview.itemclick instead anyway.
        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            var view = inflater.Inflate (Resource.Layout.ChooseDeferralButton, null);

            var image = view.FindViewById<ImageView> (Resource.Id.image);
            image.SetImageResource (data [position].i);

            var label = view.FindViewById<TextView> (Resource.Id.label);
            label.Text = data [position].l;

            view.Click += View_Click;
            view.Tag = position;
            return view;
        }

        void View_Click (object sender, EventArgs e)
        {
            var view = (View)sender;
            var position = (int)view.Tag;

            var type = data [position].t;

            if (MessageDeferralType.Custom == type) {
                ShowDatePicker ();
            } else if (null != mOnDeferralSelected) {
                mOnDeferralSelected (type, null, DateTime.MinValue);
            }
        }

        void ShowDatePicker ()
        {
            // Today is not a valid choice when picking a date.  Have the initial selection for the date picker be tomorrow.
            DateTime tomorrow = DateTime.Now.AddDays (1).Date;
            DatePicker.Show (context, tomorrow, tomorrow, DateTime.Now.AddYears (10), (DateTime date) => {
                if (date < tomorrow) {
                    // The user selected an invalid date.  (Due to a bug in Android, DatePicker doesn't always enforce the
                    // minimum date.)  Show the date picker all over again.
                    NcAlertView.Show (context, "Pick Date", "The chosen date is in the past. You must select a date in the future.", ShowDatePicker);
                } else if (null != mOnDeferralSelected) {
                    mOnDeferralSelected (MessageDeferralType.Custom, null, date.Date);
                }
            });
        }

        public struct Data
        {
            public string l;
            public int i;
            public MessageDeferralType t;
        }

        Data[] data = null;

        public static Data[] DeferralData = new Data[] {
            new Data { l = "Later Today", i = Resource.Drawable.modal_later_today, t = MessageDeferralType.Later },
            new Data { l = "Tonight", i = Resource.Drawable.modal_tonight, t = MessageDeferralType.Tonight },
            new Data { l = "Tomorrow", i = Resource.Drawable.modal_tomorrow, t = MessageDeferralType.Tomorrow },
            new Data { l = "Weekend", i = Resource.Drawable.modal_weekend, t = MessageDeferralType.Weekend },
            new Data { l = "Next Week", i = Resource.Drawable.modal_next_week, t = MessageDeferralType.NextWeek },
            new Data { l = "Forever", i = Resource.Drawable.modal_forever, t = MessageDeferralType.Forever },
            new Data { l = "Pick Date", i = Resource.Drawable.modal_pick_date, t = MessageDeferralType.Custom },
        };

        public static Data[] DeadlineData = new Data[] {
            new Data { l = "None", i = Resource.Drawable.modal_none, t = MessageDeferralType.None },
            new Data { l = "One Hour", i = Resource.Drawable.modal_later_today, t = MessageDeferralType.OneHour },
            new Data { l = "Today", i = Resource.Drawable.modal_later_today, t = MessageDeferralType.EndOfDay },
            new Data { l = "Tomorrow", i = Resource.Drawable.modal_tomorrow, t = MessageDeferralType.Tomorrow },
            new Data { l = "Next Week", i = Resource.Drawable.modal_next_week, t = MessageDeferralType.NextWeek },
            new Data { l = "Next Month", i = Resource.Drawable.modal_nextmonth, t = MessageDeferralType.NextMonth },
            new Data { l = "Pick Date", i = Resource.Drawable.modal_pick_date, t = MessageDeferralType.Custom },
        };
    }
}

