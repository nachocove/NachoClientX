
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
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

namespace NachoClient.AndroidClient
{
    public class SwitchAccountFragment : Fragment
    {
        RecyclerView recyclerView;
        RecyclerView.LayoutManager layoutManager;
        AccountAdapter accountAdapter;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            // Create your fragment here
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.SwitchAccountFragment, container, false);

            var activity = (NcActivity)this.Activity;
            activity.HookSwitchAccountView (view);

            var accountButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.account);
            accountButton.SetImageResource (Resource.Drawable.gen_avatar_backarrow);

            accountAdapter = new AccountAdapter ();

            recyclerView = view.FindViewById<RecyclerView> (Resource.Id.recyclerView);
            recyclerView.SetAdapter (accountAdapter);

            layoutManager = new LinearLayoutManager (this.Activity);
            recyclerView.SetLayoutManager (layoutManager);

            return view;
        }

    }

    public class AccountAdapter : RecyclerView.Adapter
    {

        class AccountHolder : RecyclerView.ViewHolder
        {
            public AccountHolder (View view) : base (view)
            {
            }
        }

        class Data
        {
            public int r;
            public string a;
            public string e;
        };

        const int HEADER_TYPE = 1;
        const int FOOTER_TYPE = 2;
        const int ROW_TYPE = 3;

        Data[] data = new Data[] {
            new Data { r = Resource.Drawable.avatar_imap, a = "imap", e = "rascal2210@europa.com" },
            new Data { r = Resource.Drawable.avatar_gmail, a = "Gmail", e = "rascal2210@gmail.com" },
            new Data { r = Resource.Drawable.avatar_googleapps, a = "Google Apps", e = "steve@nac02.com" },
            new Data { r = Resource.Drawable.avatar_hotmail, a = "Hotmal", e = "rascal2210@hotmail.com" },
            new Data { r = Resource.Drawable.avatar_msexchange, a = "Exchange", e = "steves@nachocove.com" },
            new Data { r = Resource.Drawable.avatar_yahoo, a = "Yahoo", e = "rascal2210@yahoo.com" },
        };

        public override int GetItemViewType (int position)
        {
            if (0 == position) {
                return HEADER_TYPE;
            }
            if (data.Length == position) {
                return FOOTER_TYPE;
            }
            return ROW_TYPE;
        }

        public override int ItemCount {
            get {
                return data.Length + 1; // plus 1 for footer
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            int resId = 0;

            switch (viewType) {
            case HEADER_TYPE:
                resId = Resource.Layout.account_header;
                break;
            case ROW_TYPE:
                resId = Resource.Layout.account_row;
                break;
            case FOOTER_TYPE:
                resId = Resource.Layout.account_footer;
                break;
            }
            var view = LayoutInflater.From (parent.Context).Inflate (resId, parent, false);
            return new AccountHolder (view);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            if (FOOTER_TYPE == holder.ItemViewType) {
                return;
            }
            var icon = holder.ItemView.FindViewById<Android.Widget.ImageView> (Resource.Id.account_icon);
            icon.SetImageResource (data [position].r);

            var name = holder.ItemView.FindViewById<Android.Widget.TextView> (Resource.Id.account_name);
            name.Text = data [position].a;

            var email = holder.ItemView.FindViewById<Android.Widget.TextView> (Resource.Id.account_email);
            email.Text = data [position].e;
        }

    }
}

