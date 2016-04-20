
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
    public class ContactLabelChooserFragment : DialogFragment
    {
        private const string SAVED_CONTACT_VALUE_KEY = "ContactLabelValue.saved";
        private const string SAVED_CONTACT_CHOICES_KEY = "ContactLabelChoices.saved";


        public string value;
        public List<string> choices;

        public event EventHandler<string> OnContactLabelChanged;

        ContactLabelAdapter contactLabelAdapter;

        ButtonBar buttonBar;

        public static ContactLabelChooserFragment newInstance (List<string> choices, string value)
        {
            var fragment = new ContactLabelChooserFragment ();
            fragment.choices = choices;
            fragment.value = value;

            // Normalize add vs replace
            if (!fragment.choices.Contains (fragment.value)) {
                fragment.choices.Add (value);
            }

            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            if (null != savedInstanceState) {
                value = savedInstanceState.GetString (SAVED_CONTACT_VALUE_KEY);
                choices = savedInstanceState.GetStringArrayList (SAVED_CONTACT_CHOICES_KEY).ToList ();
            }
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            Dialog.Window.RequestFeature (WindowFeatures.NoTitle);

            var view = inflater.Inflate (Resource.Layout.ContactLabelChooserFragment, container, false);

            buttonBar = new ButtonBar (view);

            buttonBar.SetTitle (Resource.String.select_label);

            buttonBar.SetTextButton (ButtonBar.Button.Right1, Resource.String.save, SaveButton_Click);

            var listview = view.FindViewById<ListView> (Resource.Id.listview);
            listview.ItemClick += Listview_ItemClick;

            contactLabelAdapter = new ContactLabelAdapter (this);
            listview.Adapter = contactLabelAdapter;

            return view;
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            outState.PutString (SAVED_CONTACT_VALUE_KEY, value);
            outState.PutStringArrayList (SAVED_CONTACT_CHOICES_KEY, choices);
        }

        void SaveButton_Click (object sender, EventArgs e)
        {
            if (null != OnContactLabelChanged) {
                OnContactLabelChanged (sender, value);
                Dismiss ();
            }
        }

        void Listview_ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            value = choices [e.Position];
            contactLabelAdapter.NotifyDataSetInvalidated ();
        }
            
    }

    public class ContactLabelAdapter : BaseAdapter<string>
    {
        ContactLabelChooserFragment owner;

        public ContactLabelAdapter (ContactLabelChooserFragment parent)
        {
            this.owner = parent;
        }

        public override int Count {
            get { return owner.choices.Count; }
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override string this [int position] {  
            get { return owner.choices [position]; }
        }

        // create a new ImageView for each item referenced by the Adapter
        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view;
            if (convertView == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.ContactLabelChooserCell, parent, false);
            } else {
                view = convertView;
            }

            var choice = owner.choices [position];

            var image = view.FindViewById<ImageView> (Resource.Id.chooser_icon);
            if (owner.value == choice) {
                image.SetImageResource (Resource.Drawable.gen_checkbox_checked);
            } else {
                image.SetImageResource (Resource.Drawable.gen_checkbox);
            }

            var label = view.FindViewById<TextView> (Resource.Id.chooser_text);
            label.Text = ContactsHelper.ExchangeNameToLabel (choice);

            return view;
        }


    }
}

