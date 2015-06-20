﻿using System;
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
using Android.Graphics.Drawables;


namespace NachoClient.AndroidClient
{

    public class RoundedImageView : ImageView
    {
        public RoundedImageView (Context context) : base (context)
        {
        }

        public RoundedImageView (Context context, IAttributeSet attrs) : base (context, attrs)
        {
        }

        public RoundedImageView (Context context, IAttributeSet attrs, int defStyle) : base (context, attrs, defStyle)
        {
        }

        protected override void OnDraw (Canvas canvas)
        {
            Drawable drawable = this.Drawable;

            if (drawable == null) {
                return;
            }

            if (this.Width == 0 || this.Height == 0) {
                return; 
            }
            Bitmap b = ((BitmapDrawable)drawable).Bitmap;
            Bitmap bitmap = b.Copy (Bitmap.Config.Argb8888, true);

            int w = this.Width, h = this.Height;

            Bitmap roundBitmap = getCroppedBitmap (bitmap, w);
            canvas.DrawBitmap (roundBitmap, 0, 0, null);

        }

        public static Bitmap getCroppedBitmap (Bitmap bmp, int radius)
        {
            Bitmap sbmp;
            if (bmp.Width != radius || bmp.Height != radius) {
                sbmp = Bitmap.CreateScaledBitmap (bmp, radius, radius, false);
            } else {
                sbmp = bmp;
            }
            Bitmap output = Bitmap.CreateBitmap (sbmp.Width, sbmp.Height, Bitmap.Config.Argb8888);
            Canvas canvas = new Canvas (output);

            Paint paint = new Paint ();
            Rect rect = new Rect (0, 0, sbmp.Width, sbmp.Height);

            paint.AntiAlias = true;
            paint.FilterBitmap = true;
            paint.Dither = true;
            canvas.DrawARGB (0, 0, 0, 0);
            paint.Color = Color.ParseColor ("#BAB399");
            canvas.DrawCircle (sbmp.Width / 2 + 0.7f, sbmp.Height / 2 + 0.7f, sbmp.Width / 2 + 0.1f, paint);
            paint.SetXfermode (new PorterDuffXfermode (PorterDuff.Mode.SrcIn));
            canvas.DrawBitmap (sbmp, rect, rect, paint);

            return output;
        }
    }
}