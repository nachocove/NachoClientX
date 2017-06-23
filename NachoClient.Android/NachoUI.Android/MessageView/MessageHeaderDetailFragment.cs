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
using MimeKit;

namespace NachoClient.AndroidClient
{
    public class MessageHeaderDetailFragment : Fragment, MessageHeaderDetailAdapter.Listener
    {

        public McEmailMessage Message;

        #region Subviews

        RecyclerView ListView;
        MessageHeaderDetailAdapter Adapter;

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

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.MessageHeaderDetailFragment, container, false);
            FindSubviews (view);
            Adapter = new MessageHeaderDetailAdapter (this, Message);
            ListView.SetAdapter (Adapter);
            return view;
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        #region Adapter Listener

        public void OnContactSelected (McContact contact)
        {
            ShowContact (contact);
        }

        #endregion

        #region Private Helpers

        void ShowContact (McContact contact)
        {
            // TODO: show contact detail
        }

        #endregion

    }

    public class MessageHeaderDetailAdapter : GroupedListRecyclerViewAdapter
    {

        public interface Listener
        {
            void OnContactSelected (McContact contact);
        }

        int FromSection = -1;
        int ToSection = -1;
        int CcSection = -1;
        int BccSection = -1;
        int SectionCount = 0;

        int FromRow = -1;
        int ReplyToRow = -1;

        WeakReference<Listener> WeakListener;
        McEmailMessage Message;

        MailboxAddress FromAddress;
        MailboxAddress ReplyToAddress;

        MailboxAddress [] ToAddresses;
        MailboxAddress [] CcAddresses;
        MailboxAddress [] BccAddresses;

        Dictionary<string, NcContactPortraitEmailIndex> PortraitCache;

        public MessageHeaderDetailAdapter (Listener listener, McEmailMessage message) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
            Message = message;
            Setup ();
        }

        #region Loading Data

        void Setup ()
        {
            ParseHeaders ();
            CachePortraits ();
            DetermineTableSections ();
        }

        void ParseHeaders ()
        {
            FromAddress = null;
            ReplyToAddress = null;
            ToAddresses = null;
            CcAddresses = null;
            BccAddresses = null;
            MailboxAddress.TryParse (Message.From, out FromAddress);
            if (!String.IsNullOrWhiteSpace (Message.ReplyTo)) {
                if (MailboxAddress.TryParse (Message.ReplyTo, out ReplyToAddress)) {
                    if (FromAddress != null && FromAddress.Address.ToLowerInvariant () == ReplyToAddress.Address.ToLowerInvariant ()) {
                        ReplyToAddress = null;
                    }
                }
            }

            InternetAddressList iList;
            if (!String.IsNullOrWhiteSpace (Message.To) && InternetAddressList.TryParse (Message.To, out iList)) {
                ToAddresses = iList.Mailboxes.ToArray ();
            }
            if (!String.IsNullOrWhiteSpace (Message.Cc) && InternetAddressList.TryParse (Message.Cc, out iList)) {
                CcAddresses = iList.Mailboxes.ToArray ();
            }
            if (!String.IsNullOrWhiteSpace (Message.Bcc) && InternetAddressList.TryParse (Message.Bcc, out iList)) {
                BccAddresses = iList.Mailboxes.ToArray ();
            }
        }

        void CachePortraits ()
        {
            var entries = McContact.QueryForMessagePortraitEmails (Message.Id);
            PortraitCache = new Dictionary<string, NcContactPortraitEmailIndex> (entries.Count);
            foreach (var entry in entries) {
                if (!PortraitCache.ContainsKey (entry.EmailAddress.ToLowerInvariant ())) {
                    PortraitCache.Add (entry.EmailAddress.ToLowerInvariant (), entry);
                }
            }
        }

        void DetermineTableSections ()
        {
            int section = 0;
            int row = 0;
            if (FromAddress != null) {
                FromRow = row;
                FromSection = section;
                section += 1;
                row += 1;
            }
            if (ReplyToAddress != null) {
                if (FromSection == -1) {
                    FromSection = 1;
                    section += 1;
                }
                ReplyToRow = row;
                row += 1;
            }
            if (ToAddresses != null) {
                ToSection = section;
                section += 1;
            } else {
                ToSection = -1;
            }
            if (CcAddresses != null) {
                CcSection = section;
                section += 1;
            } else {
                CcSection = -1;
            }
            if (BccAddresses != null) {
                BccSection = section;
                section += 1;
            } else {
                BccSection = -1;
            }
            SectionCount = section;
        }

        #endregion

        public override int GroupCount {
            get {
                return SectionCount;
            }
        }

        public override int GroupItemCount (int groupPosition)
        {
            if (groupPosition == FromSection) {
                if (FromAddress != null && ReplyToAddress != null) {
                    return 2;
                }
                return 1;
            } else if (groupPosition == ToSection) {
                return ToAddresses.Length;
            } else if (groupPosition == CcSection) {
                return CcAddresses.Length;
            } else if (groupPosition == BccSection) {
                return BccAddresses.Length;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("MessageHeaderDetailFragment.GroupItemCount unexpected group position: {0}", groupPosition));
        }

        public override string GroupHeaderValue (Context context, int groupPosition)
        {
            if (groupPosition == FromSection) {
                if (FromAddress != null && ReplyToAddress != null) {
                    return context.GetString (Resource.String.message_header_detail_from_replyto);
                }
                return context.GetString (Resource.String.message_header_detail_from);
            } else if (groupPosition == ToSection) {
                return context.GetString (Resource.String.message_header_detail_to);
            } else if (groupPosition == CcSection) {
                return context.GetString (Resource.String.message_header_detail_cc);
            } else if (groupPosition == BccSection) {
                return context.GetString (Resource.String.message_header_detail_bcc);
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("MessageHeaderDetailFragment.GroupHeaderValue unexpected group position: {0}", groupPosition));
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            return MessageContactViewHolder.Create (parent);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            var contactHolder = (holder as MessageContactViewHolder);
            var mailbox = MailboxForPosition (groupPosition, position);
            if (mailbox == null) {
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("MessageHeaderDetailFragment.OnBindViewHolder unexpected position: {0}.{1}", groupPosition, position));
            }
            NcContactPortraitEmailIndex portraitEntry;
            int portraitId = 0;
            int colorIndex = 1;
            if (PortraitCache.TryGetValue (mailbox.Address.ToLowerInvariant (), out portraitEntry)) {
                portraitId = portraitEntry.PortraitId;
                colorIndex = portraitEntry.ColorIndex;
            }
            contactHolder.SetAddress (mailbox, portraitId, colorIndex);
        }

        public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {

                var mailbox = MailboxForPosition (groupPosition, position);
                if (mailbox != null) {
                    var contact = McContact.QueryBestMatchByEmailAddress (Message.AccountId, mailbox.Address);
                    if (contact != null) {
                        listener.OnContactSelected (contact);
                    }
                }
            }
        }

        MailboxAddress MailboxForPosition (int groupPosition, int position)
        {
            MailboxAddress address = null;
            if (groupPosition == FromSection) {
                if (position == FromRow) {
                    address = FromAddress;
                } else if (position == ReplyToRow) {
                    address = ReplyToAddress;
                }
            } else if (groupPosition == ToSection) {
                address = ToAddresses [position];
            } else if (groupPosition == CcSection) {
                address = CcAddresses [position];
            } else if (groupPosition == BccSection) {
                address = BccAddresses [position];
            }
            return address;
        }

        class MessageContactViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            TextView NameLabel;
            TextView EmailLabel;
            PortraitView PortraitView;

            public static MessageContactViewHolder Create (ViewGroup parent)
            {
                var view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.MessageHeaderDetailListItem, parent, false);
                return new MessageContactViewHolder (view);
            }

            public MessageContactViewHolder (View view) : base (view)
            {
                NameLabel = view.FindViewById (Resource.Id.main_label) as TextView;
                EmailLabel = view.FindViewById (Resource.Id.detail_label) as TextView;
                PortraitView = view.FindViewById (Resource.Id.portrait_view) as PortraitView;
            }


            public void SetAddress (MailboxAddress mailbox, int portraitId, int colorIndex)
            {
                var initials = EmailHelper.Initials (mailbox.ToString ());
                PortraitView.SetPortrait (portraitId, colorIndex, initials);
                if (!String.IsNullOrWhiteSpace (mailbox.Name)) {
                    NameLabel.Text = mailbox.Name;
                    EmailLabel.Text = mailbox.Address;
                    EmailLabel.Visibility = ViewStates.Visible;
                } else {
                    NameLabel.Text = mailbox.Address;
                    EmailLabel.Text = "";
                    EmailLabel.Visibility = ViewStates.Gone;
                }
            }
        }
    }
}
