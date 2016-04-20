//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Content;

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

        // Email swipe icons
        public static int Id_NachoSwipeEmailArchive = Resource.Drawable.email_archive_swipe;
        public static int Id_NachoSwipeEmailDelete = Resource.Drawable.email_delete_swipe;
        public static int Id_NachoSwipeEmailDefer = Resource.Drawable.email_defer_swipe;
        public static int Id_NachoSwipeEmailMove = Resource.Drawable.email_move_swipe;

        // Contact swipe icons
        public static int Id_NachoSwipeContactCall = Resource.Drawable.contacts_call_swipe;
        public static int Id_NachoSwipeContactEmail = Resource.Drawable.contacts_email_swipe;

        // Calendar swipe icons
        public static int Id_NachoSwipeCalendarLate = Resource.Drawable.calendar_late_swipe;
        public static int Id_NachoSwipeCalendarForward = Resource.Drawable.calendar_forward_swipe;

        // Attendee list swipe items
        public static int Id_NachoSwipeAttendeeRemove = Resource.Drawable.email_delete_swipe;
        public static int Id_NachoSwipeAttendeeResend = Resource.Drawable.files_forward_swipe;
        public static int Id_NachoSwipeAttendeeRequired = Resource.Drawable.calendar_attendee_required_swipe;
        public static int Id_NachoSwipeAttendeeOptional = Resource.Drawable.calendar_attendee_optional_swipe;

        // File list swipe items
        public static int Id_NachoSwipeFileDelete = Resource.Drawable.email_delete_swipe;
        public static int Id_NachoSwipeFileForward = Resource.Drawable.files_forward_swipe;

        public static Drawable Drawable_NachoSwipeAttendeeOptional (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeAttendeeOptional);
        }

        public static Drawable Drawable_NachoSwipeAttendeeRemove (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeAttendeeRemove);
        }

        public static Drawable Drawable_NachoSwipeAttendeeRequired (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeAttendeeRequired);
        }

        public static Drawable Drawable_NachoSwipeAttendeeResend (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeAttendeeResend);
        }

        public static Drawable Drawable_NachoSwipeCalendarForward (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeCalendarForward);
        }

        public static Drawable Drawable_NachoSwipeCalendarLate (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeCalendarLate);
        }

        public static Drawable Drawable_NachoSwipeContactCall (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeContactCall);
        }

        public static Drawable Drawable_NachoSwipeContactEmail (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeContactEmail);
        }

        public static Drawable Drawable_NachoSwipeEmailArchive (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeEmailArchive);
        }

        public static Drawable Drawable_NachoSwipeEmailDefer (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeEmailDefer);
        }

        public static Drawable Drawable_NachoSwipeEmailDelete (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeEmailDelete);
        }

        public static Drawable Drawable_NachoSwipeEmailMove (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeEmailMove);
        }

        public static Drawable Drawable_NachoSwipeFileForward (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeFileForward);
        }

        public static Drawable Drawable_NachoSwipeFileDelete (Context context)
        {
            return context.Resources.GetDrawable (Resource.Drawable.SwipeFileDelete);
        }

    }
}
