
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

        public delegate void OnDeferralSelectedListener (MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate);

        OnDeferralSelectedListener mOnDeferralSelected;

        public static ChooseDeferralFragment newInstance (McEmailMessageThread messageThread)
        {
            var fragment = new ChooseDeferralFragment ();
            fragment.SetMessageThread (messageThread);
            return fragment;
        }

        protected void SetMessageThread(McEmailMessageThread messageThread)
        {
            this.messageThread = messageThread;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            Dialog.Window.RequestFeature(WindowFeatures.NoTitle);

            var view = inflater.Inflate (Resource.Layout.ChooseDeferralFragment, container, false);

            var tv = view.FindViewById<TextView> (Resource.Id.textview);
            var subject = messageThread.GetSubject ();
            if (null != subject) {
                tv.Text = Pretty.SubjectString (subject);
            } else {
                tv.Text = "";
            }

            var gridview = view.FindViewById<GridView> (Resource.Id.gridview);
            deferralAdapter = new DeferralAdapter (view.Context);
            deferralAdapter.setOnDeferralSelected (OnDeferralSelected);

            gridview.Adapter = deferralAdapter;

            return view;
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

        public DeferralAdapter (Context c)
        {
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

        // create a new ImageView for each item referenced by the Adapter
        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view;
            if (convertView == null) {
                view = inflater.Inflate (Resource.Layout.ChooseDeferralButton, null);
            } else {
                view = convertView;
            }
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
                ShowDateTimePicker ();
            } else if (null != mOnDeferralSelected) {
                mOnDeferralSelected (type, null, DateTime.MinValue);
            }
        }

        void ShowDateTimePicker ()
        {
            DateTimePicker.Show (context, DateTime.Now.AddHours (1), true, DateTime.Now, DateTime.Now.AddYears (10),
                (DateTime date) => {
                    if (DateTime.UtcNow > date) {
                        NcAlertView.ShowMessage (context, "Defer Message", "The chosen date is in the past. You must select a date in the future.");
                        return false;
                    }
                    return true;
                },
                (DateTime date) => {
                    if (null != mOnDeferralSelected) {
                        mOnDeferralSelected (MessageDeferralType.Custom, null, date);
                    }
                });
        }

        struct Data
        {
            public string l;
            public int i;
            public MessageDeferralType t;
        }

        Data[] data = new Data[] {
            new Data { l = "Later Today", i = Resource.Drawable.modal_later_today, t = MessageDeferralType.Later },
            new Data { l = "Tonight", i = Resource.Drawable.modal_tonight, t = MessageDeferralType.Tonight },
            new Data { l = "Tomorrow", i = Resource.Drawable.modal_tomorrow, t = MessageDeferralType.Tomorrow },
            new Data { l = "Weekend", i = Resource.Drawable.modal_weekend, t = MessageDeferralType.Weekend },
            new Data { l = "Next Week", i = Resource.Drawable.modal_next_week, t = MessageDeferralType.NextWeek },
            new Data { l = "Forever", i = Resource.Drawable.modal_forever, t = MessageDeferralType.Forever },
            new Data { l = "Pick Date", i = Resource.Drawable.modal_pick_date, t = MessageDeferralType.Custom },
        };
    }
}

