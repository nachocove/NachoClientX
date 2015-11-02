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
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Webkit;

namespace NachoClient.AndroidClient
{
    public class ImageViewFragment : Fragment
    {
        public static ImageViewFragment newInstance ()
        {
            var fragment = new ImageViewFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ImageViewFragment, container, false);

            string pathToImage;
            string imageName;
            ImageViewActivity.ExtractValues (Activity.Intent, out pathToImage, out imageName);

            var webview = view.FindViewById<WebView> (Resource.Id.webview);
            webview.Settings.LoadWithOverviewMode = true;
            webview.Settings.BuiltInZoomControls = true;

            var baseUri =  Android.Net.Uri.FromFile (new Java.IO.File(pathToImage));

            var body = String.Format ("<body><image src=\"{0}\"/></body>", imageName);
            webview.LoadDataWithBaseURL (baseUri.ToString() + "/", body, "text/html", "utf-8", null);

            return view;
        }
    }
}

