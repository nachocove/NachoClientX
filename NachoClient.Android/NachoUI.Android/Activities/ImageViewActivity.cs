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

namespace NachoClient.AndroidClient
{
    [Activity (Label = "ImageViewActivity")]            
    public class ImageViewActivity : NcActivity
    {
        private const string EXTRA_ATTACHMENT_ID = "com.nachocove.nachomail.EXTRA_ATTACHMENT_ID";


        public static Intent ImageViewIntent (Context context, McAttachment attachment)
        {
            var intent = new Intent (context, typeof(ImageViewActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_ATTACHMENT_ID, attachment.Id);
            return intent;
        }

        public static void ExtractValues (Intent intent, out McAttachment attachment)
        {
            var id = intent.GetIntExtra (EXTRA_ATTACHMENT_ID, 0);
            attachment = McAttachment.QueryById<McAttachment> (id);
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

