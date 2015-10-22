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
using Android.Widget;

namespace NachoClient.AndroidClient
{
    public static class Bind
    {

        public static void SetVisibility (ViewStates state, params View[] views)
        {
            foreach (var view in views) {
                view.Visibility = state;
            }
        }

        public static void BindMessageHeader (McEmailMessageThread thread, McEmailMessage message, View view)
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

            var paperclipView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.paperclip);
            paperclipView.Visibility = ViewStates.Invisible;

            if (null == message) {
                SetVisibility (ViewStates.Invisible, isUnreadView, userImageView, senderView, subjectView, dateView, chiliView);
                return;
            }

            SetVisibility (ViewStates.Visible, userImageView, senderView, subjectView, dateView, chiliView);

            if (!message.IsRead) {
                isUnreadView.Visibility = ViewStates.Visible;
            }

            userImageView.Text = message.cachedFromLetters;
            userImageView.SetBackgroundResource (ColorForUser (message.cachedFromColor));

            BindMessageChili (thread, message, chiliView);

            senderView.Text = Pretty.SenderString (message.From);
            senderView.Visibility = ViewStates.Visible;

            subjectView.Text = Pretty.SubjectString (message.Subject);
            subjectView.Visibility = ViewStates.Visible;

            dateView.Text = Pretty.FullDateTimeString (message.DateReceived);
            dateView.Visibility = ViewStates.Visible;

            if (message.cachedHasAttachments) {
                paperclipView.Visibility = ViewStates.Visible;
            } else {
                paperclipView.Visibility = ViewStates.Invisible;
            }
                
        }

        public static void BindMessageChili (McEmailMessage message, Android.Widget.ImageView chiliView)
        {
            int chiliImageId = (message.isHot () ? Resource.Drawable.email_hot : Resource.Drawable.email_not_hot);
            chiliView.SetImageResource (chiliImageId);
        }

        public static void BindMessageChili (McEmailMessageThread thread, McEmailMessage message, Android.Widget.ImageView chiliView)
        {
            if ((null != thread) && thread.HasMultipleMessages ()) {
                int chiliImageId = (message.isHot () ? Resource.Drawable.email_hotthread : Resource.Drawable.email_nothothread);
                chiliView.SetImageResource (chiliImageId);
            } else {
                BindMessageChili (message, chiliView);
            }
        }

        public static int BindContactCell (McContact contact, View view, string alternateEmailAddress = null)
        {
            var titleLabel = view.FindViewById<Android.Widget.TextView> (Resource.Id.contact_title);
            var subtitle1Label = view.FindViewById<Android.Widget.TextView> (Resource.Id.contact_subtitle1);
            var subtitle2Label = view.FindViewById<Android.Widget.TextView> (Resource.Id.contact_subtitle2);

            var displayTitle = contact.GetDisplayName ();
            var displayTitleColor = A.Color_NachoDarkText;

            var displaySubtitle1 = (null == alternateEmailAddress ? contact.GetPrimaryCanonicalEmailAddress () : alternateEmailAddress);
            var displaySubtitle1Color = A.Color_NachoDarkText;

            var displaySubtitle2 = contact.GetPrimaryPhoneNumber ();
            var displaySubtitle2Color = A.Color_NachoDarkText;

            int viewType = 3;

            if (String.IsNullOrEmpty (displayTitle) && !String.IsNullOrEmpty (displaySubtitle1)) {
                displayTitle = displaySubtitle1;
                displaySubtitle1 = "No name for this contact";
                displaySubtitle1Color = A.Color_NachoTextGray;
            }

            if (String.IsNullOrEmpty (displayTitle)) {
                displayTitle = "No name for this contact";
                displayTitleColor = A.Color_NachoLightText;
            }

            if (String.IsNullOrEmpty (displaySubtitle1)) {
                displaySubtitle1 = "No email address for this contact";
                displaySubtitle1Color = A.Color_NachoLightText;
                viewType &= ~1;
            }

            if (String.IsNullOrEmpty (displaySubtitle2)) {
                displaySubtitle2 = "No phone number for this contact";
                displaySubtitle2Color = A.Color_NachoLightText;
                viewType &= ~2;
            }

            titleLabel.Text = displayTitle;
            titleLabel.SetTextColor (displayTitleColor);

            subtitle1Label.Text = displaySubtitle1;
            subtitle1Label.SetTextColor (displaySubtitle1Color);

            subtitle2Label.Text = displaySubtitle2;
            subtitle2Label.SetTextColor (displaySubtitle2Color);

            var userInitials = view.FindViewById<Android.Widget.TextView> (Resource.Id.user_initials);
            userInitials.Text = NachoCore.Utils.ContactsHelper.GetInitials (contact);
            userInitials.SetBackgroundResource (Bind.ColorForUser (contact.CircleColor));

            return viewType;
        }

        public static void BindEventCell (McEvent ev, View view)
        {
            var colorView = view.FindViewById <View> (Resource.Id.calendar_color);
            var titleView = view.FindViewById <Android.Widget.TextView> (Resource.Id.event_title);
            var durationView = view.FindViewById<Android.Widget.TextView> (Resource.Id.event_duration);
            var locationView = view.FindViewById<Android.Widget.TextView> (Resource.Id.event_location);
            var locationImageView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.event_location_image);

            var detailView = new NcEventDetail (ev);

            int colorIndex = 0;
            var folder = McFolder.QueryByFolderEntryId<McCalendar> (detailView.Account.Id, detailView.SpecificItem.Id).FirstOrDefault ();
            if (null != folder) {
                colorIndex = folder.DisplayColor;
            }
            colorView.SetBackgroundResource (Bind.ColorForUser (colorIndex));

            titleView.Text = Pretty.SubjectString (detailView.SpecificItem.Subject);

            var startAndDuration = "";
            if (detailView.SpecificItem.AllDayEvent) {
                startAndDuration = "ALL DAY";
            } else {
                var start = Pretty.ShortTimeString (detailView.SpecificItem.StartTime);
                if (detailView.SpecificItem.EndTime > detailView.SpecificItem.StartTime) {
                    var duration = Pretty.CompactDuration (detailView.SpecificItem.StartTime, detailView.SpecificItem.EndTime);
                    startAndDuration = String.Join (" - ", new string[] { start, duration });
                } else {
                    startAndDuration = start;
                }
            }
            durationView.Text = startAndDuration;

            var location = detailView.SpecificItem.Location;
            if (String.IsNullOrEmpty (location)) {
                locationView.Text = "";
                locationImageView.Visibility = ViewStates.Invisible;
            } else {
                locationView.Text = location;
                locationImageView.Visibility = ViewStates.Visible;
            }

        }

        public static void BindEventDateCell (DateTime date, View view)
        {
            var dayOfMonthView = view.FindViewById<TextView> (Resource.Id.event_date_bignum);
            dayOfMonthView.Text = date.Day.ToString ();

            var dayOfWeekView = view.FindViewById<TextView> (Resource.Id.event_date_day_of_week);
            dayOfWeekView.Text = date.ToString ("dddd");

            var monthDayView = view.FindViewById<TextView> (Resource.Id.event_date_month_day);
            monthDayView.Text = date.ToString ("MMMM d, yyyy");

            var addButton = view.FindViewById<ImageView> (Resource.Id.event_date_add);
            addButton.SetTag (Resource.Id.event_date_add, new JavaObjectWrapper<DateTime> () { Item = date });
        }

        public static void BindHotEvent (McEvent currentEvent, View view)
        {
            var calendarColor = view.FindViewById<View> (Resource.Id.calendar_color);
            var eventIcon = view.FindViewById<View> (Resource.Id.event_location_image);
            var eventTitle = view.FindViewById<TextView> (Resource.Id.event_title);
            var eventSummary = view.FindViewById<TextView> (Resource.Id.event_summary);

            var c = currentEvent.GetCalendarItemforEvent ();
            var cRoot = CalendarHelper.GetMcCalendarRootForEvent (currentEvent.Id);

            eventTitle.Text = Pretty.SubjectString (c.GetSubject ());

            int colorIndex = 0;
            var folder = McFolder.QueryByFolderEntryId<McCalendar> (cRoot.AccountId, cRoot.Id).FirstOrDefault ();
            if (null != folder) {
                colorIndex = folder.DisplayColor;
            }
            calendarColor.SetBackgroundResource (ColorForUser (colorIndex));

            var startString = "";
            if (c.AllDayEvent) {
                startString = "ALL DAY " + Pretty.FullDateSpelledOutString (currentEvent.GetStartTimeUtc ());
            } else {
                if ((currentEvent.GetStartTimeUtc () - DateTime.UtcNow).TotalHours < 12) {
                    startString = Pretty.ShortTimeString (currentEvent.GetStartTimeUtc ());
                } else {
                    startString = Pretty.ShortDayTimeString (currentEvent.GetStartTimeUtc ());
                }
            }

            var locationString = Pretty.SubjectString (c.GetLocation ());
            var eventString = Pretty.Join (startString, locationString, " : ");

            eventSummary.Text = eventString;
            eventIcon.Visibility = (String.IsNullOrEmpty (eventString) ? ViewStates.Gone : ViewStates.Visible);
        }

        public static void BindAttachmentListView (List<McAttachment> attachments, LinearLayout view, LayoutInflater inflater, EventHandler onToggleAttachmentList, NcAttachmentView.AttachmentSelectedCallback onAttachmentSelected, NcAttachmentView.AttachmentErrorCallback onAttachmentError)
        {
            if (0 == attachments.Count) {
                view.Visibility = ViewStates.Gone;
                return;
            }
            var attachmentListCount = view.FindViewById<TextView> (Resource.Id.attachment_list_count);
            attachmentListCount.Text = attachments.Count.ToString ();

            var listview = view.FindViewById<LinearLayout> (Resource.Id.attachment_list_views);
            listview.Visibility = ViewStates.Gone;

            foreach (var a in attachments) {
                var cell = inflater.Inflate (Resource.Layout.AttachmentListViewCell, null);
                var attachmentView = new NcAttachmentView (a, cell, onAttachmentSelected, onAttachmentError);
                listview.AddView (cell);
            }

            var clickView = view.FindViewById<View> (Resource.Id.attachment_list_header);
            if (null != onToggleAttachmentList) {
                clickView.Click += onToggleAttachmentList;
            }
        }

        public static void BindAttachmentView (McAttachment attachment, View view)
        {
            var attachmentImage = view.FindViewById<ImageView> (Resource.Id.attachment_icon);
            if (Pretty.TreatLikeAPhoto (attachment.DisplayName)) {
                attachmentImage.SetImageResource (Resource.Drawable.email_att_photos);
            } else {
                attachmentImage.SetImageResource (Resource.Drawable.email_att_files);
            }
            var filenameView = view.FindViewById<TextView> (Resource.Id.attachment_name);
            filenameView.Text = System.IO.Path.GetFileNameWithoutExtension (attachment.DisplayName);
            var descriptionView = view.FindViewById<TextView> (Resource.Id.attachment_description);
            descriptionView.Text = Pretty.AttachmentDescription (attachment);

            var downloadImageView = view.FindViewById<ImageView> (Resource.Id.attachment_download);
            var spinnerView = view.FindViewById<ProgressBar> (Resource.Id.attachment_spinner);

            switch (attachment.FilePresence) {
            case McAbstrFileDesc.FilePresenceEnum.None:
                downloadImageView.Visibility = ViewStates.Visible;
                spinnerView.Visibility = ViewStates.Gone;
                break;
            case McAbstrFileDesc.FilePresenceEnum.Error:
                downloadImageView.Visibility = ViewStates.Visible;
                spinnerView.Visibility = ViewStates.Gone;
                break;
            case McAbstrFileDesc.FilePresenceEnum.Partial:
                downloadImageView.Visibility = ViewStates.Gone;
                spinnerView.Visibility = ViewStates.Visible;
                break;
            case McAbstrFileDesc.FilePresenceEnum.Complete:
                downloadImageView.Visibility = ViewStates.Gone;
                spinnerView.Visibility = ViewStates.Gone;
                break;
            default:
                NachoCore.Utils.NcAssert.CaseError ();
                break;
            }
        }

        public static void ToggleAttachmentList (View view)
        {
            var listview = view.FindViewById<View> (Resource.Id.attachment_list_views);
            var toggleView = view.FindViewById<ImageView> (Resource.Id.attachment_list_toggle);
            if (ViewStates.Gone == listview.Visibility) {
                listview.Visibility = ViewStates.Visible;
                toggleView.SetImageResource (Resource.Drawable.gen_readmore_active);
            } else {
                listview.Visibility = ViewStates.Gone;
                toggleView.SetImageResource (Resource.Drawable.gen_readmore);
            }
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

        static Random random = new Random ();

        public static int PickRandomColorForUser ()
        {
            int randomNumber = random.Next (2, userColorMap.Length);
            return randomNumber;
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

