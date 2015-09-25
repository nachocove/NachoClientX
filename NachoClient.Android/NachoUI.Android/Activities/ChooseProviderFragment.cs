
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

using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class ChooseProviderFragment : Fragment
    {
        ProviderAdapter providerAdapter;

        public static ChooseProviderFragment newInstance ()
        {
            var fragment = new ChooseProviderFragment ();
            return fragment;
        }


        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            // Create your fragment here
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.ChooseProviderFragment, container, false);
            var tv = view.FindViewById<TextView> (Resource.Id.textview);
            tv.Text = "ChooseProvider fragment";

            var gridview = view.FindViewById<GridView> (Resource.Id.gridview);
            providerAdapter = new ProviderAdapter (view.Context);
            gridview.Adapter = providerAdapter;

            gridview.ItemClick += Gridview_ItemClick;
            return view;
        }

        void Gridview_ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            var s = providerAdapter.GetItemService (e.Position);
            var parent = (LaunchActivity)Activity;
            parent.ChooseProviderFinished (s);   
        }
    }

    public class ProviderAdapter : BaseAdapter
    {
        Context context;

        public ProviderAdapter (Context c)
        {
            context = c;
        }

        public override int Count {
            get { return data.Length; }
        }

        public override Java.Lang.Object GetItem (int position)
        {
            return null;
        }

        public override long GetItemId (int position)
        {
            return 0;
        }

        public McAccount.AccountServiceEnum GetItemService (int position)
        {
            return data [position].e;
        }

        // create a new ImageView for each item referenced by the Adapter
        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            RoundedImageView imageView;

            if (convertView == null) {  // if it's not recycled, initialize some attributes
                imageView = new RoundedImageView (context);
            } else {
                return convertView;
                imageView = (RoundedImageView)convertView;
            }
            imageView.SetImageResource (data [position].i);
            return imageView;
        }

        struct Data
        {
            public int i;
            public McAccount.AccountServiceEnum e;
        }

        Data[] data = new Data[] {
            new Data { i = Resource.Drawable.avatar_msexchange, e = McAccount.AccountServiceEnum.Exchange },
            new Data { i = Resource.Drawable.avatar_gmail, e = McAccount.AccountServiceEnum.GoogleDefault },
            new Data { i = Resource.Drawable.avatar_googleapps, e = McAccount.AccountServiceEnum.GoogleExchange },
            new Data { i = Resource.Drawable.avatar_hotmail, e = McAccount.AccountServiceEnum.HotmailExchange },
            new Data { i = Resource.Drawable.avatar_icloud, e = McAccount.AccountServiceEnum.iCloud },
            new Data { i = Resource.Drawable.avatar_imap, e = McAccount.AccountServiceEnum.IMAP_SMTP },
            new Data { i = Resource.Drawable.avatar_office365, e = McAccount.AccountServiceEnum.Office365Exchange },
            new Data { i = Resource.Drawable.avatar_outlook, e = McAccount.AccountServiceEnum.OutlookExchange },
            new Data { i = Resource.Drawable.avatar_yahoo, e = McAccount.AccountServiceEnum.Yahoo },
        };
    }
}

