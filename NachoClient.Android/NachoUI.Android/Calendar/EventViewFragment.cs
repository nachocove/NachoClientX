//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
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
using Android.Support.V7.Widget;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class EventViewFragment : Fragment, EventViewAdapter.Listener
    {

        // TODO: accept/reject/maybe

        public McEvent Event;

        #region Subviews

        RecyclerView ListView;
        EventViewAdapter Adapter;

        void FindSubviews (View view)
        {
            ListView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
            ListView.SetLayoutManager (new LinearLayoutManager (view.Context));
        }

        void ClearSubviews ()
        {
            ListView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.EventViewFragment, container, false);
            FindSubviews (view);
            Adapter = new EventViewAdapter (this, Event);
            ListView.SetAdapter (Adapter);
            return view;
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        #region Listener

        public void OnReminderSelected ()
        {
            ShowRemiderPicker ();
        }

        public void OnNotesSelected ()
        {
            ShowNotes ();
        }

        public void OnAttachmentSelected (McAttachment attachment)
        {
            // TODO:
        }

        #endregion

        #region View Updates

        public void Update ()
        {
            Adapter.NotifyDataSetChanged ();
        }

        #endregion

        #region Private Helpers

        void ShowRemiderPicker ()
        {
            // TODO: show reminder picker
        }

        void ShowNotes ()
        {
            // TODO: show notes editor
        }

        #endregion

    }

    class EventViewAdapter : GroupedListRecyclerViewAdapter
    {
        public interface Listener
        {
            void OnReminderSelected ();
            void OnNotesSelected ();
            void OnAttachmentSelected (McAttachment attachment);
        }

        enum ViewTypes
        {
            EventInfo,
            IconTextHeader,
            IconText,
            Attendee,
            Attachment
        }

        WeakReference<Listener> WeakListener;
        McEvent Event;
        McAbstrCalendarRoot CalendarItem;
        IList<McAttachment> Attachments;
        IList<McAttendee> Attendees;
        McBody Body;

        int _GroupCount = 0;
        int InfoGroupPosition = 0;
        int DescriptionGroupPosition = -1;
        int AttachmentsGroupPosition = -1;
        int ReminderGroupPosition = -1;
        int NotesGroupPosition = -1;
        int CalendarGroupPosition = -1;
        int AttendeesGroupPosition = -1;

        public EventViewAdapter (Listener listener, McEvent calendarEvent) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
            Event = calendarEvent;
            CalendarItem = Event.CalendarItem;
            Attachments = Event.QueryAttachments ();
            Attendees = Event.QueryAttendees ();
            Body = Event.GetBody ();
            ConfigureGroups ();
        }

        void ConfigureGroups ()
        {
            DescriptionGroupPosition = -1;
            AttachmentsGroupPosition = -1;
            ReminderGroupPosition = -1;
            NotesGroupPosition = -1;
            CalendarGroupPosition = -1;
            AttendeesGroupPosition = -1;
            _GroupCount = 1;
            if (Body != null) {
                DescriptionGroupPosition = _GroupCount++;
            }
            if (Attachments.Count > 0) {
                AttachmentsGroupPosition = _GroupCount++;
            }
            ReminderGroupPosition = _GroupCount++;
            NotesGroupPosition = _GroupCount++;
            CalendarGroupPosition = _GroupCount++;
            if (Attendees.Count > 0) {
                AttendeesGroupPosition = _GroupCount++;
            }
        }

        public override int GroupCount {
            get {
                return _GroupCount;
            }
        }

        public override int GroupItemCount (int groupPosition)
        {
            if (groupPosition == InfoGroupPosition) {
                return 1;
            }
            if (groupPosition == DescriptionGroupPosition) {
                return 1;
            }
            if (groupPosition == AttachmentsGroupPosition) {
                return Attachments.Count;
            }
            if (groupPosition == ReminderGroupPosition) {
                return 1;
            }
            if (groupPosition == NotesGroupPosition) {
                return 1;
            }
            if (groupPosition == CalendarGroupPosition) {
                return 1;
            }
            if (groupPosition == AttendeesGroupPosition) {
                return Attendees.Count;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("EventViewFragment.GroupItemCount unexpected group position: {0}", groupPosition));
        }

        public override int GetHeaderViewType (int groupPosition)
        {
            if (groupPosition == AttendeesGroupPosition) {
                return (int)ViewTypes.IconTextHeader;
            }
            if (groupPosition == AttachmentsGroupPosition) {
                return (int)ViewTypes.IconTextHeader;
            }
            return base.GetHeaderViewType (groupPosition);
        }

        public override void OnBindHeaderViewHolder (Android.Support.V7.Widget.RecyclerView.ViewHolder holder, int groupPosition)
        {
            if (groupPosition == AttendeesGroupPosition) {
                // TODO: use alternate title if we are the organizer (Resource.String.event_header_attendees_organizer)
                (holder as IconTextHeaderViewHolder).SetIconText (Resource.Drawable.event_icon_attendees, Resource.String.event_header_attendees);
                return;
            }
            if (groupPosition == AttachmentsGroupPosition) {
                (holder as IconTextHeaderViewHolder).SetIconText (Resource.Drawable.event_icon_attendees, Resource.String.event_header_attachments);
                return;
            }
            base.OnBindHeaderViewHolder (holder, groupPosition);
        }

        public override int GetItemViewType (int groupPosition, int position)
        {
            if (groupPosition == InfoGroupPosition) {
                if (position == 0) {
                    return (int)ViewTypes.EventInfo;
                }
            } else if (groupPosition == DescriptionGroupPosition) {
                if (position == 0) {
                    return (int)ViewTypes.IconText;
                }
            } else if (groupPosition == AttachmentsGroupPosition) {
                if (position < Attachments.Count) {
                    return (int)ViewTypes.Attachment;
                }
            } else if (groupPosition == ReminderGroupPosition) {
                if (position == 0) {
                    return (int)ViewTypes.IconText;
                }
            } else if (groupPosition == NotesGroupPosition) {
                if (position == 0) {
                    return (int)ViewTypes.IconText;
                }
            } else if (groupPosition == CalendarGroupPosition) {
                if (position == 0) {
                    return (int)ViewTypes.IconText;
                }
            } else if (groupPosition == AttendeesGroupPosition) {
                if (position < Attendees.Count) {
                    return (int)ViewTypes.Attendee;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("EventViewFragment.GetItemViewType unexpected position: {0} {1}", groupPosition, position));
        }

        public override Android.Support.V7.Widget.RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            switch ((ViewTypes)viewType) {
            case ViewTypes.IconTextHeader:
                return IconTextHeaderViewHolder.Create (parent);
            case ViewTypes.EventInfo:
                return EventInfoViewHolder.Create (parent);
            case ViewTypes.IconText:
                return IconTextViewHolder.Create (parent);
            case ViewTypes.Attendee:
                return AttendeeViewHolder.Create (parent);
            case ViewTypes.Attachment:
                return AttachmentViewHolder.Create (parent);
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("EventViewFragment.OnCreateGroupedViewHolder unexpected viewType: {0}", viewType));
        }

        public override void OnBindViewHolder (Android.Support.V7.Widget.RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            if (groupPosition == InfoGroupPosition) {
                if (position == 0) {
                    (holder as EventInfoViewHolder).SetEvent (Event);
                    return;
                }
            } else if (groupPosition == DescriptionGroupPosition) {
                if (position == 0) {
                    // TODO: get body text
                    var text = "";
                    holder.ItemView.Clickable = false;
                    (holder as IconTextViewHolder).SetIconText (Resource.Drawable.event_icon_description, text);
                    return;
                }
            } else if (groupPosition == AttachmentsGroupPosition) {
                if (position < Attachments.Count) {
                    var attachment = Attachments[position];
                    (holder as AttachmentViewHolder).SetAttachment (attachment);
                    return;
                }
            } else if (groupPosition == ReminderGroupPosition) {
                if (position == 0) {
                    // TODO: get reminder text
                    var text = "";
                    holder.ItemView.Clickable = true;
                    (holder as IconTextViewHolder).SetIconText (Resource.Drawable.event_icon_reminder, text);
                    return;
                }
            } else if (groupPosition == NotesGroupPosition) {
                if (position == 0) {
                    // TODO: get notes text
                    var text = "";
                    holder.ItemView.Clickable = true;
                    (holder as IconTextViewHolder).SetIconText (Resource.Drawable.event_icon_notes, text);
                    return;
                }
            } else if (groupPosition == CalendarGroupPosition) {
                if (position == 0) {
                    // TODO: get calendar text
                    var text = "";
                    holder.ItemView.Clickable = false;
                    (holder as IconTextViewHolder).SetIconText (Resource.Drawable.event_icon_calendar, text);
                    return;
                }
            } else if (groupPosition == AttendeesGroupPosition) {
                if (position < Attendees.Count) {
                    var attendee = Attendees[position];
                    (holder as AttendeeViewHolder).SetAttendee (attendee);
                    return;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("EventViewFragment.GetItemViewType unexpected position: {0} {1}", groupPosition, position));
        }

        public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                if (groupPosition == ReminderGroupPosition) {
                    if (position == 0) {
                        listener.OnReminderSelected ();
                    }
                } else if (groupPosition == NotesGroupPosition) {
                    if (position == 0) {
                        listener.OnNotesSelected ();
                    }
                } else if (groupPosition == AttachmentsGroupPosition) {
                    if (position < Attachments.Count) {
                        var attachment = Attachments [position];
                        listener.OnAttachmentSelected (attachment);
                    }
                }
            }
        }

        class IconTextHeaderViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            ImageView IconView;
            TextView Label;

            public static IconTextHeaderViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.EventViewIconTextHeader, parent, false);
                return new IconTextHeaderViewHolder (view);
            }

            public IconTextHeaderViewHolder (View view) : base (view)
            {
                IconView = view.FindViewById (Resource.Id.icon) as ImageView;
                Label = view.FindViewById (Resource.Id.label) as TextView;
            }

            public void SetIconText (int iconResource, string text)
            {
                IconView.SetImageResource (iconResource);
                Label.Text = text;
            }

            public void SetIconText (int iconResource, int textResource)
            {
                IconView.SetImageResource (iconResource);
                Label.SetText (textResource);
            }
        }

        class EventInfoViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            TextView SubjectLabel;
            TextView TimeLabel;
            TextView LocationLabel;
            View LocationGroup;

            public static EventInfoViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.EventViewInfoItem, parent, false);
                return new EventInfoViewHolder (view);
            }

            public EventInfoViewHolder (View view) : base (view)
            {
                SubjectLabel = view.FindViewById (Resource.Id.subject) as TextView;
                TimeLabel = view.FindViewById (Resource.Id.time) as TextView;
                LocationLabel = view.FindViewById (Resource.Id.location) as TextView;
                LocationGroup = view.FindViewById (Resource.Id.location_group);
            }

            public void SetEvent (McEvent calendarEvent)
            {
                SubjectLabel.Text = calendarEvent.Subject ?? "";
                // TODO: fill in TimeLabel.Text
                if (String.IsNullOrWhiteSpace (calendarEvent.Location)) {
                    LocationGroup.Visibility = ViewStates.Gone;
                } else {
                    LocationGroup.Visibility = ViewStates.Visible;
                    LocationLabel.Text = calendarEvent.Location;
                }
            }
        }

        class IconTextViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            ImageView IconView;
            TextView Label;

            public static IconTextViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.EventViewIconTextItem, parent, false);
                return new IconTextViewHolder (view);
            }

            public IconTextViewHolder (View view) : base (view)
            {
                IconView = view.FindViewById (Resource.Id.icon) as ImageView;
                Label = view.FindViewById (Resource.Id.label) as TextView;
            }

            public void SetIconText (int iconResource, string text)
            {
                IconView.SetImageResource (iconResource);
                Label.Text = text;
            }
        }

        class AttachmentViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            ImageView IconView;
            TextView NameLabel;
            TextView DetailLabel;

            public static AttachmentViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.EventViewAttachmentItem, parent, false);
                return new AttachmentViewHolder (view);
            }

            public AttachmentViewHolder (View view) : base (view)
            {
                // TODO: find views
            }

            public void SetAttachment (McAttachment attachment)
            {
                
            }
        }

        class AttendeeViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            PortraitView PortraitView;
            TextView NameLabel;
            TextView StatusLabel;

            public static AttendeeViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.EventViewAttendeeItem, parent, false);
                return new AttendeeViewHolder (view);
            }

            public AttendeeViewHolder (View view) : base (view)
            {
                // TODO: find views
            }

            public void SetAttendee (McAttendee attendee)
            {
                // TODO:
            }
        }
    }
}
