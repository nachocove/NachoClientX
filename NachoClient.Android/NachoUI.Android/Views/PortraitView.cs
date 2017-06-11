//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
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
using Android.Graphics.Drawables;

namespace NachoClient.AndroidClient
{
    public class PortraitView : LinearLayout
    {

        public RoundedImageView ImageView { get; private set; }
        public TextView FallbackView { get; private set; }
        float FontSizeRatio = 0.6f;

        public PortraitView (Context context) :
            base (context)
        {
            Initialize ();
        }

        public PortraitView (Context context, IAttributeSet attrs) :
            base (context, attrs)
        {
            Initialize ();
        }

        public PortraitView (Context context, IAttributeSet attrs, int defStyle) :
            base (context, attrs, defStyle)
        {
            Initialize ();
        }

        void Initialize ()
        {
            ImageView = new RoundedImageView (Context);
            ImageView.LayoutParameters = new LayoutParams (LayoutParams.MatchParent, LayoutParams.MatchParent);
            ImageView.SetScaleType (Android.Widget.ImageView.ScaleType.CenterCrop);
            FallbackView = new TextView (Context);
            FallbackView.LayoutParameters = new LayoutParams (LayoutParams.MatchParent, LayoutParams.MatchParent);
            FallbackView.SetTextColor (Android.Graphics.Color.White);
            FallbackView.Text = "AB";
            FallbackView.SetBackgroundResource (Util.ColorForUser (1));
            FallbackView.Gravity = GravityFlags.Center;
            FallbackView.SetIncludeFontPadding (false);
            AddView (ImageView);
            AddView (FallbackView);
        }

        public void SetPortrait (int portraitId, int color, string initials)
        {
            FallbackView.Text = initials;
            Drawable portraitDrawable = Util.PortraitToDrawable (portraitId);
            if (portraitDrawable != null) {
                ImageView.SetImageDrawable (portraitDrawable);
                ImageView.Visibility = ViewStates.Visible;
                FallbackView.Visibility = ViewStates.Gone;
            } else {
                ImageView.Visibility = ViewStates.Gone;
                FallbackView.Visibility = ViewStates.Visible;
                FallbackView.SetBackgroundResource (Util.ColorForUser (color));
            }
        }

        protected override void OnLayout (bool changed, int l, int t, int r, int b)
        {
            base.OnLayout (changed, l, t, r, b);
            FallbackView.TextSize = Height * FontSizeRatio / Resources.DisplayMetrics.Density;
        }
    }
}
