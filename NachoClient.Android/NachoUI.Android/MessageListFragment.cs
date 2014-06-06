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

            var folderId = this.Arguments.GetInt ("folderId", 0);
            var accountId = this.Arguments.GetInt ("accountId", 0);
            var folder = NcModel.Instance.Db.Table<McFolder> ().Where (x => folderId == x.Id).Single ();

            messages = new NachoEmailMessages (folder);
            adapter = new MessageListAdapter (this.Activity, messages);
            listview.Adapter = adapter;

            // Watch for changes from the back end
            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                    messages.Refresh ();
                    adapter.NotifyDataSetChanged ();
                }
            };

            listview.ItemClick += (object sender, AdapterView.ItemClickEventArgs e) => {
                var fragment = new MessageViewFragment ();
                var bundle = new Bundle ();
                var thread = messages.GetEmailThread (e.Position);
                var message = thread.SingleMessageSpecialCase ();
                bundle.PutInt ("accountId", message.AccountId);
                bundle.PutInt ("messageId", message.Id);
                bundle.PutInt ("folderId", folderId);
                bundle.PutString ("segue", "MessageListToMessageView");
                fragment.Arguments = bundle;
                Activity.SupportFragmentManager.BeginTransaction ()
                    .Add (Resource.Id.content_frame, fragment)
                    .AddToBackStack (null)
                    .Commit ();
            };

            // When started with a message id, visit that message.
            var messageId = this.Arguments.GetInt ("messageId", 0);
            if (0 < messageId) {
                this.Arguments.Remove ("messageId");
                var fragment = new MessageViewFragment ();
                var bundle = new Bundle ();
                bundle.PutInt ("accountId", accountId);
                bundle.PutInt ("messageId", messageId);
                bundle.PutInt ("folderId", folderId);
                bundle.PutString ("segue", "MessageListToMessageView");
                fragment.Arguments = bundle;
                Activity.SupportFragmentManager.BeginTransaction ()
                    .Add (Resource.Id.content_frame, fragment)
                    .AddToBackStack (null)
                    .Commit ();
            }

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
            get { return messages.GetEmailThread (position).SingleMessageSpecialCase (); }
        }

        public override int Count {
            get { return messages.Count (); }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                // otherwise create a new one
                view = context.LayoutInflater.Inflate (Resource.Layout.MessageListItem, null);
            }
            var image = view.FindViewById<ImageView> (Resource.Id.status_image);
            var sender = view.FindViewById<TextView> (Resource.Id.sender);
            var subject = view.FindViewById<TextView> (Resource.Id.subject);
            var summary = view.FindViewById<TextView> (Resource.Id.summary);
            var received = view.FindViewById<TextView> (Resource.Id.received);

            var thread = messages.GetEmailThread (position);
            var message = thread.SingleMessageSpecialCase ();

            sender.Text = message.From;
            subject.Text = Pretty.SubjectString (message.Subject);
            if (null == message.Summary) {
                UpdateDbWithSummary (message);
            }
            NcAssert.True (null != message.Summary);
            summary.Text = message.Summary;
            received.Text = Pretty.FullDateString (message.DateReceived);

            if (message.IsRead) {
                image.Visibility = ViewStates.Invisible;
            } else {
                image.Visibility = ViewStates.Visible;
            }

            return view;
        }

        void UpdateDbWithSummary (McEmailMessage message)
        {
            message.Summary = MimeHelpers.ExtractSummary (message);
            message.Update ();
        }
    }
}
