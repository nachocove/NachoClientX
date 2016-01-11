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
using Android.Views;
using Android.Widget;
using NachoCore.Model;
using NachoCore;
using Java.Net;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "Nacho Mail")]
    [IntentFilter (new[] { Intent.ActionSend, Intent.ActionSendMultiple }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "*/*")]
    [IntentFilter (new[] { Intent.ActionSendto }, Categories = new[] { Intent.CategoryDefault }, DataScheme = "mailto", DataMimeType = "*/*")]
    public class MessageComposePublicListener : NcActivity
    {
        protected override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            SetContentView (Resource.Layout.WaitingFragment);
            FindViewById<TextView> (Resource.Id.textview).Text = "Loading Nacho Mail";

            System.Threading.Tasks.Task.Run (() => {
                MainApplication.OneTimeStartup ("MessageComposePublicListener");

                var accounts = McAccount.QueryByAccountCapabilities (McAccount.AccountCapabilityEnum.EmailSender).ToList ();
                if (0 == accounts.Count) {
                    RunOnUiThread (() => {
                        NcAlertView.Show (this, "No Accounts", "No email accounts are currently set up.", () => {
                            Finish ();
                        });
                    });
                } else if (1 == accounts.Count) {
                    StartMessageCompose (accounts.First ());
                    RunOnUiThread (() => {
                        Finish ();
                    });
                } else {
                    RunOnUiThread (() => {
                        var accountChooser = new AccountChooserFragment (accounts);
                        accountChooser.Show (FragmentManager, "AccountChooser");
                    });
                }
            });
        }

        public void AccountSelected (McAccount account)
        {
            System.Threading.Tasks.Task.Run (() => {
                StartMessageCompose (account);
            });
        }

        void StartMessageCompose (McAccount account)
        {
            var message = new McEmailMessage ();
            message.AccountId = account.Id;

            string initialText = "";

            if (Intent.HasExtra (Intent.ExtraEmail)) {
                message.To = string.Join (", ", Intent.GetStringArrayExtra (Intent.ExtraEmail));
            }
            if (Intent.HasExtra (Intent.ExtraCc)) {
                message.Cc = string.Join (", ", Intent.GetStringArrayExtra (Intent.ExtraCc));
            }
            if (Intent.HasExtra (Intent.ExtraBcc)) {
                message.Bcc = string.Join (", ", Intent.GetStringArrayExtra (Intent.ExtraBcc));
            }
            if (Intent.HasExtra (Intent.ExtraSubject)) {
                message.Subject = Intent.GetStringExtra (Intent.ExtraSubject);
            }
            if (Intent.HasExtra (Intent.ExtraText)) {
                initialText = Intent.GetStringExtra (Intent.ExtraText);
            }

            var attachments = new List<McAttachment> ();
            if (Intent.HasExtra (Intent.ExtraStream)) {
                try {
                    if (Intent.ActionSendMultiple == Intent.Action) {
                        var uris = Intent.GetParcelableArrayListExtra (Intent.ExtraStream);
                        foreach (var uriObject in uris) {
                            var attachment = AttachmentHelper.UriToAttachment (this, (Android.Net.Uri)uriObject, Intent.Type);
                            if (null != attachment) {
                                attachments.Add (attachment);
                            }
                        }
                    } else {
                        var uri = (Android.Net.Uri)Intent.GetParcelableExtra (Intent.ExtraStream);
                        var attachment = AttachmentHelper.UriToAttachment (this, uri, Intent.Type);
                        if (null != attachment) {
                            attachments.Add (attachment);
                        }
                    }
                } catch (Exception e) {
                    Log.Error (Log.LOG_LIFECYCLE, "Exception while processing the STREAM extra of a Send intent: {0}", e.ToString ());
                }
            }

            Intent composeIntent;
            if (0 < attachments.Count) {
                composeIntent = MessageComposeActivity.MessageWithAttachmentsIntent (this, message, initialText, attachments);
            } else {
                composeIntent = MessageComposeActivity.InitialTextIntent (this, message, initialText);
            }

            RunOnUiThread (() => {
                StartActivity (composeIntent);
            });
        }
    }

    public class AccountChooserFragment : DialogFragment
    {
        AccountChooserAdapter adapter;
        AlertDialog dialog;
        List<McAccount> accounts;

        public AccountChooserFragment (List<McAccount> accounts)
        {
            this.accounts = accounts;
        }

        // The default constructor will be called if the device is rotated while the dialog is visible.
        public AccountChooserFragment ()
        {
            accounts = McAccount.QueryByAccountCapabilities (McAccount.AccountCapabilityEnum.EmailSender).ToList ();
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            adapter = new AccountChooserAdapter (this, accounts);

            var view = new ListView (this.Activity);
            view.Id = Resource.Id.listView;
            view.Adapter = adapter;

            dialog = new AlertDialog.Builder (this.Activity).Create ();
            dialog.SetView (view);
            return dialog;
        }

        public override void OnDestroy ()
        {
            base.OnDestroy ();
            this.Activity.Finish ();
        }

        public override void OnResume ()
        {
            base.OnResume ();
            dialog.FindViewById<ListView> (Resource.Id.listView).ItemClick += ItemClick;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            dialog.FindViewById<ListView> (Resource.Id.listView).ItemClick -= ItemClick;
        }

        void ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            ((MessageComposePublicListener)this.Activity).AccountSelected (adapter [e.Position]);
            dialog.Dismiss ();
        }

        private class AccountChooserAdapter : BaseAdapter<McAccount>
        {
            private const int HEADER_CELL_TYPE = 0;
            private const int ACCOUNT_CELL_TYPE = 1;
            private const int NUM_CELL_TYPES = 2;

            private List<McAccount> listItems;
            private Fragment parent;

            public AccountChooserAdapter (Fragment parent, List<McAccount> accounts)
            {
                this.parent = parent;
                listItems = accounts;
            }

            public override int Count {
                get {
                    return listItems.Count + 1;
                }
            }

            public override McAccount this[int index] {
                get {
                    if (0 == index) {
                        return null;
                    }
                    return listItems [index - 1];
                }
            }

            public override bool IsEnabled (int position)
            {
                return 0 != position;
            }

            public override int ViewTypeCount {
                get {
                    return NUM_CELL_TYPES;
                }
            }

            public override int GetItemViewType (int position)
            {
                return IsEnabled (position) ? ACCOUNT_CELL_TYPE : HEADER_CELL_TYPE;
            }

            public override long GetItemId (int position)
            {
                return position;
            }

            public override View GetView (int position, View convertView, ViewGroup parent)
            {
                if (IsEnabled (position)) {
                    var view = new TextView (parent.Context);
                    var account = this [position];
                    view.Text = string.Format ("{0} <{1}>", account.DisplayName, account.EmailAddr);
                    view.TextSize = dp2px (8);
                    view.SetTextColor (A.Color_NachoDarkText);
                    view.SetPadding (10, 30, 10, 30);
                    return view;
                } else {
                    // This is the header at the top of the list.
                    var view = new TextView (parent.Context);
                    view.Text = "Choose an account for sending the message:";
                    view.TextSize = dp2px (9);
                    view.Gravity = GravityFlags.Center;
                    view.TextAlignment = TextAlignment.Center;
                    view.SetTextColor (Android.Graphics.Color.White);
                    view.SetBackgroundResource (Resource.Color.NachoGreen);
                    return view;
                }
            }

            private int dp2px (int dp)
            {
                return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, parent.Resources.DisplayMetrics);
            }
        }
    }
}

