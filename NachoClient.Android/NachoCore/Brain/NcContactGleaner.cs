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
    public class NcGleaningInterruptedException : Exception
    {
        public NcGleaningInterruptedException ()
        {
        }
    }

    public delegate void NcContactGleanerAction (bool obeyAbatement);

    public class NcContactGleaner
    {
        public const int GLEAN_PERIOD = 5;
        private const uint MaxSaneAddressLength = 40;

        public NcContactGleaner ()
        {
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
            if (address.Contains ("do_not_reply")) {
                return true;
            }
            return false;
        }

        protected static void CreateGleanContact (MailboxAddress mbAddr, int accountId, McFolder gleanedFolder)
        {
            if (DoNotGlean (mbAddr.Address)) {
                return;
            }
            if (null == gleanedFolder) {
                Log.Warn (Log.LOG_BRAIN, "gleaning folder is null");
                return;
            }
            var gleanedContact = new McContact () {
                AccountId = accountId,
                Source = McAbstrItem.ItemSource.Internal,
            };

            if (null == gleanedContact.AddEmailAddressAttribute (accountId, "Email1Address", null, mbAddr.Address)) {
                return;
            }
            NcEmailAddress.ParseName (mbAddr, ref gleanedContact);
            if (mbAddr.Address == mbAddr.Name) {
                // Some mail clients generate email addresses like "bob@company.net <bob@company.net>"
                // And it creates a first name of "Bob@company.Net". This partially breaks eclipsing
                // because the eclipsing algorithm considers this a different first name (from Bob)
                // and a gleaned contact like this will not be eclipsed.
                //
                // Having the email address as the first name is really the same as not have a first name.
                // We clear the first name in this case so that anonymous eclipsing will consolidate
                // this with "Bob <bob@company.net" (if it exists.)
                gleanedContact.FirstName = null;
            }

            NcModel.Instance.RunInTransaction (() => {
                // Check if the contact is a duplicate
                var isDup = false;
                var contactList = McContact.QueryGleanedContactsByEmailAddress (accountId, mbAddr.Address);
                foreach (var contact in contactList) {
                    if (gleanedContact.HasSameName (contact)) {
                        isDup = true;
                        break;
                    }
                }
                if (!isDup) {
                    gleanedContact.Insert ();
                    gleanedFolder.Link (gleanedContact);
                }
            });
        }

        public static void GleanContacts (string address, int accountId, bool obeyAbatement)
        {
            if (String.IsNullOrEmpty (address)) {
                return;
            }
            var addressList = NcEmailAddress.ParseAddressListString (address);
            var gleanedFolder = McFolder.GetGleanedFolder (accountId);
            foreach (var mbAddr in addressList) {
                if (obeyAbatement) {
                    NcAbate.PauseWhileAbated ();
                }
                if (mbAddr is MailboxAddress) {
                    CreateGleanContact ((MailboxAddress)mbAddr, accountId, gleanedFolder);
                }
            }
        }

        private static NcDisqualifier<McEmailMessage> marketingDisqualifier = new NcMarketingEmailDisqualifier ();
        private static NcDisqualifier<McEmailMessage> yahooDisqualifier = new NcYahooBulkEmailDisqualifier ();

        protected static bool CheckDisqualification (McEmailMessage emailMessage)
        {
            return marketingDisqualifier.Analyze (emailMessage) || yahooDisqualifier.Analyze (emailMessage);
        }

        // Do not glean junk or marketing emails because they are usually junk
        private static bool DoNotGlean (McEmailMessage emailMessage)
        {
            if (emailMessage.IsJunk) {
                return true;
            }
            if (emailMessage.HeadersFiltered) {
                return true;
            }
            if (CheckDisqualification (emailMessage)) {
                return true;
            }
            return false;
        }

        public static void GleanContactsHeaderPart1 (McEmailMessage emailMessage)
        {
            if (!DoNotGlean(emailMessage)) {
                GleanContacts (emailMessage.To, emailMessage.AccountId, false);
                GleanContacts (emailMessage.From, emailMessage.AccountId, false);
            }
            emailMessage.MarkAsGleaned (McEmailMessage.GleanPhaseEnum.GLEAN_PHASE1);
        }

        public static void GleanContactsHeader (McEmailMessage emailMessage)
        {
            if ((int)McEmailMessage.GleanPhaseEnum.GLEAN_PHASE2 > emailMessage.HasBeenGleaned) {
                if (!DoNotGlean (emailMessage)) {
                    int accountId = emailMessage.AccountId;
                    if ((int)McEmailMessage.GleanPhaseEnum.GLEAN_PHASE1 > emailMessage.HasBeenGleaned) {
                        GleanContacts (emailMessage.To, accountId, true);
                        GleanContacts (emailMessage.From, accountId, true);
                    }
                    GleanContacts (emailMessage.Cc, accountId, true);
                    GleanContacts (emailMessage.ReplyTo, accountId, true);
                    GleanContacts (emailMessage.Sender, accountId, true);
                }
                emailMessage.MarkAsGleaned (McEmailMessage.GleanPhaseEnum.GLEAN_PHASE2);
            }
        }
    }
}
