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
        public McAttachment Attachment;

        public ImageViewFragment () : base ()
        {
            RetainInstance = true;
        }

        #region Subviews

        WebView WebView;

        void FindSubviews (View view)
        {
            WebView = view.FindViewById (Resource.Id.webview) as WebView;
        }

        void ClearSubviews ()
        {
            WebView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ImageViewFragment, container, false);

            FindSubviews (view);
            WebView.Settings.LoadWithOverviewMode = true;
            WebView.Settings.BuiltInZoomControls = true;

            LoadAttachment ();

            return view;
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        void LoadAttachment ()
        {
            var pathToImage = Attachment.GetFileDirectory ();
            var imageName = Attachment.GetFileName ();
            var baseUri = Android.Net.Uri.FromFile (new Java.IO.File (pathToImage));
            var body = String.Format ("<!DOCTYPE html><html><head><style>img{{display: inline; height: auto; max-width: 100%;}}</style></head><body><image src=\"{0}\"/></body>", imageName);
            WebView.LoadDataWithBaseURL (baseUri.ToString () + "/", body, "text/html", "utf-8", null);
        }
    }
}

