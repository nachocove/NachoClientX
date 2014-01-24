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
        private static Timer Invoker;
        #pragma warning restore 414
        private static void InvokerCallback (Object state)
        {
            var nextMsg = BackEnd.Instance.Db.Table<McEmailMessage> ().FirstOrDefault (x => x.HasBeenGleaned == false);
            if (null == nextMsg) {
                return;
            }
            GleanContacts (nextMsg.AccountId, nextMsg);
        }

        public static void Start ()
        {
            Invoker = new Timer (InvokerCallback, null, TimeSpan.Zero, new TimeSpan (0, 0, 1));
        }

        public NcContactGleaner ()
        {
        }

        public static void GleanContacts (int accountId, McEmailMessage emailMessage)
        {
            if (0 == emailMessage.BodyId) {
                // Mark the email message as gleaned.
                emailMessage.HasBeenGleaned = true;
                BackEnd.Instance.Db.Update (emailMessage);
                return;
            }
            var bodySource = new MemoryStream (Encoding.UTF8.GetBytes (emailMessage.GetBody (BackEnd.Instance.Db)));
            var bodyParser = new MimeParser (bodySource, MimeFormat.Default);
            MimeMessage mimeMsg;
            try {
                mimeMsg = bodyParser.ParseMessage ();
            } catch (Exception e) {
                // TODO: Find root cause
                // Mark the email message as gleaned.
                emailMessage.HasBeenGleaned = true;
                BackEnd.Instance.Db.Update (emailMessage);
                NachoCore.Utils.Log.Error ("GleanContacts exception ignored:\n{0}", e);
                return;
            }
            var gleanedFolder = BackEnd.Instance.GetGleaned (accountId);
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
            foreach (var addrsList in addrsLists) {
                foreach (var addr in addrsList) {
                    if (addr is MailboxAddress) {
                        MailboxAddress mbAddr = addr as MailboxAddress;
                        var contacts = McContact.QueryByEmailAddress (accountId, mbAddr.Address);
                        if (0 == contacts.Count &&
                            MaxSaneAddressLength >= mbAddr.Address.Length &&
                            !mbAddr.Address.Contains ("noreply") &&
                            !mbAddr.Address.Contains ("no-reply") &&
                            !mbAddr.Address.Contains ("donotreply")) {
                            // Create a new gleaned contact.
                            var contact = new McContact () {
                                AccountId = accountId,
                                Source = McContact.McContactSource.Internal,
                                FolderId = gleanedFolder.Id,
                                RefCount = 1,
                            };
                            BackEnd.Instance.Db.Insert (contact);
                            var strattr = new McContactStringAttribute () {
                                Name = "Email1Address",
                                Value = mbAddr.Address,
                                Type = McContactStringType.EmailAddress,
                                ContactId = contact.Id,
                            };
                            BackEnd.Instance.Db.Insert (strattr);
                        } else {
                            // Update the refcount on the existing contact.
                            foreach (var contact in contacts) {
                                // TODO: need update count using timestamp check.
                                contact.RefCount += 1;
                                BackEnd.Instance.Db.Update (contact);
                            }
                        }
                    }
                }
            }
            // As long as we have the data in memory (sorry!)
            if (null == emailMessage.Summary) {
                emailMessage.Summary = MimeHelpers.CreateSummary (mimeMsg);
            }
            // Mark the email message as gleaned.
            emailMessage.HasBeenGleaned = true;
            BackEnd.Instance.Db.Update (emailMessage);
        }
    }
}

