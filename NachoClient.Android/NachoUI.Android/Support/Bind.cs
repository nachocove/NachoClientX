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
using NachoPlatform;

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

        public class MessageHeaderViewHolder
        {
            public ImageView isUnreadView;
            public ContactPhotoView userImageView;
            public TextView senderView;
            public TextView subjectView;
            public TextView dateView;
            public ImageView chiliView;
            public ImageView paperclipView;
            public View dueDateView;
            public TextView dueDateTextView;

            public MessageHeaderViewHolder (View view)
            {
                isUnreadView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.message_read);
                userImageView = view.FindViewById<ContactPhotoView> (Resource.Id.user_image);
                senderView = view.FindViewById<Android.Widget.TextView> (Resource.Id.sender);
                subjectView = view.FindViewById<Android.Widget.TextView> (Resource.Id.subject);
                dateView = view.FindViewById<Android.Widget.TextView> (Resource.Id.date);
                chiliView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.chili);
                paperclipView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.paperclip);
                dueDateView = view.FindViewById (Resource.Id.due_date_view);
                dueDateTextView = view.FindViewById<TextView> (Resource.Id.due_date);
            }
        }

        public static void BindMessageHeader (McEmailMessageThread thread, McEmailMessage message, MessageHeaderViewHolder vh, bool isDraft = false)
        {
            if (null == message) {
                SetVisibility (ViewStates.Invisible, vh.isUnreadView, vh.userImageView, vh.senderView, vh.subjectView, vh.dateView, vh.paperclipView, vh.chiliView);
                return;
            }

            SetVisibility (ViewStates.Visible, vh.senderView, vh.subjectView, vh.dateView);

            if (!message.IsRead && !isDraft) {
                vh.isUnreadView.Visibility = ViewStates.Visible;
            } else {
                vh.isUnreadView.Visibility = ViewStates.Invisible;
            }

            if (isDraft) {
                vh.userImageView.Visibility = ViewStates.Invisible;
            } else {
                vh.userImageView.Visibility = ViewStates.Visible;
                var initials = message.cachedFromLetters;
                var color = ColorForUser (message.cachedFromColor);
                vh.userImageView.SetPortraitId (message.cachedPortraitId, initials, color);
            }

            if (isDraft) {
                vh.chiliView.Visibility = ViewStates.Invisible;
            } else {
                vh.chiliView.Visibility = ViewStates.Visible;
                BindMessageChili (thread, message, vh.chiliView);
            }

            if (isDraft) {
                vh.senderView.Text = Pretty.RecipientString (message.To);
            } else {
                vh.senderView.Text = Pretty.SenderString (message.From);
            }

            var subjectString = message.Subject;
            if (isDraft && String.IsNullOrEmpty (subjectString)) {
                subjectString = Pretty.NoSubjectString ();
            }

            vh.subjectView.Text = EmailHelper.CreateSubjectWithIntent (subjectString, message.Intent, message.IntentDateType, message.IntentDate);

            vh.dateView.Text = Pretty.MediumFullDateTime (message.DateReceived);

            if (message.cachedHasAttachments) {
                vh.paperclipView.Visibility = ViewStates.Visible;
            } else {
                vh.paperclipView.Visibility = ViewStates.Invisible;
            }

            if (message.HasDueDate () || message.IsDeferred ()) {
                vh.dueDateView.Visibility = ViewStates.Visible;
                vh.dueDateTextView.Text = Pretty.ReminderText (message);
            } else {
                vh.dueDateView.Visibility = ViewStates.Gone;
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

        public static int BindContactCell (McContact contact, View view, string sectionLabel, string alternateEmailAddress)
        {
            if (null == contact) {
                var titleView = view.FindViewById<TextView> (Resource.Id.contact_title);
                titleView.Visibility = ViewStates.Visible;
                titleView.SetText (Resource.String.contact_not_available);
                view.FindViewById (Resource.Id.contact_section_header).Visibility = ViewStates.Invisible;
                view.FindViewById (Resource.Id.user_initials).Visibility = ViewStates.Invisible;
                view.FindViewById (Resource.Id.contact_subtitle1).Visibility = ViewStates.Invisible;
                view.FindViewById (Resource.Id.contact_subtitle2).Visibility = ViewStates.Invisible;
                view.FindViewById (Resource.Id.vip).Visibility = ViewStates.Invisible;
                return 0;
            }

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

            var userPhotoView = view.FindViewById<ContactPhotoView> (Resource.Id.user_initials);
            userPhotoView.SetContact (contact);

            var vipView = view.FindViewById<ImageView> (Resource.Id.vip);
            BindContactVip (contact, vipView);

            var sectionHeader = view.FindViewById<TextView> (Resource.Id.contact_section_header);
            if (null == sectionLabel) {
                sectionHeader.Visibility = ViewStates.Gone;
            } else {
                sectionHeader.Visibility = ViewStates.Visible;
                sectionHeader.Text = sectionLabel;
            }

            return viewType;
        }

        public static void BindContactVip (McContact contact, ImageView vipView)
        {
            vipView.SetImageResource (contact.IsVip ? Resource.Drawable.contacts_vip_checked : Resource.Drawable.contacts_vip);
            vipView.Tag = contact.Id;
        }

        public static void BindEventCell (McEvent ev, View view)
        {
            var colorView = view.FindViewById <View> (Resource.Id.calendar_color);
            var titleView = view.FindViewById <Android.Widget.TextView> (Resource.Id.event_title);
            var durationView = view.FindViewById<Android.Widget.TextView> (Resource.Id.event_duration);
            var locationView = view.FindViewById<Android.Widget.TextView> (Resource.Id.event_location);
            var locationImageView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.event_location_image);

            string title = null;
            string location = null;
            bool allDay = false;
            DateTime startTime;
            DateTime endTime;

            if (0 != ev.DeviceEventId) {

                int displayColor;
                if (!AndroidCalendars.GetEventDetails (ev.DeviceEventId, out title, out location, out displayColor)) {
                    BindEmptyEventCell (titleView, colorView, durationView, locationView, locationImageView);
                    return;
                }
                allDay = ev.AllDayEvent;
                startTime = ev.StartTime;
                endTime = ev.EndTime;

                colorView.Visibility = ViewStates.Visible;
                var circle = view.Resources.GetDrawable (Resource.Drawable.UserColor0).Mutate ();
                ((GradientDrawable)circle).SetColor (displayColor);
                colorView.Background = circle;

            } else {

                var eventDetail = new NcEventDetail (ev);

                if (!eventDetail.IsValid) {
                    BindEmptyEventCell (titleView, colorView, durationView, locationView, locationImageView);
                    return;
                }

                int colorIndex = 0;
                var folder = McFolder.QueryByFolderEntryId<McCalendar> (eventDetail.Account.Id, eventDetail.SpecificItem.Id).FirstOrDefault ();
                if (null != folder) {
                    colorIndex = folder.DisplayColor;
                }

                title = eventDetail.SpecificItem.GetSubject ();
                location = eventDetail.SpecificItem.GetLocation ();
                allDay = eventDetail.SpecificItem.AllDayEvent;
                startTime = eventDetail.StartTime;
                endTime = eventDetail.EndTime;

                colorView.Visibility = ViewStates.Visible;
                colorView.SetBackgroundResource (Bind.ColorForUser (colorIndex));
            }

            titleView.Text = Pretty.SubjectString (title);

            var startAndDuration = "";
            if (allDay) {
                startAndDuration = "ALL DAY";
            } else {
                var startString = Pretty.Time (startTime);
                if (endTime > startTime) {
                    var duration = Pretty.CompactDuration (startTime, endTime);
                    startAndDuration = string.Join (" - ", startString, duration);
                } else {
                    startAndDuration = startString;
                }
            }
            durationView.Text = startAndDuration;

            if (string.IsNullOrEmpty (location)) {
                locationView.Text = "";
                locationImageView.Visibility = ViewStates.Invisible;
            } else {
                locationView.Text = location;
                locationImageView.Visibility = ViewStates.Visible;
            }
        }

        private static void BindEmptyEventCell (TextView title, View color, TextView duration, TextView location, View locationImage)
        {
            title.Text = "";
            color.Visibility = ViewStates.Invisible;
            duration.Text = "This event has been deleted.";
            location.Text = "";
            locationImage.Visibility = ViewStates.Invisible;
        }

        public static void BindEventDateCell (DateTime date, View view)
        {
            var dayOfMonthView = view.FindViewById<TextView> (Resource.Id.event_date_bignum);
            dayOfMonthView.Text = date.Day.ToString ();

            var dayOfWeekView = view.FindViewById<TextView> (Resource.Id.event_date_day_of_week);
            dayOfWeekView.Text = date.ToString ("dddd");

            var monthDayView = view.FindViewById<TextView> (Resource.Id.event_date_month_day);
            monthDayView.Text = Pretty.LongMonthDayYear (date);

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

            if (null == c || null == cRoot) {
                eventTitle.Text = "This event has been deleted.";
                calendarColor.Visibility = ViewStates.Invisible;
                eventSummary.Visibility = ViewStates.Invisible;
                eventIcon.Visibility = ViewStates.Invisible;
                return;
            }

            calendarColor.Visibility = ViewStates.Visible;
            eventSummary.Visibility = ViewStates.Visible;

            eventTitle.Text = Pretty.SubjectString (c.GetSubject ());

            int colorIndex = 0;
            var folder = McFolder.QueryByFolderEntryId<McCalendar> (cRoot.AccountId, cRoot.Id).FirstOrDefault ();
            if (null != folder) {
                colorIndex = folder.DisplayColor;
            }
            calendarColor.SetBackgroundResource (ColorForUser (colorIndex));

            var startString = "";
            if (c.AllDayEvent) {
                startString = "ALL DAY " + Pretty.LongFullDate (currentEvent.GetStartTimeUtc ());
            } else {
                if ((currentEvent.GetStartTimeUtc () - DateTime.UtcNow).TotalHours < 12) {
                    startString = Pretty.Time (currentEvent.GetStartTimeUtc ());
                } else {
                    startString = Pretty.LongDayTime (currentEvent.GetStartTimeUtc ());
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
                new NcAttachmentView (a, cell, onAttachmentSelected, onAttachmentError);
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
            attachmentImage.SetImageResource (AttachmentHelper.FileIconFromExtension (attachment));
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

        public static void BindChatListCell (McChat chat, View view)
        {
            var title = view.FindViewById<TextView> (Resource.Id.title);
            var date = view.FindViewById<TextView> (Resource.Id.date);
            var preview = view.FindViewById<TextView> (Resource.Id.preview);

            title.Text = chat.CachedParticipantsLabel;
            date.Text = Pretty.TimeWithDecreasingPrecision (chat.LastMessageDate);
            if (chat.LastMessagePreview == null) {
                preview.Text = "";
            } else {
                preview.Text = chat.LastMessagePreview;
            }
        }

        public static void BindChatViewCell(McEmailMessage message, McEmailMessage previous, McEmailMessage next, View view)
        {
            var oneHour = TimeSpan.FromHours (1);
            var atTimeBlockStart = previous == null || (message.DateReceived - previous.DateReceived > oneHour);
            var atTimeBlockEnd = next == null || (next.DateReceived - message.DateReceived > oneHour);
            var atParticipantBlockStart = previous == null || previous.FromEmailAddressId != message.FromEmailAddressId;
            var atParticipantBlockEnd = next == null || next.FromEmailAddressId != message.FromEmailAddressId;
            var showName = atTimeBlockStart || atParticipantBlockStart;
            var showPortrait = atTimeBlockEnd || atParticipantBlockEnd;
            var showTimestamp = atTimeBlockStart;

            var dateView = view.FindViewById<TextView> (Resource.Id.date);
            if (showTimestamp) {
                dateView.Text = Pretty.VariableDayTime (message.DateReceived);
                dateView.Visibility = ViewStates.Visible;
            } else {
                dateView.Visibility = ViewStates.Gone;
            }

            var titleView = view.FindViewById<TextView> (Resource.Id.title);
            if (showName) {
                titleView.Text = Pretty.SenderString (message.From);
                titleView.Visibility = ViewStates.Visible;
            } else {
                titleView.Visibility = ViewStates.Gone;
            }

            var previewView = view.FindViewById<TextView> (Resource.Id.preview);
            var bundle = new NcEmailMessageBundle (message);
            if (bundle.NeedsUpdate) {
                previewView.Text = "!! " + message.BodyPreview;  
            } else {
                previewView.Text = bundle.TopText;
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

