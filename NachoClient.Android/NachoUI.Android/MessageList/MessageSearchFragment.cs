
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Views;
using Android.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Widget;
using Android.Support.V7.Widget;
using Android.Text;
using Android.Text.Style;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;

namespace NachoClient.AndroidClient
{

    public class MessageSearchFragment : Fragment
    {

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

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Android.OS.Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.MessageSearchFragment, container, false);
            FindSubviews (view);
            // MessagesAdapter = new MessageListAdapter (this);
            // MessagesAdapter.SetMessages (Messages);
            // ListView.SetAdapter (MessagesAdapter);
            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            // StartListeningForStatusInd ();
        }

        public override void OnPause ()
        {
            // StopListeningForStatusInd ();
            base.OnPause ();
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion
    }
}