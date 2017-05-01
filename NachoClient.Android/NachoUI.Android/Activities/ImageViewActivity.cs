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
using Android.Support.V7.Widget;
using NachoCore.Model;


namespace NachoClient.AndroidClient
{
    [Activity ()]
    public class ImageViewActivity : NcActivity
    {

        private const string EXTRA_ATTACHMENT_ID = "com.nachocove.nachomail.EXTRA_ATTACHMENT_ID";

        McAttachment Attachment;

        #region Intents

        public static Intent BuildIntent (Context context, McAttachment attachment)
        {
            var intent = new Intent (context, typeof (ImageViewActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_ATTACHMENT_ID, attachment.Id);
            return intent;
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;

        void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
        }

        void ClearSubviews ()
        {
            Toolbar = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            PopulateFromIntent ();
            SetContentView (Resource.Layout.ImageViewActivity);
            FindSubviews ();
            Toolbar.Title = "";
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
        }

        public override void OnAttachFragment (Fragment fragment)
        {
            base.OnAttachFragment (fragment);
            if (fragment is ImageViewFragment) {
                (fragment as ImageViewFragment).Attachment = Attachment;
            }
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
        }

        public void PopulateFromIntent ()
        {
        	var attachmentId = Intent.GetIntExtra (EXTRA_ATTACHMENT_ID, 0);
        	Attachment = McAttachment.QueryById<McAttachment> (attachmentId);
        }

        #endregion

        #region Options Menu

        public override bool OnCreateOptionsMenu (IMenu menu)
        {
            MenuInflater.Inflate (Resource.Menu.image_view, menu);
            return base.OnCreateOptionsMenu (menu);
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            switch (item.ItemId) {
            case Android.Resource.Id.Home:
                Finish ();
                return true;
            case Resource.Id.action_share:
                Share ();
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        void Share ()
        {
            AttachmentHelper.OpenAttachment (this, Attachment, useInternalViewer: false);
        }
    }
}

