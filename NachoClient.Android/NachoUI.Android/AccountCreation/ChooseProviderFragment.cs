
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
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public interface ChooseProviderDelegate
    {
        void ChooseProviderFinished (McAccount.AccountServiceEnum service);
    }

    public class ChooseProviderFragment : Fragment
    {

        private const int REQUEST_ENABLE_EXCHANGE = 1;

        ProviderAdapter providerAdapter;

        public static ChooseProviderFragment newInstance ()
        {
            var fragment = new ChooseProviderFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ChooseProviderFragment, container, false);

            var gridview = view.FindViewById<GridView> (Resource.Id.gridview);
            providerAdapter = new ProviderAdapter (view.Context);
            gridview.Adapter = providerAdapter;

            gridview.ItemClick += Gridview_ItemClick;
            return view;
        }

        void Gridview_ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            var s = providerAdapter.GetItemService (e.Position);
            var generalType = McAccount.GetAccountType (s);
            if (generalType == McAccount.AccountTypeEnum.Exchange && !NachoCore.Utils.PermissionManager.Instance.CanCreateExchange) {
                var intent = ExchangeEnableActivity.BuildIntent (Activity, s);
                StartActivityForResult (intent, REQUEST_ENABLE_EXCHANGE);
            } else {
	            var parent = (ChooseProviderDelegate)Activity;
	            parent.ChooseProviderFinished (s);
	        }
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == REQUEST_ENABLE_EXCHANGE){
                if (NachoCore.Utils.PermissionManager.Instance.CanCreateExchange){
                    var service = (McAccount.AccountServiceEnum)data.GetIntExtra (ExchangeEnableActivity.EXTRA_SERVICE, 0);
					var parent = (ChooseProviderDelegate)Activity;
					parent.ChooseProviderFinished (service);
                }
                return;
            }
            base.OnActivityResult (requestCode, resultCode, data);
        }
    }

    public class ProviderAdapter : BaseAdapter
    {
        Context context;
        LayoutInflater inflater;

        public ProviderAdapter (Context c)
        {
            context = c;
            inflater = (LayoutInflater)context.GetSystemService (Context.LayoutInflaterService);
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
            return position;
        }

        public McAccount.AccountServiceEnum GetItemService (int position)
        {
            return data [position].e;
        }

        // create a new ImageView for each item referenced by the Adapter
        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            var view = inflater.Inflate (Resource.Layout.ChooseProviderCell, null);
            var imageview = view.FindViewById<RoundedImageView> (Resource.Id.image);
            var labelview = view.FindViewById<TextView> (Resource.Id.label);

            imageview.SetImageResource (data [position].i);
            labelview.Text = NcServiceHelper.AccountServiceName (data [position].e);

            return view;
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

