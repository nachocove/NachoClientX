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
            var initials = NachoCore.Utils.ContactsHelper.GetInitials (contact);
            var color = GetContactColor (contact);
            SetPortraitId (contact.PortraitId, initials, color);
        }

        public void SetEmailAddress (int accountId, string address, string initials, int colorResource)
        {
            var portraitId = 0;
            List<McContact> contacts = McContact.QueryByEmailAddress (accountId, address);
            if (contacts != null) {
                foreach (var contact in contacts) {
                    if (contact.PortraitId != 0) {
                        portraitId = contact.PortraitId;
                        break;
                    }
                }
            }
            SetPortraitId (portraitId, initials, colorResource);
        }

        public void SetPortraitId (int portraitId, string initials, int colorResource)
        {
            if (portraitId == 0) {
                InitialsView.SetBackgroundResource(colorResource);
                InitialsView.Text = initials;
                InitialsView.Visibility = ViewStates.Visible;
                PhotoView.Visibility = ViewStates.Gone;
            } else {
                var image = PortraitToImage (portraitId);
                PhotoView.SetImageBitmap (image);
                PhotoView.Visibility = ViewStates.Visible;
                InitialsView.Visibility = ViewStates.Gone;
            }
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

        protected override void OnLayout (bool changed, int l, int t, int r, int b)
        {
            base.OnLayout (changed, l, t, r, b);
            InitialsView.SetTextSize (ComplexUnitType.Px, (float)(b - t) * 0.6f);
        }

    }
}

