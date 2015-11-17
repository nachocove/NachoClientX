
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
    public class DaysToSyncChooserFragment : DialogFragment
    {
        private const string SAVED_DAYS_TO_SYNC_KEY = "DaysToSyncChooser.saved";

        public NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode value;

        public event EventHandler<NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode> OnDaysToSyncChanged;

        DaysToSyncAdapter daysToSyncAdapter;

        ButtonBar buttonBar;

        public static DaysToSyncChooserFragment newInstance (NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode value)
        {
            var fragment = new DaysToSyncChooserFragment ();
            fragment.value = value;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            if (null != savedInstanceState) {
                int savedDaysToSync = savedInstanceState.GetInt (SAVED_DAYS_TO_SYNC_KEY, -1);
                if (-1 != savedDaysToSync) {
                    value = (NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode)savedDaysToSync;
                }
            }
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            Dialog.Window.RequestFeature (WindowFeatures.NoTitle);

            var view = inflater.Inflate (Resource.Layout.DaysToSyncChooserFragment, container, false);

            buttonBar = new ButtonBar (view);

            buttonBar.SetTitle (Resource.String.days_to_sync);

            buttonBar.SetTextButton (ButtonBar.Button.Right1, Resource.String.save, SaveButton_Click);

            var listview = view.FindViewById<ListView> (Resource.Id.listview);
            listview.ItemClick += Listview_ItemClick;

            daysToSyncAdapter = new DaysToSyncAdapter (this);
            listview.Adapter = daysToSyncAdapter;

            return view;
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            outState.PutInt (SAVED_DAYS_TO_SYNC_KEY, (int)value);
        }

        void SaveButton_Click (object sender, EventArgs e)
        {
            if (null != OnDaysToSyncChanged) {
                OnDaysToSyncChanged (sender, value);
                Dismiss ();
            }
        }

        void Listview_ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            var choice = (NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode)e.Id;
            value = choice;
            daysToSyncAdapter.NotifyDataSetInvalidated ();
        }
            
    }

    public class DaysToSyncAdapter : BaseAdapter< NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode>
    {
        DaysToSyncChooserFragment owner;

        List<NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode> choices = new List<NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode> () {
            NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode.OneMonth_5, NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode.SyncAll_0,
        };

        public DaysToSyncAdapter (DaysToSyncChooserFragment parent)
        {
            this.owner = parent;
        }

        public override int Count {
            get { return choices.Count; }
        }

        public override NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode this [int position] {  
            get { return choices [position]; }
        }

        public override long GetItemId (int position)
        {
            return (long)choices [position];
        }

        // create a new ImageView for each item referenced by the Adapter
        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view;
            if (convertView == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.DaysToSyncChooserCell, parent, false);
            } else {
                view = convertView;
            }

            var choice = choices [position];

            var image = view.FindViewById<ImageView> (Resource.Id.days_to_sync_chooser_icon);
            if (owner.value == choice) {
                image.SetImageResource (Resource.Drawable.gen_checkbox_checked);
            } else {
                image.SetImageResource (Resource.Drawable.gen_checkbox);
            }

            var label = view.FindViewById<TextView> (Resource.Id.days_to_sync_chooser_text);
            label.Text = Pretty.MaxAgeFilter (choice);

            return view;
        }


    }
}

