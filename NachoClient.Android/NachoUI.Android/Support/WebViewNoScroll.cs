//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

namespace NachoClient.AndroidClient
{

    public class WebViewNoScroll : Android.Webkit.WebView
    {
        protected WebViewNoScroll (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer)
        {
        }

        //        public WebViewNoScroll (Context context, Android.Util.IAttributeSet attrs, int defStyleAttr, int defStyleRes) : base (context, attrs, defStyleAttr, defStyleRes)
        //        {
        //        }

        public WebViewNoScroll (Context context, Android.Util.IAttributeSet attrs, int defStyleAttr, bool privateBrowsing) : base (context, attrs, defStyleAttr, privateBrowsing)
        {
        }

        public WebViewNoScroll (Context context, Android.Util.IAttributeSet attrs, int defStyleAttr) : base (context, attrs, defStyleAttr)
        {
        }

        public WebViewNoScroll (Context context, Android.Util.IAttributeSet attrs) : base (context, attrs)
        {
        }

        public WebViewNoScroll (Context context) : base (context)
        {
        }

        public override bool OnTouchEvent (MotionEvent e)
        {
            return false;
        }

        public override bool OnInterceptTouchEvent (MotionEvent ev)
        {
            return false;
        }

        public override bool DispatchTouchEvent (MotionEvent e)
        {
            return false;
        }

    }
}

