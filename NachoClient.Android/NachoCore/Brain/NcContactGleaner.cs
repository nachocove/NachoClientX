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
        public const int GLEAN_PERIOD = 4;
        private const uint MaxSaneAddressLength = 40;

        private static NcTimer Invoker;
        private static void InvokerCallback (Object state)
        {
            if (NcApplication.ExecutionContextEnum.Background != NcApplication.Instance.ExecutionContext &&
                NcApplication.ExecutionContextEnum.Foreground != NcApplication.Instance.ExecutionContext) {
                // TODO - This is a temporary solution. We should not process any event other 
                return;
            }
            try {
                NcBrain.SharedInstance.EnqueueIfNotAlreadyThere (new NcBrainEvent (NcBrainEventType.PERIODIC_GLEAN));
            } catch (OperationCanceledException) {
                // brain is no longer active. Shut ourselves down.
                Log.Error (Log.LOG_BRAIN, "NcContactGleaner tried to enqueue, but brain is not there.");
                Stop ();
            }
        }

        public static void Start ()
        {
            if (NcBrain.ENABLED) {
                if (!NcBrain.SharedInstance.IsCancelled ()) {
                    if (null != Invoker) {
                        Invoker.Dispose ();
                        Invoker = null;
                    }
                    Invoker = new NcTimer ("NcContactGleaner", InvokerCallback, null,
                        TimeSpan.Zero, new TimeSpan (0, 0, GLEAN_PERIOD));
                    Invoker.Stfu = true;
                }
            }
        }

        public static void Stop ()
        {
            if (NcBrain.ENABLED) {
                if (null != Invoker) {
                    Invoker.Dispose ();
                    Invoker = null;
                }
            }
        }

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
                if (NcApplication.Instance.IsBackgroundAbateRequired && obeyAbatement) {
                    throw new NcGleaningInterruptedException ();
                }
                if (mbAddr is MailboxAddress) {
                    CreateGleanContact ((MailboxAddress)mbAddr, accountId, gleanedFolder);
                }
            }
        }

        protected static bool InterruptibleGleaning (NcContactGleanerAction action, bool obeyAbatement)
        {
            try {
                action (obeyAbatement);
            } catch (NcGleaningInterruptedException) {
                return false;
            }
            return true;
        }

        protected static bool CheckDisqualification (McEmailMessage emailMessage)
        {
            var disqualifiers = new List<NcDisqualifier<McEmailMessage>> () {
                new NcMarketingEmailDisqualifier (),
                new NcYahooBulkEmailDisqualifier (),
            };
            foreach (var dq in disqualifiers) {
                if (dq.Analyze (emailMessage)) {
                    return true;
                }
            }
            return false;
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

        public static bool GleanContactsHeaderPart1 (McEmailMessage emailMessage)
        {
            bool gleaned;
            if (DoNotGlean (emailMessage)) {
                gleaned = true;
            } else {
                gleaned = InterruptibleGleaning ((obeyAbatement) => {
                    GleanContacts (emailMessage.To, emailMessage.AccountId, obeyAbatement);
                    GleanContacts (emailMessage.From, emailMessage.AccountId, obeyAbatement);
                }, false);
            }
            if (gleaned) {
                emailMessage.MarkAsGleaned (McEmailMessage.GleanPhaseEnum.GLEAN_PHASE1);
            }
            return gleaned;
        }

        public static bool GleanContactsHeaderPart2 (McEmailMessage emailMessage)
        {
            bool gleaned = false;
            if (DoNotGlean (emailMessage)) {
                gleaned = true;
            } else {
                gleaned = InterruptibleGleaning ((obeyAbatement) => {
                    GleanContacts (emailMessage.Cc, emailMessage.AccountId, obeyAbatement);
                    GleanContacts (emailMessage.ReplyTo, emailMessage.AccountId, obeyAbatement);
                    GleanContacts (emailMessage.Sender, emailMessage.AccountId, obeyAbatement);
                }, true);
            }
            if (gleaned) {
                emailMessage.MarkAsGleaned (McEmailMessage.GleanPhaseEnum.GLEAN_PHASE2);
            }
            return gleaned;
        }
    }
}

