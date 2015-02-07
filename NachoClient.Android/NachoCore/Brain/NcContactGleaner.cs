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

    public class NcContactGleaner
    {
        public const int GLEAN_PERIOD = 10;
        private const uint MaxSaneAddressLength = 40;

        private static object LockObj = new object ();

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

        protected static void MarkAsGleaned (McEmailMessage emailMessage)
        {
            emailMessage.HasBeenGleaned = true;
            emailMessage.Update ();
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
            lock (LockObj) {
                var gleanedContact = new McContact () {
                    AccountId = accountId,
                    Source = McAbstrItem.ItemSource.Internal,
                };
                NcEmailAddress.SplitName (mbAddr, ref gleanedContact);

                // Check if the contact is a duplicate
                var contactList = McContact.QueryGleanedContactsByEmailAddress (accountId, mbAddr.Address);
                foreach (var contact in contactList) {
                    if (gleanedContact.HasSameName (contact)) {
                        return; // this gleaned contact already exists
                    }
                }

                NcModel.Instance.Db.RunInTransaction (() => {
                    gleanedContact.Insert ();
                    gleanedFolder.Link (gleanedContact);

                    McEmailAddress emailAddress;
                    McEmailAddress.Get (accountId, mbAddr, out emailAddress);

                    var strattr = new McContactEmailAddressAttribute () {
                        AccountId = accountId,
                        Name = "Email1Address",
                        Value = mbAddr.Address,
                        ContactId = gleanedContact.Id,
                        EmailAddress = emailAddress.Id,
                    };
                    strattr.Insert ();
                });
            }
        }


        public static void GleanContacts (string address, int accountId)
        {
            if (String.IsNullOrEmpty (address)) {
                return;
            }
            var addressList = NcEmailAddress.ParseAddressListString (address);
            var gleanedFolder = McFolder.GetGleanedFolder (accountId);
            NcModel.Instance.Db.RunInTransaction (() => {
                foreach (var mbAddr in addressList) {
                    CreateGleanContact ((MailboxAddress)mbAddr, accountId, gleanedFolder);
                }
            });
        }

        protected static bool InterruptibleGleaning (Action action, bool enforceAbatement)
        {
            bool gleaned = false;
            do {
                try {
                    action ();
                    gleaned = true;
                } catch (NcGleaningInterruptedException) {
                    if (enforceAbatement) {
                        break;
                    } else {
                        if (!NcTask.CancelableSleep (100)) {
                            NcTask.Cts.Token.ThrowIfCancellationRequested ();
                        }
                    }
                }
            } while (!enforceAbatement && !gleaned);
            return gleaned;
        }

        public static bool GleanContactsHeaderPart1 (McEmailMessage emailMessage)
        {
            return InterruptibleGleaning (() => {
                GleanContacts (emailMessage.To, emailMessage.AccountId);
                GleanContacts (emailMessage.From, emailMessage.AccountId);
            }, false);
        }

        public static bool GleanContactsHeaderPart2 (McEmailMessage emailMessage)
        {
            bool gleaned = InterruptibleGleaning (() => {
                GleanContacts (emailMessage.Cc, emailMessage.AccountId);
                GleanContacts (emailMessage.ReplyTo, emailMessage.AccountId);
                GleanContacts (emailMessage.Sender, emailMessage.AccountId);
            }, true);
            if (gleaned) {
                MarkAsGleaned (emailMessage);
            }
            return gleaned;
        }
    }
}

