//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

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
using NachoCore.Model;

namespace NachoClient.AndroidClient
{

    public interface QuickResponseFragmentDelegate {
        void QuickResponseFragmentDidSelectResponse (QuickResponseFragment fragment, NcQuickResponse.QuickResponse response);
    }

    public class QuickResponseFragment : DialogFragment
    {

        public QuickResponseFragmentDelegate Delegate;
        ListView ResponseListView;
        NcQuickResponse.QRTypeEnum Type;

        public QuickResponseFragment (NcQuickResponse.QRTypeEnum type) : base()
        {
            Type = type;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            var inflater = Activity.LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.QuickResponseFragment, null);
            ResponseListView = view.FindViewById<ListView> (Resource.Id.response_list_view);
            ResponseListView.ItemClick += OnListItemClick;
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
            var adapter = ResponseListView.Adapter as QuickResponseDialogAdapter;
            var response = adapter [e.Position];
            if (Delegate != null) {
                Delegate.QuickResponseFragmentDidSelectResponse (this, response);
            }
            Dialog.Dismiss ();
        }

        void SetupListView ()
        {
            var adapter = new QuickResponseDialogAdapter (this, Type);
            ResponseListView.Adapter = adapter;
        }
    }

    public class QuickResponseDialogAdapter : BaseAdapter<NcQuickResponse.QuickResponse> {

        List <NcQuickResponse.QuickResponse> Responses;
        Fragment Parent;

        public QuickResponseDialogAdapter (Fragment parent, NcQuickResponse.QRTypeEnum type)
        {
            Responses = new NcQuickResponse (type).GetResponseList ();
            Parent = parent;
        }

        public override int Count {
            get {
                return Responses.Count;
            }
        }

        public override NcQuickResponse.QuickResponse this[int position]
        {
            get {
                return Responses [position];
            }
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            var view = convertView;
            if (convertView == null) {
                view = Parent.Activity.LayoutInflater.Inflate (Resource.Layout.QuickResponseDialogItem, null);
            }
            var response = Responses [position];
            view.FindViewById<TextView> (Resource.Id.response_item_text).Text = response.body;
            return view;
        }

    }
}

