//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using Android.Graphics.Drawables;

namespace NachoClient.AndroidClient
{
    public static class Bind
    {

        public static void SetVisibility(ViewStates state, params View[] views)
        {
            foreach (var view in views) {
                view.Visibility = state;
            }
        }

        public static void BindMessageHeader(McEmailMessageThread thread, McEmailMessage message, View view)
        {
            var isUnreadView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.message_read);
            isUnreadView.Visibility = ViewStates.Invisible;

            var userImageView = view.FindViewById<Android.Widget.TextView> (Resource.Id.user_image);
            userImageView.Visibility = ViewStates.Invisible;

            var senderView = view.FindViewById<Android.Widget.TextView> (Resource.Id.sender);
            senderView.Visibility = ViewStates.Invisible;

            var subjectView = view.FindViewById<Android.Widget.TextView> (Resource.Id.subject);
            subjectView.Visibility = ViewStates.Invisible;

            var dateView = view.FindViewById<Android.Widget.TextView> (Resource.Id.date);
            dateView.Visibility = ViewStates.Invisible;

            var chiliView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.chili);
            chiliView.Visibility = ViewStates.Invisible;

            // FIXME: Attachment icon

            if (null == message) {
                SetVisibility (ViewStates.Invisible, isUnreadView, userImageView, senderView, subjectView, dateView, chiliView);
                return;
            }

            SetVisibility (ViewStates.Visible, userImageView, senderView, subjectView, dateView, chiliView);

            if (!message.IsRead) {
                isUnreadView.Visibility = ViewStates.Visible;
            }

            userImageView.Text = message.cachedFromLetters;
            userImageView.SetBackgroundResource (ColorForUser(message.cachedFromColor));

            int chiliImageId;
            if (thread.HasMultipleMessages ()) {
                chiliImageId = (message.isHot () ? Resource.Drawable.email_not_hot: Resource.Drawable.email_nothothread);
            } else {
                chiliImageId = (message.isHot () ? Resource.Drawable.email_hot : Resource.Drawable.email_not_hot);
            }
            chiliView.SetImageResource (chiliImageId);

            senderView.Text = Pretty.SenderString (message.From);
            senderView.Visibility = ViewStates.Visible;

            subjectView.Text = Pretty.SubjectString (message.Subject);
            subjectView.Visibility = ViewStates.Visible;

            dateView.Text = Pretty.FullDateTimeString (message.DateReceived);
            dateView.Visibility = ViewStates.Visible;

            // FIXME attachment icon

        }

        public static int ColorForUser (int index)
        {
            if (0 > index) {
                NachoCore.Utils.Log.Warn (NachoCore.Utils.Log.LOG_UI, "ColorForUser not set");
                index = 1;
            }
            if (userColorMap.Length <= index) {
                NachoCore.Utils.Log.Warn (NachoCore.Utils.Log.LOG_UI, "ColorForUser out of range {0}", index);
                index = 1;
            }
            return userColorMap [index];
        }

        static int[] userColorMap = {
            Resource.Drawable.UserColor0,
            Resource.Drawable.UserColor1,
            Resource.Drawable.UserColor2,
            Resource.Drawable.UserColor3,
            Resource.Drawable.UserColor4,
            Resource.Drawable.UserColor5,
            Resource.Drawable.UserColor6,
            Resource.Drawable.UserColor7,
            Resource.Drawable.UserColor8,
            Resource.Drawable.UserColor9,
            Resource.Drawable.UserColor10,
            Resource.Drawable.UserColor11,
            Resource.Drawable.UserColor12,
            Resource.Drawable.UserColor13,
            Resource.Drawable.UserColor14,
            Resource.Drawable.UserColor15,
            Resource.Drawable.UserColor16,
            Resource.Drawable.UserColor17,
            Resource.Drawable.UserColor18,
            Resource.Drawable.UserColor19,
            Resource.Drawable.UserColor20,
            Resource.Drawable.UserColor21,
            Resource.Drawable.UserColor22,
            Resource.Drawable.UserColor23,
            Resource.Drawable.UserColor24,
            Resource.Drawable.UserColor25,
            Resource.Drawable.UserColor26,
            Resource.Drawable.UserColor27,
            Resource.Drawable.UserColor28,
            Resource.Drawable.UserColor29,
            Resource.Drawable.UserColor30,
            Resource.Drawable.UserColor31,
        };
  
    }
}

