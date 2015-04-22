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
        public const int GLEAN_PERIOD = 10;
        private const uint MaxSaneAddressLength = 40;

        #pragma warning disable 414
        private static NcTimer Invoker;
        #pragma warning restore 414
        private static void InvokerCallback (Object state)
        {
            if (NcApplication.ExecutionContextEnum.Background != NcApplication.Instance.ExecutionContext &&
                NcApplication.ExecutionContextEnum.Foreground != NcApplication.Instance.ExecutionContext) {
                // TODO - This is a temporary solution. We should not process any event other 
                return;
            }
            NcBrainEvent brainEvent = new NcBrainEvent (NcBrainEventType.PERIODIC_GLEAN);
            NcBrain.SharedInstance.Enqueue (brainEvent);
        }

        public static void Start ()
        {
            if (NcBrain.ENABLED) {
                Invoker = new NcTimer ("NcContactGleaner", InvokerCallback, null,
                    TimeSpan.Zero, new TimeSpan (0, 0, GLEAN_PERIOD));
                Invoker.Stfu = true;
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

            gleanedContact.AddEmailAddressAttribute (accountId, "Email1Address", null, mbAddr.Address);
            NcEmailAddress.SplitName (mbAddr, ref gleanedContact);

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
                CreateGleanContact ((MailboxAddress)mbAddr, accountId, gleanedFolder);
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

        public static bool GleanContactsHeaderPart1 (McEmailMessage emailMessage)
        {
            // Caller is responsible for making sure that this is not in a junk folder.
            // We do not check here in order to avoid a lot of db queries just for
            // gleaning.
            bool gleaned = InterruptibleGleaning ((obeyAbatement) => {
                GleanContacts (emailMessage.To, emailMessage.AccountId, obeyAbatement);
                GleanContacts (emailMessage.From, emailMessage.AccountId, obeyAbatement);
            }, false);
            if (gleaned) {
                emailMessage.MarkAsGleaned (McEmailMessage.GleanPhaseEnum.GLEAN_PHASE1);
            }
            return gleaned;
        }

        public static bool GleanContactsHeaderPart2 (McEmailMessage emailMessage)
        {
            // McEmailMessage.QueryNeedGleaning() should filter out all ungleaned emails
            // in any of the junk folders. So, we don't do the junk folder check again
            // because it costs an additional query (on McMapFolderFolderEntry) per email.
            bool gleaned = InterruptibleGleaning ((obeyAbatement) => {
                GleanContacts (emailMessage.Cc, emailMessage.AccountId, obeyAbatement);
                GleanContacts (emailMessage.ReplyTo, emailMessage.AccountId, obeyAbatement);
                GleanContacts (emailMessage.Sender, emailMessage.AccountId, obeyAbatement);
            }, true);
            if (gleaned) {
                emailMessage.MarkAsGleaned (McEmailMessage.GleanPhaseEnum.GLEAN_PHASE2);
            }
            return gleaned;
        }
    }
}

