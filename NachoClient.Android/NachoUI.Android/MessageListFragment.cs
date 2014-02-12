using Android.OS;
using Android.Views;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V7.App;
using Android.Support.V4.Widget;
using Android.Widget;
using NachoCore.Model;
using NachoCore;
using NachoClient;
using NachoCore.Utils;
using System;
using System.Linq;
using Android.App;

namespace NachoClient.AndroidClient
{
    public class MessageListFragment : Android.Support.V4.App.Fragment
    {
        INachoEmailMessages messages;
        MessageListAdapter adapter;

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var rootView = inflater.Inflate (Resource.Layout.MessageListFragment, container, false);
            var listview = rootView.FindViewById<ListView> (Resource.Id.listview);

            // Watch for changes from the back end
            BackEnd.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                    messages.Refresh ();
                    adapter.NotifyDataSetChanged ();
                }
            };

            var folderId = this.Arguments.GetInt ("folderId", 0);
            var accountId = this.Arguments.GetInt ("accountId", 0);

            var folder = BackEnd.Instance.Db.Table<McFolder> ().Where (x => folderId == x.Id).Single();

            messages = new NachoEmailMessages (folder);
            adapter = new MessageListAdapter (this.Activity, messages);
            listview.Adapter = adapter;

            listview.ItemClick += (object sender, AdapterView.ItemClickEventArgs e) => {
                var thread = messages.GetEmailThread (e.Position);
                var message = thread.First ();
            };

            Activity.Title = "Messages";
            return rootView;
        }
    }

    public class MessageListAdapter : BaseAdapter<McEmailMessage>
    {
        Activity context;
        INachoEmailMessages messages;

        public MessageListAdapter (Activity context, INachoEmailMessages messages) : base ()
        {
            this.context = context;
            this.messages = messages;
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override McEmailMessage this [int position] {  
            get { return messages.GetEmailThread (position).First (); }
        }

        public override int Count {
            get { return messages.Count (); }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                // otherwise create a new one
                view = context.LayoutInflater.Inflate (Android.Resource.Layout.SimpleListItem1, null);
            }
            var message = messages.GetEmailThread (position);
            view.FindViewById<TextView> (Android.Resource.Id.Text1).Text = message.First ().Summary;
            return view;
        }
    }
}
