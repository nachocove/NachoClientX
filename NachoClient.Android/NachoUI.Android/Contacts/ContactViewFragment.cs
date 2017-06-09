//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
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
using Android.Support.V7.Widget;

using NachoCore.Model;
using NachoCore.Utils;


namespace NachoClient.AndroidClient
{
    public class ContactViewFragment : Fragment, ContactViewAdapter.Listener
    {

        public McContact Contact;
        ContactViewAdapter Adapter;

        public ContactViewFragment () : base ()
        {
            RetainInstance = true;
        }

        #region Subviews

        RecyclerView ListView;

        void FindSubviews (View view)
        {
            ListView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
            ListView.SetLayoutManager (new LinearLayoutManager (view.Context));
        }

        void ClearSubviews ()
        {
            ListView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ContactViewFragment, container, false);
            FindSubviews (view);
            Adapter = new ContactViewAdapter (this);
            ListView.SetAdapter (Adapter);
            return view;
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        public void Update ()
        {
            Adapter.NotifyDataSetChanged ();
        }

    }

    public class ContactViewAdapter : GroupedListRecyclerViewAdapter
    {

        public interface Listener
        {
        }

        enum ViewType
        {
        }

        WeakReference<Listener> WeakListener;

        public ContactViewAdapter (Listener listener) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
        }

        int _GroupCount = 0;

        public override int GroupCount {
            get {
                return _GroupCount;
            }
        }

        public override int GroupItemCount (int groupPosition)
        {
            throw new NotImplementedException ();
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            throw new NotImplementedException ();
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            throw new NotImplementedException ();
        }

    }
}
