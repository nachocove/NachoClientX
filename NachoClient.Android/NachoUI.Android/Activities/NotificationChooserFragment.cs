
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
    public class NotificationChooserFragment : DialogFragment
    {
        private const string SAVED_VALUE_KEY = "NotificationChooserFragment.savedValue";

        McAccount.NotificationConfigurationEnum value;

        public event EventHandler<McAccount.NotificationConfigurationEnum> OnNotificationsChanged;

        NotificationAdapter notificationAdapter;
        ButtonBar buttonBar;

        public static NotificationChooserFragment newInstance (McAccount.NotificationConfigurationEnum value)
        {
            var fragment = new NotificationChooserFragment ();
            fragment.value = value;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            if (null != savedInstanceState) {
                int savedValue = savedInstanceState.GetInt (SAVED_VALUE_KEY, -1);
                if (-1 != savedValue) {
                    value = (McAccount.NotificationConfigurationEnum)savedValue;
                }
            }
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            Dialog.Window.RequestFeature (WindowFeatures.NoTitle);

            var view = inflater.Inflate (Resource.Layout.NotificationChooserFragment, container, false);

            buttonBar = new ButtonBar (view);

            buttonBar.SetTitle (Resource.String.notifications);

            buttonBar.SetTextButton (ButtonBar.Button.Right1, Resource.String.save, SaveButton_Click);

            var listview = view.FindViewById<ListView> (Resource.Id.listview);
            listview.ItemClick += Listview_ItemClick;

            notificationAdapter = new NotificationAdapter (this);
            listview.Adapter = notificationAdapter;

            return view;
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            outState.PutInt (SAVED_VALUE_KEY, (int)value);
        }

        void SaveButton_Click (object sender, EventArgs e)
        {
            if (null != OnNotificationsChanged) {
                OnNotificationsChanged (sender, value);
                Dismiss ();
            }
        }

        void Listview_ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            var choice = (McAccount.NotificationConfigurationEnum)e.Id;
            SetChoice (choice);
            notificationAdapter.NotifyDataSetInvalidated ();
        }

        public bool IsChoiceSet (McAccount.NotificationConfigurationEnum choice)
        {
            if (0 == choice) {
                return (0 == value);
            } else {
                return (choice == (value & choice));
            }
        }

        public void SetChoice (McAccount.NotificationConfigurationEnum choice)
        {
            if (0 == choice) {
                value = 0;
            } else {
                value = value ^ choice;
            }
        }

    }

    public class NotificationAdapter : BaseAdapter<McAccount.NotificationConfigurationEnum>
    {
        NotificationChooserFragment owner;

        List<McAccount.NotificationConfigurationEnum> choices = new List<McAccount.NotificationConfigurationEnum> () {
            0,
            McAccount.NotificationConfigurationEnum.ALLOW_HOT_2,
            McAccount.NotificationConfigurationEnum.ALLOW_VIP_4,
            McAccount.NotificationConfigurationEnum.ALLOW_INBOX_64,
        };

        public NotificationAdapter (NotificationChooserFragment parent)
        {
            this.owner = parent;
        }

        public override int Count {
            get { return choices.Count; }
        }

        public override McAccount.NotificationConfigurationEnum this [int position] {  
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
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.NotificationChooserCell, parent, false);
            } else {
                view = convertView;
            }

            var choice = choices [position];

            var image = view.FindViewById<ImageView> (Resource.Id.notification_chooser_icon);
            if (owner.IsChoiceSet (choice)) {
                image.SetImageResource (Resource.Drawable.gen_checkbox_checked);
            } else {
                image.SetImageResource (Resource.Drawable.gen_checkbox);
            }

            var label = view.FindViewById<TextView> (Resource.Id.notification_chooser_text);
            label.Text = Pretty.NotificationConfiguration (choice);

            return view;
        }


    }
}

