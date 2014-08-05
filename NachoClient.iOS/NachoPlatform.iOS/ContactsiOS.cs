//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoTouch.Foundation;
using MonoTouch.AddressBook;
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
                        if (instance == null)
                            instance = new Contacts ();
                    }
                }
                return instance;
            }
        }

        public class PlatformContactRecordiOS : PlatformContactRecord
        {
            public override string UniqueId { get { 
                    return Person.Id.ToString (); 
                } 
            }
            public override DateTime LastUpdate { get {
                    return Person.ModificationDate.ToDateTime ();
                }
            }
            // Person not to be referenced from platform independent code.
            public ABPerson Person { get; set; }

            public override NcResult ToMcContact ()
            {
                var contact = new McContact () {
                    Source = McAbstrItem.ItemSource.Device,
                    ServerId = "NachoDeviceContact:" + UniqueId,
                    AccountId = McAccount.QueryByAccountType(McAccount.AccountTypeEnum.Device).Single ().Id,
                    OwnerEpoch = SchemaRev,
                };
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
                    contact.AddEmailAddressAttribute (string.Format ("Email{0}Address", i), null, email.Value);
                    ++i;
                }
                var birthday = Person.Birthday;
                if (null != birthday) {
                    contact.AddDateAttribute ("Birthday", null, birthday.ToDateTime ());
                }
                if (null != Person.Note) {
                    var body = McBody.Save (Person.Note);
                    contact.BodyId = body.Id;
                    contact.BodyType = McBody.PlainText;
                }
                var phones = Person.GetPhones ();
                foreach (var phone in phones) {
                    var phoneLabel = phone.Label.ToString ();
                    if (phoneLabel.Contains ("Work")) {
                        contact.AddPhoneNumberAttribute ("BusinessPhoneNumber", "Work", phone.Value);
                    } else if (phoneLabel.Contains ("Home")) {
                        contact.AddPhoneNumberAttribute ("HomePhoneNumber", "Home", phone.Value);
                    } else {
                        // Guess mobile.
                        contact.AddPhoneNumberAttribute ("MobilePhoneNumber", null, phone.Value);
                    }
                }
                contact.DeviceCreation = Person.CreationDate.ToDateTime ();
                contact.DeviceLastUpdate = LastUpdate;
                contact.DeviceUniqueId = UniqueId;

                if (Person.HasImage) {
                    var data = Person.GetImage (ABPersonImageFormat.OriginalSize);
                    var portrait = McPortrait.Save (data.ToArray ());
                    contact.PortraitId = portrait.Id;
                }
                // TODO: Street addresses, IM addresses, etc.

                return NcResult.OK (contact);
            }
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
            foreach (var source in sources) {
                switch (source.SourceType) {
                case ABSourceType.Exchange:
                    continue;
                default:
                    Log.Info (Log.LOG_SYS, "Processing source {0}", source.SourceType);
                    var peeps = ab.GetPeople (source);
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

        private static string GetNSErrorString (NSError nsError)
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

