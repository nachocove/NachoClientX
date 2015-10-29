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

namespace NachoClient.AndroidClient
{
    [Activity (Label = "ImageViewActivity")]            
    public class ImageViewActivity : NcActivity
    {
        private const string EXTRA_IMAGE_PATH = "com.nachocove.nachomail.EXTRA_IMAGE_PATH";
        private const string EXTRA_IMAGE_NAME = "com.nachocove.nachomail.EXTRA_IMAGE_NAME";


        public static Intent ImageViewIntent (Context context, string pathToImage, string imageName)
        {
            var intent = new Intent (context, typeof(ImageViewActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_IMAGE_PATH, pathToImage);
            intent.PutExtra (EXTRA_IMAGE_NAME, imageName);
            return intent;
        }

        public static void ExtractValues (Intent intent, out string imagePath, out string imageName)
        {
            imagePath = intent.GetStringExtra (EXTRA_IMAGE_PATH);
            imageName = intent.GetStringExtra (EXTRA_IMAGE_NAME);
        }

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            SetContentView (Resource.Layout.ImageViewActivity);

            if (null == FragmentManager.FindFragmentByTag ("Viewer")) {
                var imageViewFragment = ImageViewFragment.newInstance ();
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, imageViewFragment, "Viewer").Commit ();
            }
        }
    }
}

