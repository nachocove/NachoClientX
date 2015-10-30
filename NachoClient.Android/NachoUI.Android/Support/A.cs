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

        public static Color Color_NachoDarkText = Color.Black;
        public static Color Color_NachoLightText = Color.Rgb (0x99, 0x99, 0x99);
        public static Color Color_NachoTextGray = Color.Rgb (0x77, 0x77, 0x77);

        // Email swipe items
        public static Color Color_NachoSwipeEmailArchive = Color.Rgb (0x2b, 0xd9, 0xb2);
        public static Color Color_NachoSwipeEmailDelete = Color.Rgb (0xff, 0x3f, 0x20);
        public static Color Color_NachoSwipeEmailDefer = Color.Rgb (0xff, 0xbb, 0x33);
        public static Color Color_NachoSwipeEmailMove = Color.Rgb (0x90, 0x90, 0x90);
        public static int Id_NachoSwipeEmailArchive = Resource.Drawable.email_archive_swipe;
        public static int Id_NachoSwipeEmailDelete = Resource.Drawable.email_delete_swipe;
        public static int Id_NachoSwipeEmailDefer = Resource.Drawable.email_defer_swipe;
        public static int Id_NachoSwipeEmailMove = Resource.Drawable.email_move_swipe;

        // Contact swipe items
        public static Color Color_NachoSwipeContactCall = Color.Rgb (245, 152, 39);
        public static Color Color_NachoSwipeContactEmail = Color.Rgb (79, 100, 109);
        public static int Id_NachoSwipeContactCall = Resource.Drawable.contacts_call_swipe;
        public static int Id_NachoSwipeContactEmail = Resource.Drawable.contacts_email_swipe;

        // Calendar swipe items
        public static Color Color_NachoSwipeCalendarLate = Color.Rgb (0xff, 0x47, 0x47);
        public static Color Color_NachoSwipeCalendarForward = Color.Rgb (0x00, 0xBA, 0xD7);
        public static int Id_NachoSwipeCalendarLate = Resource.Drawable.calendar_late_swipe;
        public static int Id_NachoSwipeCalendarForward = Resource.Drawable.calendar_forward_swipe;

        // Attendee list swipe items
        public static Color Color_NachoSwipeAttendeeRemove = Color.Rgb(232, 61, 14);
        public static Color Color_NachoSwipeAttendeeResend = Color.Rgb (0x00, 0xBA, 0xD7);
        public static Color Color_NachoSwipeAttendeeRequired = Color.Rgb (0xff, 0x9b, 0x12);
        public static Color Color_NachoSwipeAteendeeOptional = Color.Rgb (0x90, 0x90, 0x90);
        public static int Id_NachoSwipeAttendeeRemove = Resource.Drawable.email_delete_swipe;
        public static int Id_NachoSwipeAttendeeResend = Resource.Drawable.files_forward_swipe;
        public static int Id_NachoSwipeAttendeeRequired = Resource.Drawable.calendar_attendee_required_swipe;
        public static int Id_NachoSwipeAttendeeOptional = Resource.Drawable.calendar_attendee_optional_swipe;
    }
}
