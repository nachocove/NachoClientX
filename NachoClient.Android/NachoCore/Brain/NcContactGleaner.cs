//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Linq;
using MimeKit;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.Brain
{
    public class NcContactGleaner
    {
        private const uint MaxSaneAddressLength = 40;
        #pragma warning disable 414
        private static NcTimer Invoker;
        #pragma warning restore 414
        private static void InvokerCallback (Object state)
        {
            NcBrainEvent brainEvent = new NcBrainEvent (NcBrainEventType.PERIODIC_GLEAN);
            NcBrain.SharedInstance.Enqueue (brainEvent);
        }

        public static void Start ()
        {
            if (NcBrain.ENABLED) {
                Invoker = new NcTimer ("NcContactGleaner", InvokerCallback, null, TimeSpan.Zero, new TimeSpan (0, 0, 10));
                Invoker.Stfu = true;
            }
        }

        public static void Stop ()
        {
            if (NcBrain.ENABLED) {
                Invoker.Dispose ();
                Invoker = null;
            }
        }

        public NcContactGleaner ()
        {
        }

        protected static void MarkAsGleaned (McEmailMessage emailMessage)
        {
            emailMessage.HasBeenGleaned = true;
            NcModel.Instance.Db.Update (emailMessage);
        }

        /// TODO: I wanna be table driven!
        protected static bool DoNotGlean (string address)
        {
            if (MaxSaneAddressLength < address.Length) {
                return true;
            }
            if (address.Contains ("noreply")) {
                return true;
            }
            if (address.Contains ("no-reply")) {
                return true;
            }
            if (address.Contains ("donotreply")) {
                return true;
            }
            return false;
        }

        private static void GleanContact (MailboxAddress mbAddr, int accountId, McFolder gleanedFolder, McEmailMessage emailMessage)
        {
            // Don't glean when scrolling
            var contacts = McContact.QueryByEmailAddress (accountId, mbAddr.Address);
            if (0 == contacts.Count && !DoNotGlean (mbAddr.Address)) {
                // Create a new gleaned contact.
                var contact = new McContact () {
                    AccountId = accountId,
                    Source = McItem.ItemSource.Internal,
                    RefCount = 1,
                };
                // Try to parse the display name into first / middle / last name
                string[] items = mbAddr.Name.Split (new char [] { ',', ' ' });
                switch (items.Length) {
                case 2:
                    if (0 < mbAddr.Name.IndexOf (',')) {
                        // Last name, First name
                        contact.LastName = items [0];
                        contact.FirstName = items [1];
                    } else {
                        // First name, Last name
                        contact.FirstName = items [0];
                        contact.LastName = items [1];
                    }
                    break;
                case 3:
                    if (-1 == mbAddr.Name.IndexOf (',')) {
                        contact.FirstName = items [0];
                        contact.MiddleName = items [1];
                        contact.LastName = items [2];
                    }
                    break;
                }

                NcModel.Instance.Db.Insert (contact);
                gleanedFolder.Link (contact);

                var strattr = new McContactStringAttribute () {
                    Name = "Email1Address",
                    Value = mbAddr.Address,
                    Type = McContactStringType.EmailAddress,
                    ContactId = contact.Id,
                };

                // Update statistics
                contact.EmailsReceived += 1;
                if (emailMessage.IsRead) {
                    contact.EmailsReplied += 1;
                }
                // TODO - Get the reply state
                NcModel.Instance.Db.Insert (strattr);
            } else {
                // Update the refcount on the existing contact.
                foreach (var contact in contacts) {
                    // TODO: need update count using timestamp check.
                    contact.RefCount += 1;
                    NcModel.Instance.Db.Update (contact);
                }
            }
        }

        public static void GleanContacts (int accountId, McEmailMessage emailMessage)
        {
            var gleanedFolder = McFolder.GetGleanedFolder (accountId);
            if (null == gleanedFolder) {
                NachoCore.Utils.Log.Error (Log.LOG_BRAIN, "GleanContacts gleandedFolder is null for account id {0}", accountId);
                MarkAsGleaned (emailMessage);
                return;
            }
            if ((McBody.MIME == emailMessage.BodyType) && (McItem.BodyStateEnum.Whole_0 == emailMessage.BodyState)) {
                GleanContactsFromMime (accountId, gleanedFolder, emailMessage);
            } else {
                GleanContactsFromMcEmailMessage (accountId, gleanedFolder, emailMessage);
            }
            MarkAsGleaned (emailMessage);
        }

        public static void GleanContactsFromMcEmailMessage (int accountId, McFolder gleanedFolder, McEmailMessage emailMessage)
        {
            List<InternetAddressList> addrsLists = new List<InternetAddressList> ();
            if (null != emailMessage.To) {
                addrsLists.Add (NcEmailAddress.ParseString (emailMessage.To));
            }
            if (null != emailMessage.From) {
                addrsLists.Add (NcEmailAddress.ParseString (emailMessage.From));
            }
            if (null != emailMessage.Cc) {
                addrsLists.Add (NcEmailAddress.ParseString (emailMessage.Cc));
            }
            if (null != emailMessage.ReplyTo) {
                addrsLists.Add (NcEmailAddress.ParseString (emailMessage.ReplyTo));
            }
            if (null != emailMessage.Sender) {
                addrsLists.Add (NcEmailAddress.ParseString (emailMessage.Sender));
            }
            foreach (var addrsList in addrsLists) {
                foreach (var addr in addrsList) {
                    if (addr is MailboxAddress) {
                        GleanContact (addr as MailboxAddress, accountId, gleanedFolder, emailMessage);
                    }
                }
            }
        }

        public static void GleanContactsFromMime (int accountId, McFolder gleanedFolder, McEmailMessage emailMessage)
        {
            var path = emailMessage.GetBodyPath ();
            if (null == path) {
                MarkAsGleaned (emailMessage);
                return;
            }
            using (var fileStream = new FileStream (path, FileMode.Open, FileAccess.Read)) {
                MimeMessage mimeMsg;
                try {
                    var mimeParser = new MimeParser (fileStream, true);
                    mimeMsg = mimeParser.ParseMessage ();
                } catch (Exception e) {
                    // TODO: Find root cause
                    MarkAsGleaned (emailMessage);
                    NachoCore.Utils.Log.Error (Log.LOG_BRAIN, "GleanContactsFromMime exception ignored:\n{0}", e);
                    return;
                }
                List<InternetAddressList> addrsLists = new List<InternetAddressList> ();
                if (null != mimeMsg.To) {
                    addrsLists.Add (mimeMsg.To);
                }
                if (null != mimeMsg.From) {
                    addrsLists.Add (mimeMsg.From);
                }
                if (null != mimeMsg.Cc) {
                    addrsLists.Add (mimeMsg.Cc);
                }
                if (null != mimeMsg.Bcc) {
                    addrsLists.Add (mimeMsg.Bcc);
                }
                if (null != mimeMsg.ReplyTo) {
                    addrsLists.Add (mimeMsg.ReplyTo);
                }
                if (null != mimeMsg.Sender) {
                    var senderAsList = new InternetAddressList ();
                    senderAsList.Add (mimeMsg.Sender);
                    addrsLists.Add (senderAsList);
                }
                if (null != mimeMsg.MessageId) {
                    emailMessage.MessageID = mimeMsg.MessageId;
                }
                if (null != mimeMsg.InReplyTo) {
                    emailMessage.InReplyTo = mimeMsg.InReplyTo;
                }
                if (null != mimeMsg.References) {
                    emailMessage.References = String.Join ("\n", mimeMsg.References.ToArray ());
                }
                foreach (var addrsList in addrsLists) {
                    foreach (var addr in addrsList) {
                        if (addr is MailboxAddress) {
                            GleanContact (addr as MailboxAddress, accountId, gleanedFolder, emailMessage);
                        }
                    }
                }
            }
        }
    }
}

