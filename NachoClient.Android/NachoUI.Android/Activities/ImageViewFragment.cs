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
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class ImageViewFragment : Fragment
    {
        McAttachment attachment;

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

            ImageViewActivity.ExtractValues (Activity.Intent, out attachment);

            if (null == attachment) {
                Activity.Finish ();
                return view;
            }

            var buttonBar = new ButtonBar (view);
            buttonBar.SetIconButton (ButtonBar.Button.Right1, Android.Resource.Drawable.IcMenuShare, ShareButton_Click);

            var webview = view.FindViewById<WebView> (Resource.Id.webview);
            webview.Settings.LoadWithOverviewMode = true;
            webview.Settings.BuiltInZoomControls = true;

            var pathToImage = attachment.GetFileDirectory ();
            var imageName = attachment.GetFileName ();

            var baseUri = Android.Net.Uri.FromFile (new Java.IO.File (pathToImage));

            var body = String.Format ("<style>img{{display: inline; height: auto; max-width: 100%;}}</style><body><image src=\"{0}\"/></body>", imageName);
            webview.LoadDataWithBaseURL (baseUri.ToString () + "/", body, "text/html", "utf-8", null);

            return view;
        }

        private void ShareButton_Click (object sender, EventArgs e)
        {
            AttachmentHelper.OpenAttachment (Activity, attachment, useInternalViewer: false);
        }
    }
}

