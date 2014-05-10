using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Webkit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using MimeKit;

namespace NachoClient.AndroidClient
{
    public class ContactViewFragment : Android.Support.V4.App.Fragment
    {
        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            this.HasOptionsMenu = true;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var rootView = inflater.Inflate (Resource.Layout.ContactViewFragment, container, false);

            var contactId = this.Arguments.GetInt ("contactId", 0);

            var contact = NcModel.Instance.Db.Get<McContact> (contactId);

            var valueField = rootView.FindViewById<TextView> (Resource.Id.value);
            valueField.Text = contact.DisplayName;

            return rootView;
        }
    }
}
