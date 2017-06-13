//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoClient.AndroidClient
{
    public static class Compat
    {

        public static void SetTextAppearanceCompat (this Android.Widget.TextView textView, int resourceId)
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M) {
                textView.SetTextAppearance (resourceId);
            } else {
#pragma warning disable CS0618 // Type or member is obsolete
                textView.SetTextAppearance (textView.Context, resourceId);
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        public static void SetOnScrollChangeCompat (this Android.Support.V7.Widget.RecyclerView recyclerView, Action scrollAction)
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M) {
                recyclerView.ScrollChange += (sender, e) => {
                    scrollAction ();
                };
            }else{
                recyclerView.AddOnScrollListener (new ScrollListener (scrollAction));
            }
        }

        public static Android.Graphics.Color ThemeColorCompat (this Android.Content.Context context, int attr)
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Lollipop){
                var typedVal = new Android.Util.TypedValue ();
                context.Theme.ResolveAttribute (attr, typedVal, true);
                return (Android.Support.V4.Content.ContextCompat.GetDrawable (context, typedVal.ResourceId) as Android.Graphics.Drawables.ColorDrawable).Color;
            }else{
                if (attr == Android.Resource.Attribute.ColorPrimary){
                    return Android.Graphics.Color.Rgb (0x0C, 0x42, 0x4B);
                }
            }
            throw new NachoCore.Utils.NcAssert.NachoDefaultCaseFailure ("ThemeColorCompat unknown attribute");
        }

        class ScrollListener : Android.Support.V7.Widget.RecyclerView.OnScrollListener
        {

            Action ScrollAction;

            public ScrollListener (Action scrollAction)
            {
                ScrollAction = scrollAction;
            }

            public override void OnScrolled (Android.Support.V7.Widget.RecyclerView recyclerView, int dx, int dy)
            {
                ScrollAction ();
            }
        }
    }
}
