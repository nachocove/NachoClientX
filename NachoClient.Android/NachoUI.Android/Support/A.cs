//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Graphics;
using Android.Graphics.Drawables;

namespace NachoClient.AndroidClient
{
    public class A
    {
        public A ()
        {
        }

        public static Color Color_White = Color.White;

        // Email Swipe Colors and Icons
        public static Color Color_NachoSwipeEmailArchive = Color.Rgb (0x2b, 0xd9, 0xb2);
        public static Color Color_NachoSwipeEmailDelete = Color.Rgb (0xff, 0x3f, 0x20);
        public static Color Color_NachoSwipeEmailDefer = Color.Rgb (0xff, 0xbb, 0x33);
        public static Color Color_NachoSwipeEmailMove = Color.Rgb (0x90, 0x90, 0x90);
        public static int Id_NachoSwipeEmailArchive = Resource.Drawable.email_archive_swipe;
        public static int Id_NachoSwipeEmailDelete = Resource.Drawable.email_delete_swipe;
        public static int Id_NachoSwipeEmailDefer = Resource.Drawable.email_defer_swipe;
        public static int Id_NachoSwipeEmailMove = Resource.Drawable.email_move_swipe;
    }
}

