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

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class MessageHeaderView : LinearLayout
    {

        #region Subviews

        TextView SenderLabel;
        TextView SubjectLabel;
        TextView DateLabel;
        PortraitView PortraitView;

        void FindSubviews ()
        {
            SenderLabel = FindViewById (Resource.Id.sender) as TextView;
            SubjectLabel = FindViewById (Resource.Id.subject) as TextView;
            DateLabel = FindViewById (Resource.Id.date) as TextView;
            PortraitView = FindViewById (Resource.Id.portrait) as PortraitView;
        }

        #endregion

        #region Creating a Message Header View

        public MessageHeaderView (Context context) :
            base (context)
        {
            Initialize ();
        }

        public MessageHeaderView (Context context, IAttributeSet attrs) :
            base (context, attrs)
        {
            Initialize ();
        }

        public MessageHeaderView (Context context, IAttributeSet attrs, int defStyle) :
            base (context, attrs, defStyle)
        {
            Initialize ();
        }

        void Initialize ()
        {
            var view = LayoutInflater.From (Context).Inflate (Resource.Layout.MessageHeaderView, this);
            FindSubviews ();
        }

        #endregion

        #region Updating the View

        public void SetMessage (McEmailMessage message)
        {
            PortraitView.SetPortrait (message.cachedPortraitId, message.cachedFromColor, message.cachedFromLetters);
            DateLabel.Text = Pretty.FriendlyFullDateTime (message.DateReceived);
            // TODO: add styled intent to date label
            SenderLabel.Text = Pretty.SenderString (message.From);
            if (String.IsNullOrEmpty (message.Subject)) {
                SubjectLabel.Text = "(no subject)";
            } else {
                SubjectLabel.Text = Pretty.SubjectString (message.Subject);
            }
        }

        #endregion

    }
}
