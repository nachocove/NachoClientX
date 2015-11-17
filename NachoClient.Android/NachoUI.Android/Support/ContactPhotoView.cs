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
using Android.Graphics;

using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class ContactPhotoView : LinearLayout
    {

        RoundedImageView PhotoView;
        TextView InitialsView;

        public ContactPhotoView (Context context) :
            base (context)
        {
            Initialize ();
        }

        public ContactPhotoView (Context context, IAttributeSet attrs) :
            base (context, attrs)
        {
            Initialize ();
        }

        public ContactPhotoView (Context context, IAttributeSet attrs, int defStyle) :
            base (context, attrs, defStyle)
        {
            Initialize ();
        }

        void Initialize ()
        {
            InitialsView = new TextView (Context);
            InitialsView.SetTextColor (Color.White);
            InitialsView.TextAlignment = TextAlignment.Center;
            InitialsView.Gravity = GravityFlags.Center;
            InitialsView.LayoutParameters = new LayoutParams (LayoutParams.MatchParent, LayoutParams.MatchParent);
            PhotoView = new RoundedImageView (Context);
            PhotoView.LayoutParameters = new LayoutParams (LayoutParams.MatchParent, LayoutParams.MatchParent);
            AddView (InitialsView);
            AddView (PhotoView);
        }

        public void SetContact (McContact contact)
        {
            if (contact.PortraitId == 0) {
                InitialsView.SetBackgroundResource(GetContactColor (contact));
                InitialsView.Text = NachoCore.Utils.ContactsHelper.GetInitials (contact);
                InitialsView.Visibility = ViewStates.Visible;
                PhotoView.Visibility = ViewStates.Gone;
            } else {
                var image = ContactToPortraitImage (contact);
                PhotoView.SetImageBitmap (image);
                PhotoView.Visibility = ViewStates.Visible;
                InitialsView.Visibility = ViewStates.Gone;
            }
        }

        public static Bitmap ContactToPortraitImage (McContact contact)
        {
            if (null == contact) {
                return null;
            }
            if (0 == contact.PortraitId) {
                return null;
            }
            return PortraitToImage (contact.PortraitId);
        }

        public static Bitmap PortraitToImage (int portraitId)
        {
            if (0 == portraitId) {
                return null;
            }
            var data = McPortrait.GetContentsByteArray (portraitId);
            if (null == data) {
                return null;
            }
            return BitmapFactory.DecodeByteArray (data, 0, data.Length);
        }

        public static int GetContactColor (McContact contact)
        {
            if (contact.CircleColor != 0) {
                return Bind.ColorForUser (contact.CircleColor);
            }
            return Resource.Drawable.UserColor0;
        }

    }
}

