
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Util;
using Android.Widget;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.Design.Widget;
using Android.Graphics;

namespace NachoClient.AndroidClient
{
    //Uses a combination of a PageTransformer and swapping X & Y coordinates
    // of touch events to create the illusion of a vertically scrolling ViewPager.
    //Requires API 11+

    public class VerticalViewPager : ViewPager
    {

        public VerticalViewPager (Context context) : base (context)
        {
            init ();
        }

        public VerticalViewPager (Context context, IAttributeSet attrs) : base (context, attrs)
        {
            init ();
        }

        private void init ()
        {
            // The majority of the magic happens here
            SetPageTransformer (true, new VerticalPageTransformer ());
            // The easiest way to get rid of the overscroll drawing that happens on the left and right
            this.OverScrollMode = OverScrollMode.Never;
        }

        private class VerticalPageTransformer : Java.Lang.Object, ViewPager.IPageTransformer
        {

            public void TransformPage (View view, float position)
            {

                if (position < -1) { // [-Infinity,-1)
                    // This page is way off-screen to the left.
                    view.Alpha = 0;
                } else if (position <= 1) { // [-1,1]
                    view.Alpha = 1;

                    // Counteract the default slide transition
                    view.TranslationX = (view.Width * -position);

                    //set Y position to swipe in from top
                    float yPosition = position * view.Height;
                    view.TranslationY = yPosition;

                } else { // (1,+Infinity]
                    // This page is way off-screen to the right.
                    view.Alpha = 0;
                }
            }
        }

        /**
     * Swaps the X and Y coordinates of your touch event.
     */
        private MotionEvent swapXY (MotionEvent ev)
        {
            float width = Width;
            float height = Height;

            float newX = (ev.GetY () / height) * width;
            float newY = (ev.GetX () / width) * height;

            ev.SetLocation (newX, newY);

            return ev;
        }

        public override bool OnInterceptTouchEvent (MotionEvent ev)
        {
            var intercepted = base.OnInterceptTouchEvent(swapXY(ev));
            swapXY(ev); // return touch coordinates to original reference frame for any child views
            return intercepted;
        }

        public override bool OnTouchEvent (MotionEvent ev)
        {
            return base.OnTouchEvent(swapXY(ev));
        }

    }
}

