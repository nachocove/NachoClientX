//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.IO;
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
using Android.Views.InputMethods;

using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.AndroidClient
{
    public class EventEditFragment : Fragment, EventEditAdapter.Listener
    {

        private const string FRAGMENT_ADD_ATTENDEE = "NachoClient.AndroidClient.EventEditFragment.ADD_ATTENDEE";
        private const string FRAGMENT_REMINDER_DIALOG = "NachoClient.AndroidClient.EventEditFragment.FRAGMENT_REMINDER_DIALOG";
        private const string FRAGMENT_CALENDAR_DIALOG = "NachoClient.AndroidClient.EventEditFragment.FRAGMENT_CALENDAR_DIALOG";

        public McCalendar CalendarItem;

        EventEditAdapter Adapter;
        AttachmentPicker AttachmentPicker = new AttachmentPicker ();

        public EventEditFragment () : base ()
        {
            RetainInstance = true;
        }

        #region Subviews

        RecyclerView ListView;

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
            AttachmentPicker.AttachmentPicked += AttachmentPicked;
            if (savedInstanceState != null) {
                AttachmentPicker.OnCreate (savedInstanceState);
            }
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.EventEditFragment, container, false);
            FindSubviews (view);
            Adapter = new EventEditAdapter (this, CalendarItem);
            ListView.SetAdapter (Adapter);
            return view;
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        public override void OnDestroy ()
        {
            AttachmentPicker.AttachmentPicked -= AttachmentPicked;
            base.OnDestroy ();
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            if (AttachmentPicker.OnActivityResult (this, CalendarItem.AccountId, requestCode, resultCode, data)) {
                return;
            }
            base.OnActivityResult (requestCode, resultCode, data);
        }

        #endregion

        #region Listener


        public void OnReminderSelected ()
        {
            ShowReminderPicker ();
        }

        public void OnAttachmentSelected (McAttachment attachment)
        {
        }

        public void OnStartSelected ()
        {
            ShowDatePicker (CalendarItem.StartTime, (date) => {
                var delta = CalendarItem.EndTime - CalendarItem.StartTime;
                CalendarItem.StartTime = date;
                CalendarItem.EndTime = CalendarItem.StartTime + delta;
                Adapter.NotifyStartChanged ();
                Adapter.NotifyEndChanged ();
            });
        }

        public void OnEndSelected ()
        {
            ShowDatePicker (CalendarItem.EndTime, (date) => {
                CalendarItem.EndTime = date;
                Adapter.NotifyEndChanged ();
                if (CalendarItem.EndTime < CalendarItem.StartTime) {
                    CalendarItem.StartTime = CalendarItem.EndTime;
                    Adapter.NotifyStartChanged ();
                }
            });
        }

        public void OnCalendarSelected ()
        {
            ShowCalendarPicker ();
        }

        public void OnAddAttachmentSelected ()
        {
            ShowAttachmentPicker ();
        }

        public void OnAddAttendeeSelected ()
        {
            ShowAddAttendees ();
        }

        #endregion

        #region Private Helpers

        public void EndEditing ()
        {
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.HideSoftInputFromWindow (View.WindowToken, HideSoftInputFlags.NotAlways);
        }

        public void Save ()
        {
            CalendarHelper.Save (CalendarItem, Adapter.Account, Adapter.Folder, Adapter.Attachments, Adapter.Attendees);
        }

        void ShowReminderPicker ()
        {
            var dialog = new EventReminderPickerDialog (CalendarItem.ReminderIsSet, CalendarItem.Reminder);
            dialog.Show (FragmentManager, FRAGMENT_REMINDER_DIALOG, (isSet, reminder) => {
                if (CalendarItem.ReminderIsSet != isSet || reminder != CalendarItem.Reminder) {
                    CalendarItem.ReminderIsSet = isSet;
                    CalendarItem.Reminder = reminder;
                    Adapter.NotifyReminderChanged ();
                }
            });
        }

        void ShowCalendarPicker ()
        {
            var dialog = new CalendarPickerDialog ();
            dialog.Show (FragmentManager, FRAGMENT_CALENDAR_DIALOG, Adapter.Account, Adapter.Folder, () => {
                Adapter.Account = dialog.SelectedAccount;
                Adapter.Folder = dialog.SelectedFolder;
                Adapter.NotifyCalendarChanged ();
            });
        }

        void ShowAddAttendees ()
        {
            var attendeeDialog = new AddAttendeeDialog ((required, optional) => {
                var attendees = CalendarHelper.CreateAttendees (CalendarItem.AccountId, required, optional);
                Adapter.AddAttendees (attendees);
            });
            attendeeDialog.Show (FragmentManager, FRAGMENT_ADD_ATTENDEE);
        }

        void ShowAttachmentPicker ()
        {
            AttachmentPicker.Show (this, CalendarItem.AccountId);
        }

        void AttachmentPicked (object sender, McAttachment attachment)
        {
            Adapter.AddAttachment (attachment);
        }

        void ShowDatePicker (DateTime initialValue, Action<DateTime> completion)
        {
            var localInitialValue = initialValue.ToLocalTime ();
            DatePicker.Show (Activity, localInitialValue, DateTime.MinValue, DateTime.MaxValue, (DateTime date) => {
                if (CalendarItem.AllDayEvent) {
                    completion (date);
                } else {
                    TimePicker.Show (Activity, localInitialValue.TimeOfDay, (span) => {
                        completion (date + span);
                    });
                }
            });
        }

        void ShowAttachment (McAttachment attachment)
        {
            AttachmentHelper.OpenAttachment (Activity, attachment);
        }

        #endregion

    }

    public class EventEditAdapter : GroupedListRecyclerViewAdapter
    {
        public interface Listener
        {
            void OnReminderSelected ();
            void OnAttachmentSelected (McAttachment attachment);
            void OnStartSelected ();
            void OnEndSelected ();
            void OnCalendarSelected ();
            void OnAddAttachmentSelected ();
            void OnAddAttendeeSelected ();
        }

        enum ViewType
        {
            InlineText,
            Switch,
            NameValue,
            Attachment,
            Attendee
        }

        WeakReference<Listener> WeakListener;

        McCalendar CalendarItem;
        public McAccount Account;
        public McFolder Folder;
        public List<McAttachment> Attachments { get; private set; }
        public List<McAttendee> Attendees { get; private set; }

        int _GroupCount = 0;
        int InfoGroupPosition = -1;
        int InfoGroupCount = 0;
        int InfoSubjectPosition = -1;
        int InfoAllDayPosition = -1;
        int InfoStartPosition = -1;
        int InfoEndPosition = -1;
        int InfoLocationPosition = -1;
        int InfoCalendarPosition = -1;
        int DetailsGroupPosition = -1;
        int DetailsGroupCount = -1;
        int DetailsDescriptionPosition = -1;
        int DetailsReminderPosition = -1;
        int AttachmentsGroupPosition = -1;
        int AttendeesGroupPosition = -1;
        int AttachmentsGroupExtraCount = 0;
        int AttendeesGroupExtraCount = 0;
        int AttachmentsAddExtraPosition = -1;
        int AttendeesAddExtraPosition = -1;

        public EventEditAdapter (Listener listener, McCalendar calendarItem) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
            CalendarItem = calendarItem;
            Attachments = new List<McAttachment> (CalendarItem.attachments);
            Attendees = new List<McAttendee> (CalendarItem.attendees);
            Account = McAccount.QueryById<McAccount> (CalendarItem.AccountId);
            Folder = null;
            if (CalendarItem.Id != 0) {
                Folder = McFolder.QueryByFolderEntryId<McCalendar> (Account.Id, CalendarItem.Id).FirstOrDefault ();
            }
            if (Folder == null) {
                var folders = new NachoFolders (Account.Id, NachoFolders.FilterForCalendars);
                Folder = folders.GetFirstOfTypeOrDefault (NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            }
            ConfigureGroups ();
        }

        void ConfigureGroups ()
        {
            _GroupCount = 0;
            InfoGroupCount = 0;
            AttachmentsGroupExtraCount = 0;
            AttendeesGroupExtraCount = 0;

            InfoGroupPosition = -1;
            InfoSubjectPosition = -1;
            InfoAllDayPosition = -1;
            InfoStartPosition = -1;
            InfoEndPosition = -1;
            InfoLocationPosition = -1;
            InfoCalendarPosition = -1;
            DetailsGroupPosition = -1;
            DetailsGroupCount = 0;
            DetailsDescriptionPosition = -1;
            DetailsReminderPosition = -1;
            AttachmentsGroupPosition = -1;
            AttendeesGroupPosition = -1;
            AttachmentsAddExtraPosition = -1;
            AttendeesAddExtraPosition = -1;

            InfoGroupPosition = _GroupCount++;
            InfoSubjectPosition = InfoGroupCount++;
            InfoAllDayPosition = InfoGroupCount++;
            InfoStartPosition = InfoGroupCount++;
            InfoEndPosition = InfoGroupCount++;
            InfoLocationPosition = InfoGroupCount++;
            InfoCalendarPosition = InfoGroupCount++;
            DetailsGroupPosition = _GroupCount++;
            DetailsReminderPosition = DetailsGroupCount++;
            DetailsDescriptionPosition = DetailsGroupCount++;
            AttachmentsGroupPosition = _GroupCount++;
            AttachmentsAddExtraPosition = AttachmentsGroupExtraCount++;
            AttendeesGroupPosition = _GroupCount++;
            AttendeesAddExtraPosition = AttendeesGroupExtraCount++;
        }

        public void NotifyStartChanged ()
        {
            NotifyItemChanged (InfoGroupPosition, InfoStartPosition);
        }

        public void NotifyEndChanged ()
        {
            NotifyItemChanged (InfoGroupPosition, InfoEndPosition);
        }

        public void NotifyCalendarChanged ()
        {
            NotifyItemChanged (InfoGroupPosition, InfoCalendarPosition);
        }

        public void NotifyReminderChanged ()
        {
            NotifyItemChanged (DetailsGroupPosition, DetailsReminderPosition);
        }

        public override int GroupCount {
            get {
                return _GroupCount;
            }
        }

        public void AddAttachment (McAttachment attachment)
        {
            var attachments = new List<McAttachment> ();
            attachments.Add (attachment);
            AddAttachments (attachments);
        }

        public void AddAttachments (List<McAttachment> attachments)
        {
            var position = Attachments.Count;
            foreach (var attachment in attachments) {
                Attachments.Add (attachment);
            }
            NotifyItemRangeInserted (AttachmentsGroupPosition, position, attachments.Count);
        }

        public void AddAttendees (List<McAttendee> attendees)
        {
            var position = Attendees.Count;
            foreach (var attendee in attendees) {
                Attendees.Add (attendee);
            }
            NotifyItemRangeInserted (AttendeesGroupPosition, position, attendees.Count);
        }

        public override int GroupItemCount (int groupPosition)
        {
            if (groupPosition == InfoGroupPosition) {
                return InfoGroupCount;
            }
            if (groupPosition == DetailsGroupPosition) {
                return DetailsGroupCount;
            }
            if (groupPosition == AttachmentsGroupPosition) {
                return Attachments.Count + AttachmentsGroupExtraCount;
            }
            if (groupPosition == AttendeesGroupPosition) {
                return Attendees.Count + AttendeesGroupExtraCount;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("EventEditFragment.GroupItemCount unexpected position: {0}", groupPosition));
        }

        public override string GroupHeaderValue (Context context, int groupPosition)
        {
            if (groupPosition == InfoGroupPosition) {
                return context.GetString (Resource.String.event_edit_section_info);
            }
            if (groupPosition == DetailsGroupPosition) {
                return context.GetString (Resource.String.event_edit_section_description);
            }
            if (groupPosition == AttachmentsGroupPosition) {
                return context.GetString (Resource.String.event_edit_section_attachments);
            }
            if (groupPosition == AttendeesGroupPosition) {
                return context.GetString (Resource.String.event_edit_section_attendees);
            }
            return null;
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            switch ((ViewType)viewType) {
            case ViewType.InlineText:
                return InlineTextEditViewHolder.Create (parent);
            case ViewType.Attendee:
                return AttendeeViewHolder.Create (parent);
            case ViewType.Attachment:
                return AttachmentViewHolder.Create (parent);
            case ViewType.NameValue:
                return SettingsBasicItemViewHolder.Create (parent);
            case ViewType.Switch:
                return SettingsSwitchItemViewHolder.Create (parent);
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("EventEditFragment.OnCreateGroupedViewHolder unknown view type: {0}", viewType));
        }

        public override int GetItemViewType (int groupPosition, int position)
        {
            if (groupPosition == InfoGroupPosition) {
                if (position == InfoSubjectPosition) {
                    return (int)ViewType.InlineText;
                }
                if (position == InfoAllDayPosition) {
                    return (int)ViewType.Switch;
                }
                if (position == InfoEndPosition) {
                    return (int)ViewType.NameValue;
                }
                if (position == InfoStartPosition) {
                    return (int)ViewType.NameValue;
                }
                if (position == InfoLocationPosition) {
                    return (int)ViewType.InlineText;
                }
                if (position == InfoCalendarPosition) {
                    return (int)ViewType.NameValue;
                }
            } else if (groupPosition == DetailsGroupPosition) {
                if (position == DetailsDescriptionPosition) {
                    return (int)ViewType.InlineText;
                }
                if (position == DetailsReminderPosition) {
                    return (int)ViewType.NameValue;
                }
            } else if (groupPosition == AttachmentsGroupPosition) {
                if (position < Attachments.Count) {
                    return (int)ViewType.Attachment;
                } else {
                    var extraPosition = position - Attachments.Count;
                    if (extraPosition == AttachmentsAddExtraPosition) {
                        return (int)ViewType.NameValue;
                    }
                }
            } else if (groupPosition == AttendeesGroupPosition) {
                if (position < Attendees.Count) {
                    return (int)ViewType.Attendee;
                } else {
                    var extraPosition = position - Attendees.Count;
                    if (extraPosition == AttendeesAddExtraPosition) {
                        return (int)ViewType.NameValue;
                    }
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("EventEditFragment.OnBindViewHolder unexpected position: {0}.{1}", groupPosition, position));
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            if (groupPosition == InfoGroupPosition) {
                if (position == InfoSubjectPosition) {
                    var textHolder = (holder as InlineTextEditViewHolder);
                    textHolder.SetText (CalendarItem.Subject, Resource.String.event_edit_subject);
                    textHolder.SetChangeHandler ((sender, e) => {
                        CalendarItem.Subject = e.Text.ToString ();
                    });
                    return;
                }
                if (position == InfoAllDayPosition) {
                    var switchHolder = (holder as SettingsSwitchItemViewHolder);
                    switchHolder.SetLabels (Resource.String.event_edit_all_day);
                    switchHolder.SetChangeHandler ((sender, e) => {
                        CalendarItem.AllDayEvent = e.IsChecked;
                        NotifyStartChanged ();
                        NotifyEndChanged ();
                    });
                    return;
                }
                if (position == InfoStartPosition) {
                    var text = Pretty.EventEditTime (CalendarItem.StartTime, CalendarItem.AllDayEvent, isEnd: false);
                    (holder as SettingsBasicItemViewHolder).SetLabels (Resource.String.event_edit_start, text);
                    return;
                }
                if (position == InfoEndPosition) {
                    var text = Pretty.EventEditTime (CalendarItem.EndTime, CalendarItem.AllDayEvent, isEnd: true);
                    (holder as SettingsBasicItemViewHolder).SetLabels (Resource.String.event_edit_end, text);
                    return;
                }
                if (position == InfoLocationPosition) {
                    var textHolder = (holder as InlineTextEditViewHolder);
                    textHolder.SetText (CalendarItem.Location, Resource.String.event_edit_location);
                    textHolder.SetChangeHandler ((sender, e) => {
                        CalendarItem.Location = e.Text.ToString ();
                    });
                    return;
                }
                if (position == InfoCalendarPosition) {
                    (holder as SettingsBasicItemViewHolder).SetLabels (Resource.String.event_edit_calendar, CalendarHelper.CalendarName (Account, Folder));
                    return;
                }
            } else if (groupPosition == DetailsGroupPosition) {
                if (position == DetailsDescriptionPosition) {
                    var textHolder = (holder as InlineTextEditViewHolder);
                    textHolder.SetText (CalendarItem.PlainDescription, Resource.String.event_edit_description);
                    textHolder.SetChangeHandler ((sender, e) => {
                        CalendarItem.PlainDescription = e.Text.ToString ();
                    });
                    return;
                }
                if (position == DetailsReminderPosition) {
                    var text = Pretty.ReminderString (CalendarItem.ReminderIsSet, CalendarItem.Reminder);
                    (holder as SettingsBasicItemViewHolder).SetLabels (Resource.String.event_edit_section_reminder, text);
                    return;
                }
            } else if (groupPosition == AttachmentsGroupPosition) {
                if (position < Attachments.Count) {
                    var attachment = Attachments [position];
                    var attachmentHolder = (holder as AttachmentViewHolder);
                    attachmentHolder.SetAttachment (attachment);
                    attachmentHolder.SetClickHandler ((sender, e) => {
                        Listener listener;
                        if (WeakListener.TryGetTarget (out listener)) {
                            listener.OnAttachmentSelected (attachment);
                        }
                    });
                    attachmentHolder.SetRemoveHandler ((sender, e) => {
                        var index = Attachments.IndexOf (attachment);
                        Attachments.RemoveAt (index);
                        NotifyItemRemoved (AttachmentsGroupPosition, index);
                    });
                    return;
                } else {
                    var extraPosition = position - Attachments.Count;
                    if (extraPosition == AttachmentsAddExtraPosition) {
                        (holder as SettingsBasicItemViewHolder).SetLabels (Resource.String.event_edit_add_attachment);
                        return;
                    }
                }
            } else if (groupPosition == AttendeesGroupPosition) {
                if (position < Attendees.Count) {
                    var attendee = Attendees [position];
                    var attendeeHolder = (holder as AttendeeViewHolder);
                    attendeeHolder.SetAttendee (attendee);
                    attendeeHolder.SetRemoveHandler ((sender, e) => {
                        var index = Attendees.IndexOf (attendee);
                        Attendees.RemoveAt (index);
                        NotifyItemRemoved (AttendeesGroupPosition, index);
                    });
                    return;
                } else {
                    var extraPosition = position - Attendees.Count;
                    if (extraPosition == AttendeesAddExtraPosition) {
                        (holder as SettingsBasicItemViewHolder).SetLabels (Resource.String.event_edit_add_attendees);
                        return;
                    }
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("EventEditFragment.OnBindViewHolder unexpected position: {0}.{1}", groupPosition, position));
        }

        public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                if (groupPosition == InfoGroupPosition) {
                    if (position == InfoStartPosition) {
                        listener.OnStartSelected ();
                    } else if (position == InfoEndPosition) {
                        listener.OnEndSelected ();
                    } else if (position == InfoCalendarPosition) {
                        listener.OnCalendarSelected ();
                    }
                } else if (groupPosition == DetailsGroupPosition) {
                    if (position == DetailsReminderPosition) {
                        listener.OnReminderSelected ();
                    }
                } else if (groupPosition == AttachmentsGroupPosition) {
                    if (position < Attachments.Count) {
                        var attachment = Attachments [position];
                        listener.OnAttachmentSelected (attachment);
                    } else {
                        position -= Attachments.Count;
                        if (position == AttendeesAddExtraPosition) {
                            listener.OnAddAttachmentSelected ();
                        }
                    }
                } else if (groupPosition == AttendeesGroupPosition) {
                    if (position >= Attendees.Count) {
                        position -= Attendees.Count;
                        if (position == AttendeesAddExtraPosition) {
                            listener.OnAddAttendeeSelected ();
                        }
                    }
                }
            }
        }

        class InlineTextEditViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            EditText Input;
            EventHandler<Android.Text.TextChangedEventArgs> ChangeHandler;

            public static InlineTextEditViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.EventEditInlineTextItem, parent, false);
                return new InlineTextEditViewHolder (view);
            }

            public InlineTextEditViewHolder (View view) : base (view)
            {
                Input = view.FindViewById (Resource.Id.input) as EditText;
            }

            public void SetText (string text, int placeholderResource)
            {
                Input.Text = text;
                Input.SetHint (placeholderResource);
            }

            public void SetChangeHandler (EventHandler<Android.Text.TextChangedEventArgs> changeHandler)
            {
                if (ChangeHandler != null) {
                    Input.TextChanged -= ChangeHandler;
                }
                ChangeHandler = changeHandler;
                if (ChangeHandler != null) {
                    Input.TextChanged += ChangeHandler;
                }
            }

        }

        class AttachmentViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {
            ImageView IconView;
            TextView NameLabel;
            TextView DetailLabel;
            View RemoveButton;

            EventHandler ClickHandler;
            EventHandler RemoveHandler;

            public event EventHandler RemoveClicked;

            public static AttachmentViewHolder Create (ViewGroup parent)
            {
                var view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.EventEditAttachmentItem, parent, false);
                return new AttachmentViewHolder (view);
            }

            public AttachmentViewHolder (View view) : base (view)
            {
                IconView = view.FindViewById (Resource.Id.icon) as ImageView;
                RemoveButton = view.FindViewById (Resource.Id.remove_button);
                NameLabel = view.FindViewById (Resource.Id.attachment_name) as TextView;
                DetailLabel = view.FindViewById (Resource.Id.attachment_detail) as TextView;
                RemoveButton.Click += (sender, e) => {
                    RemoveClicked.Invoke (this, new EventArgs ());
                };
            }

            public void SetAttachment (McAttachment attachment)
            {
                var name = Path.GetFileNameWithoutExtension (attachment.DisplayName);
                if (String.IsNullOrEmpty (name)) {
                    name = "(no name)";
                }
                IconView.SetImageResource (AttachmentHelper.FileIconFromExtension (attachment));
                NameLabel.Text = name;
                DetailLabel.Text = Pretty.GetAttachmentDetail (attachment);
            }

            public void SetClickHandler (EventHandler clickHandler)
            {
                if (ClickHandler != null) {
                    ItemView.Click -= ClickHandler;
                }
                ClickHandler = clickHandler;
                if (ClickHandler != null) {
                    ItemView.Click += ClickHandler;
                }
            }

            public void SetRemoveHandler (EventHandler removeHandler)
            {
                if (RemoveHandler != null) {
                    RemoveClicked -= RemoveHandler;
                }
                RemoveHandler = removeHandler;
                if (RemoveHandler != null) {
                    RemoveClicked += RemoveHandler;
                }
            }
        }

        class AttendeeViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            PortraitView PortraitView;
            TextView NameLabel;
            TextView StatusLabel;
            View RemoveButton;

            EventHandler RemoveHandler;
            public event EventHandler RemoveClicked;

            public static AttendeeViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.EventEditAttendeeItem, parent, false);
                return new AttendeeViewHolder (view);
            }

            public AttendeeViewHolder (View view) : base (view)
            {
                PortraitView = view.FindViewById (Resource.Id.portrait_view) as PortraitView;
                NameLabel = view.FindViewById (Resource.Id.name) as TextView;
                StatusLabel = view.FindViewById (Resource.Id.status) as TextView;
                RemoveButton = view.FindViewById (Resource.Id.remove_button);
                RemoveButton.Click += (sender, e) => {
                    RemoveClicked (this, new EventArgs ());
                };
            }

            public void SetAttendee (McAttendee attendee)
            {
                var contact = attendee.GetContact ();
                if (contact != null) {
                    var name = contact.GetDisplayName ();
                    if (String.IsNullOrWhiteSpace (name)) {
                        name = attendee.Email;
                    }
                    NameLabel.Text = name;
                    PortraitView.SetPortrait (contact.PortraitId, contact.CircleColor, ContactsHelper.GetInitials (contact));
                } else {
                    NameLabel.Text = attendee.DisplayName;
                    PortraitView.SetPortrait (0, 1, ContactsHelper.NameToLetters (attendee.DisplayName));
                }
                var status = Pretty.AttendeeStatus (attendee);
                if (String.IsNullOrEmpty (status)) {
                    StatusLabel.Visibility = ViewStates.Gone;
                } else {
                    StatusLabel.Visibility = ViewStates.Visible;
                    StatusLabel.Text = status;

                }
            }

            public void SetRemoveHandler (EventHandler removeHandler)
            {
                if (RemoveHandler != null) {
                    RemoveClicked -= RemoveHandler;
                }
                RemoveHandler = removeHandler;
                if (RemoveHandler != null) {
                    RemoveClicked += RemoveHandler;
                }
            }

        }

    }
}
