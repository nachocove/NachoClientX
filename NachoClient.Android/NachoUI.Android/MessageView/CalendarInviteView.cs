//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;

using Android.Views;
using Android.Content;
using Android.Util;
using Android.Widget;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class CalendarInviteView : LinearLayout
    {

        public McEmailMessage Message;
        private McMeetingRequest _MeetingRequest;
        public McMeetingRequest MeetingRequest {
            get {
                return _MeetingRequest;
            }
            set {
                _MeetingRequest = value;
                if (_MeetingRequest != null) {
                    Update ();
                }
            }
        }

        public event EventHandler<NcResponseType> Respond;
        public event EventHandler Remove;

        ViewGroup ActionsView;
        ImageView IconView;
        TextView TextLabel;
        TextView DetailTextLabel;
        Button AcceptButton;
        Button TentativeButton;
        Button DeclineButton;
        Button RemoveButton;

        #region Creating an invite view

        public CalendarInviteView (Context context) :
            base (context)
        {
            Initialize ();
        }

        public CalendarInviteView (Context context, IAttributeSet attrs) :
            base (context, attrs)
        {
            Initialize ();
        }

        public CalendarInviteView (Context context, IAttributeSet attrs, int defStyle) :
            base (context, attrs, defStyle)
        {
            Initialize ();
        }

        void Initialize ()
        {
            var view = LayoutInflater.From (Context).Inflate (Resource.Layout.CalendarInviteView, this);
            FindSubviews ();
        }

        #endregion

        #region Subviews

        void FindSubviews ()
        {
            IconView = FindViewById (Resource.Id.icon) as ImageView;
            TextLabel = FindViewById (Resource.Id.text_label) as TextView;
            DetailTextLabel = FindViewById (Resource.Id.detail_label) as TextView;
            ActionsView = FindViewById (Resource.Id.actions) as ViewGroup;
            AcceptButton = FindViewById (Resource.Id.accept) as Button;
            TentativeButton = FindViewById (Resource.Id.tentative) as Button;
            DeclineButton = FindViewById (Resource.Id.decline) as Button;
            RemoveButton = FindViewById (Resource.Id.remove_button) as Button;

            AcceptButton.Click += Accept;
            TentativeButton.Click += Tenative;
            DeclineButton.Click += Decline;
            RemoveButton.Click += _Remove;
        }

        public void Cleanup ()
        {
            AcceptButton.Click -= Accept;
            TentativeButton.Click -= Tenative;
            DeclineButton.Click -= Decline;
            RemoveButton.Click -= _Remove;

            IconView = null;
            TextLabel = null;
            DetailTextLabel = null;
            ActionsView = null;
            AcceptButton = null;
            TentativeButton = null;
            DeclineButton = null;
            RemoveButton = null;
        }

        #endregion

        #region View Updates

        void Update ()
        {
            TextLabel.Text = Pretty.MeetingRequestTime (MeetingRequest);
            if (Message.IsMeetingResponse) {
                ActionsView.Visibility = ViewStates.Gone;
                IconView.SetImageResource (Resource.Drawable.calendar_invite_response);
                DetailTextLabel.Text = Pretty.MeetingResponse (Message);
            } else if (Message.IsMeetingCancelation) {
                if (MeetingRequest.IsOnCalendar ()) {
                    ActionsView.Visibility = ViewStates.Visible;
                } else {
                    ActionsView.Visibility = ViewStates.Gone;
                }
                AcceptButton.Visibility = ViewStates.Gone;
                TentativeButton.Visibility = ViewStates.Gone;
                DeclineButton.Visibility = ViewStates.Gone;
                RemoveButton.Visibility = ViewStates.Visible;
                IconView.SetImageResource (Resource.Drawable.calendar_invite_cancel);
                DetailTextLabel.SetText (Resource.String.calendar_invite_canceled);
            } else {
                ActionsView.Visibility = ViewStates.Visible;
                AcceptButton.Visibility = ViewStates.Visible;
                TentativeButton.Visibility = ViewStates.Visible;
                DeclineButton.Visibility = ViewStates.Visible;
                RemoveButton.Visibility = ViewStates.Gone;
                IconView.SetImageResource (Resource.Drawable.calendar_invite_request);
                if (!String.IsNullOrEmpty (MeetingRequest.Location)) {
                    DetailTextLabel.Text = MeetingRequest.Location;
                } else {
                    DetailTextLabel.Text = "";
                }
            }
            if (string.IsNullOrEmpty (DetailTextLabel.Text)) {
                DetailTextLabel.Visibility = ViewStates.Gone;
            } else {
                DetailTextLabel.Visibility = ViewStates.Visible;
            }
        }

        #endregion

        #region User Actions

        void Accept (object sender, EventArgs e)
        {
            Respond?.Invoke (this, NcResponseType.Accepted);
        }

        void Tenative (object sender, EventArgs e)
        {
            Respond?.Invoke (this, NcResponseType.Tentative);
        }

        void Decline (object sender, EventArgs e)
        {
            Respond?.Invoke (this, NcResponseType.Declined);
        }

        void _Remove (object sender, EventArgs e)
        {
            Remove?.Invoke (this, null);
        }

        #endregion
    }
}
