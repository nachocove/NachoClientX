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
    public class AccountChooserFragment : NcDialogFragment
    {

        public McAccount SelectedAccount { get; private set; }
        private AccountChooserAdapter Adapter;

        public AccountChooserFragment () : base ()
        {
            RetainInstance = true;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var accounts = McAccount.GetAllConfiguredNormalAccounts ();
            Adapter = new AccountChooserAdapter (accounts, SelectedAccount);

            var builder = new AlertDialog.Builder (this.Activity);
            builder.SetAdapter (Adapter, ItemClick);
            return builder.Create ();
        }

        private void ItemClick (object sender, Android.Content.DialogClickEventArgs e)
        {
            SelectedAccount = Adapter [e.Which];
            Adapter.NotifyDataSetChanged ();
            Dismiss ();
        }

        public void Show (FragmentManager manager, string tag, McAccount selectedAccount, Action dismissAction)
        {
            SelectedAccount = selectedAccount;
            Show (manager, tag, dismissAction);
        }

        private class AccountChooserAdapter : BaseAdapter<McAccount>
        {
            private List<McAccount> Accounts;
            private McAccount SelectedAccount;

            public AccountChooserAdapter (List<McAccount> accounts, McAccount selectedAccount)
            {
                SelectedAccount = selectedAccount;

                Accounts = new List<McAccount> ();
                foreach (var account in accounts) {
                    if (McAccount.AccountTypeEnum.Unified != account.AccountType) {
                        Accounts.Add (account);
                    }
                }
            }

            public override int Count {
                get {
                    return Accounts.Count;
                }
            }

            public override McAccount this [int index] {
                get {
                    return Accounts [index];
                }
            }

            public override long GetItemId (int position)
            {
                return Accounts [position].Id;
            }

            public override View GetView (int position, View convertView, ViewGroup parent)
            {
                View view = convertView ?? LayoutInflater.From (parent.Context).Inflate (Resource.Layout.AccountChooserCell, parent, false);
                var account = Accounts [position];

                var imageView = view.FindViewById<ImageView> (Resource.Id.account_icon);
                var nameLabel = view.FindViewById<TextView> (Resource.Id.account_name);
                var emailLabel = view.FindViewById<TextView> (Resource.Id.account_email);

                if (account.Id == SelectedAccount.Id) {
                    view.SetBackgroundColor (new Android.Graphics.Color (0x11000000));
                } else {
                    var values = view.Context.Theme.ObtainStyledAttributes (new int [] { Android.Resource.Attribute.SelectableItemBackground });
                    view.SetBackgroundResource (values.GetResourceId (0, 0));
                }

                imageView.SetImageDrawable (Util.GetAccountImage (imageView.Context, account));

                if (!String.IsNullOrEmpty (account.DisplayName)) {
                    nameLabel.Text = account.DisplayName;
                    emailLabel.Text = account.EmailAddr;
                    emailLabel.Visibility = ViewStates.Visible;
                } else {
                    nameLabel.Text = account.EmailAddr;
                    emailLabel.Visibility = ViewStates.Gone;
                }

                return view;
            }
        }
    }
}

