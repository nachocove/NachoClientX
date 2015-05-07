//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Foundation;
using AddressBook;
using MimeKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoPlatform
{
    public sealed class Contacts : IPlatformContacts
    {
        private const int SchemaRev = 0;
        private static volatile Contacts instance;
        private static object syncRoot = new Object ();

        private Contacts ()
        {
        }

        public static Contacts Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new Contacts ();
                        }
                    }
                }
                return instance;
            }
        }

        public class PlatformContactRecordiOS : PlatformContactRecord
        {
            public override string UniqueId { get { return Person.Id.ToString (); } }

            public override DateTime LastUpdate { get { return Person.ModificationDate.ToDateTime (); } }
            // Person not to be referenced from platform independent code.
            public ABPerson Person { get; set; }

            public override NcResult ToMcContact (McContact contactToUpdate)
            {
                var accountId = McAccount.GetDeviceAccount ().Id;
                McContact contact;
                if (null == contactToUpdate) {
                    contact = new McContact () {
                        Source = McAbstrItem.ItemSource.Device,
                        ServerId = "NachoDeviceContact:" + UniqueId,
                        AccountId = accountId,
                        OwnerEpoch = SchemaRev,
                    };
                } else {
                    contact = contactToUpdate;
                }
                contact.FirstName = Person.FirstName;
                contact.LastName = Person.LastName;
                contact.MiddleName = Person.MiddleName;
                contact.Suffix = Person.Suffix;
                contact.NickName = Person.Nickname;
                contact.YomiFirstName = Person.FirstNamePhonetic;
                contact.YomiLastName = Person.LastNamePhonetic;
                contact.CompanyName = Person.Organization;
                contact.Title = Person.JobTitle;
                contact.Department = Person.Department;
                var emails = Person.GetEmails ();
                int i = 1;
                foreach (var email in emails) {
                    // Check if the email address string is valid. iOS contact email address are not
                    // guaranteed to be RFC compliant.
                    var emailAddresses = NcEmailAddress.ParseAddressListString (email.Value);
                    if (1 != emailAddresses.Count) {
                        Log.Warn (Log.LOG_SYS, "Cannot import invalid email addresses (count={0})", emailAddresses.Count);
                        continue;
                    }
                    contact.AddEmailAddressAttribute (accountId, string.Format ("Email{0}Address", i), null, email.Value);
                    ++i;
                }
                var birthday = Person.Birthday;
                if (null != birthday) {
                    contact.AddDateAttribute (accountId, "Birthday", null, birthday.ToDateTime ());
                }
                if (null != Person.Note) {
                    var body = McBody.InsertFile (accountId, McAbstrFileDesc.BodyTypeEnum.PlainText_1, Person.Note);
                    contact.BodyId = body.Id;
                }
                var phones = Person.GetPhones ();
                foreach (var phone in phones) {
                    var phoneLabel = (null == phone.Label) ? "" : phone.Label.ToString ();
                    if (phoneLabel.Contains ("Work")) {
                        contact.AddPhoneNumberAttribute (accountId, "BusinessPhoneNumber", "Work", phone.Value);
                    } else if (phoneLabel.Contains ("Home")) {
                        contact.AddPhoneNumberAttribute (accountId, "HomePhoneNumber", "Home", phone.Value);
                    } else {
                        // Guess mobile.
                        contact.AddPhoneNumberAttribute (accountId, "MobilePhoneNumber", null, phone.Value);
                    }
                }
                contact.DeviceCreation = Person.CreationDate.ToDateTime ();
                contact.DeviceLastUpdate = LastUpdate;
                contact.DeviceUniqueId = UniqueId;

                if (Person.HasImage) {
                    var data = Person.GetImage (ABPersonImageFormat.OriginalSize);
                    var portrait = McPortrait.InsertFile (accountId, data.ToArray ());
                    contact.PortraitId = portrait.Id;
                }
                // TODO: Street addresses, IM addresses, etc.

                return NcResult.OK (contact);
            }
        }

        public bool ShouldWeBotherToAsk ()
        {
            if (ABAuthorizationStatus.NotDetermined == ABAddressBook.GetAuthorizationStatus ()) {
                return true;
            }
            // ABAuthorizationStatus.Authorized -- The user already said yes
            // ABAuthorizationStatus.Denied -- The user already said no
            // ABAuthorizationStatus.Restricted -- E.g. parental controls
            return false;
        }

        private ABAddressBook ABAddressBookCreate ()
        {
            NSError err; 
            var ab = ABAddressBook.Create (out err);
            if (null != err) {
                Log.Error (Log.LOG_SYS, "ABAddressBook.Create: {0}", GetNSErrorString (err));
                return null;
            }
            return ab;
        }

        public void AskForPermission (Action<bool> result)
        {
            var ab = ABAddressBookCreate ();
            if (null == ab) {
                result (false);
                return;
            }
            ab.RequestAccess ((granted, reqErr) => {
                if (null != reqErr) {
                    Log.Error (Log.LOG_SYS, "ABAddressBook.RequestAccess: {0}", GetNSErrorString (reqErr));
                    result (false);
                }
                if (granted) {
                    Log.Info (Log.LOG_SYS, "ABAddressBook.RequestAccess authorized.");
                } else {
                    Log.Info (Log.LOG_SYS, "ABAddressBook.RequestAccess not authorized.");
                }
                result (granted);
            });
        }

        public IEnumerable<PlatformContactRecord> GetContacts ()
        {
            if (ABAddressBook.GetAuthorizationStatus () != ABAuthorizationStatus.Authorized) {
                Log.Warn (Log.LOG_SYS, "GetContacts: not Authorized: {0}", ABAddressBook.GetAuthorizationStatus ());
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                    Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_NeedContactsPermission),
                    Account = ConstMcAccount.NotAccountSpecific,
                });
                return null;
            }
            var ab = ABAddressBookCreate ();
            if (null == ab) {
                return null;
            }
            var sources = ab.GetAllSources ();

            var retval = new List<PlatformContactRecordiOS> ();
            Log.Info (Log.LOG_SYS, "GetContacts: Processing {0} sources", sources.Length);
            foreach (var source in sources) {
                switch (source.SourceType) {
                case ABSourceType.Exchange:
                case ABSourceType.ExchangeGAL:
                    continue;
                default:
                    var peeps = ab.GetPeople (source);
                    Log.Info (Log.LOG_SYS, "GetContacts: Processing source {0} with {1} contacts", source.SourceType, peeps.Length);
                    foreach (var peep in peeps) {
                        retval.Add (new PlatformContactRecordiOS () {
                            Person = peep,
                        });
                    }
                    break;
                }
            }
            return retval;
        }

        // TODO: Move to a general ios file.
        public static string GetNSErrorString (NSError nsError)
        {
            try {
                StringBuilder sb = new StringBuilder ();
                sb.AppendFormat ("Error Code: {0}:", nsError.Code.ToString ());
                sb.AppendFormat ("Description: {0}", nsError.LocalizedDescription);
                var userInfo = nsError.UserInfo;
                for (int i = 0; i < userInfo.Keys.Length; i++) {
                    sb.AppendFormat ("[{0}]: {1}\r\n", userInfo.Keys [i].ToString (), userInfo.Values [i].ToString ());
                }
                return sb.ToString ();
            } catch {
                return "";     
            }
        }
    }
}

