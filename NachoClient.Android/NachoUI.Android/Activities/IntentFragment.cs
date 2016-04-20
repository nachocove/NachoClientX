
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

using NachoCore.Brain;

namespace NachoClient.AndroidClient
{

    public interface IntentFragmentDelegate {
        void IntentFragmentDidSelectIntent (NcMessageIntent.MessageIntent intent);
    }

    public class IntentFragment : DialogFragment
    {

        public IntentFragmentDelegate Delegate;
        ListView IntentListView;

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            var inflater = Activity.LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.IntentFragment, null);
            IntentListView = view.FindViewById<ListView> (Resource.Id.intent_list_view);
            IntentListView.ItemClick += OnListItemClick;
            SetupListView ();
            builder.SetView (view);
            var dialog = builder.Create ();
            dialog.Window.RequestFeature (WindowFeatures.NoTitle);
            return dialog;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            // Don't try to preserve the state across a configuration change.
            // Instead, just get rid of this dialog.
            Dismiss ();
        }

        void OnListItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            var adapter = IntentListView.Adapter as IntentDialogAdapter;
            var intent = adapter [e.Position];
            if (Delegate != null) {
                Delegate.IntentFragmentDidSelectIntent (intent);
            }
            Dialog.Dismiss ();
        }

        void SetupListView ()
        {
            var adapter = new IntentDialogAdapter (this);
            IntentListView.Adapter = adapter;
        }

    }

    public class IntentDialogAdapter : BaseAdapter<NcMessageIntent.MessageIntent> {

        List<NcMessageIntent.MessageIntent> Intents;
        Fragment Parent;

        public IntentDialogAdapter (Fragment parent)
        {
            Intents = NcMessageIntent.GetIntentList ();
            Parent = parent;
        }

        public override int Count {
            get {
                return Intents.Count;
            }
        }

        public override NcMessageIntent.MessageIntent this[int position]
        {
            get {
                return Intents [position];
            }
        }

        public override long GetItemId (int position)
        {
            return (long) Intents [position].type;
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            var view = convertView;
            if (convertView == null) {
                view = Parent.Activity.LayoutInflater.Inflate (Resource.Layout.IntentDialogItem, null);
            }
            var intent = Intents [position];
            view.FindViewById<TextView> (Resource.Id.intent_item_text).Text = intent.text;
            view.FindViewById<ImageView> (Resource.Id.intent_item_icon).Visibility = intent.dueDateAllowed ? ViewStates.Visible : ViewStates.Gone;
            return view;
        }

    }
}
