
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

using MimeKit;

namespace NachoClient.AndroidClient
{
    public class ContactViewFragment : Fragment
    {
        McContact contact;

        public static ContactViewFragment newInstance (McContact contact)
        {
            var fragment = new ContactViewFragment ();
            fragment.contact = contact;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ContactViewFragment, container, false);

            var editButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            editButton.SetImageResource (Android.Resource.Drawable.IcMenuEdit);
            editButton.Visibility = Android.Views.ViewStates.Visible;
//            editButton.Click += EditButton_Click;

            view.Click += View_Click;

            return view;
        }

        public override void OnStart ()
        {
            base.OnStart ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            BindValues (View);
        }

        public override void OnPause ()
        {
            base.OnPause ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        void BindValues (View view)
        {
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

        }

        void View_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("View_Click");
        }
    }


}
