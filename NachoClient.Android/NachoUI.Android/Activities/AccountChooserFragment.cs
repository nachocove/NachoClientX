//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.App;
using NachoCore.Model;
using System.Collections.Generic;
using NachoCore;
using Android.Widget;
using Android.Views;
using Android.OS;
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    public class AccountChooserFragment : DialogFragment
    {
        public delegate void AccountSelectedDelegate (McAccount selectedAccount);

        private McAccount initialSelection;
        private AccountSelectedDelegate selectedCallback;
        private AlertDialog dialog;
        private AccountChooserAdapter adapter;

        public void SetValues (McAccount initialSelection, AccountSelectedDelegate selectedCallback)
        {
            this.initialSelection = initialSelection;
            this.selectedCallback = selectedCallback;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var accounts = McAccount.GetAllConfiguredNormalAccounts ();
            adapter = new AccountChooserAdapter (accounts, initialSelection);

            var view = new ListView (this.Activity);
            view.Id = Resource.Id.listView;
            view.Adapter = adapter;

            dialog = new AlertDialog.Builder (this.Activity).Create ();
            dialog.SetView (view);
            return dialog;
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

        private void ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            dialog.Dismiss ();
            if (null != selectedCallback) {
                selectedCallback (adapter [e.Position]);
            }
        }

        private class AccountChooserAdapter : BaseAdapter<McAccount>
        {
            private List<McAccount> listItems;
            private McAccount selected;

            public AccountChooserAdapter (List<McAccount> accounts, McAccount initialSelection)
            {
                this.selected = initialSelection;

                listItems = new List<McAccount> ();
                foreach (var account in accounts) {
                    if (McAccount.AccountTypeEnum.Unified != account.AccountType) {
                        listItems.Add (account);
                    }
                }
            }

            public override int Count {
                get {
                    return listItems.Count;
                }
            }

            public override McAccount this [int index] {
                get {
                    return listItems [index];
                }
            }

            public override long GetItemId (int position)
            {
                return listItems [position].Id;
            }

            public override View GetView (int position, View convertView, ViewGroup parent)
            {
                View cellView;
                var item = listItems [position];
                cellView = convertView ?? LayoutInflater.From (parent.Context).Inflate (Resource.Layout.AccountChooserCell, parent, false);
                var icon = cellView.FindViewById<ImageView> (Resource.Id.account_chooser_icon);
                if (item.Id == selected.Id) {
                    icon.SetImageResource (Resource.Drawable.gen_checkbox_checked);
                } else {
                    icon.SetImageResource (Resource.Drawable.gen_checkbox);
                }
                var text = cellView.FindViewById<TextView> (Resource.Id.account_chooser_text);
                text.Text = item.EmailAddr;
                return cellView;
            }
        }
    }
}

