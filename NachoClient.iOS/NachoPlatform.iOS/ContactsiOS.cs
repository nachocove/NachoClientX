//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
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
            public override string UniqueId { get { return person.Id.ToString (); } }
            public override DateTime LastUpdate { get { return person.ModificationDate.ToDateTime (); } }

            public override NcResult ToMcContact ()
            {
                var contact = new McContact () {
                    Source = McAbstrItem.ItemSource.Device,
                    ServerId = "PlatformContactRecord" + UniqueId,
                    AccountId = ConstMcAccount.NotAccountSpecific.Id,
                };
                contact.FirstName = person.FirstName;
                contact.LastName = person.LastName;
                contact.MiddleName = person.MiddleName;
                // TODO: ignoring person.Prefix.
                contact.Suffix = person.Suffix;
                contact.NickName = person.Nickname;
                contact.YomiFirstName = person.FirstNamePhonetic;
                contact.YomiLastName = person.LastNamePhonetic;
                // Ignoring person.MiddleNamePhonetic.
                contact.CompanyName = person.Organization;
                contact.Title = person.JobTitle;
                contact.Department = person.Department;
                var emails = person.GetEmails ();
                int i = 1;
                foreach (var email in emails) {
                    contact.AddEmailAddressAttribute (string.Format ("Email{0}Address", i), null, email.Value);
                    ++i;
                }
                var birthday = person.Birthday;
                if (null != birthday) {
                    contact.AddDateAttribute ("Birthday", null, birthday.ToDateTime ());
                }
                // TODO: ignoring person.Note.
                // FIXME person.CreationDate;
                // FIXME person.GetPhones()
                return NcResult.OK (contact);
            }

            // Not to be referenced from platform independent code.
            public ABPerson person { get; set; }
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
                        });
                    }
                    // peeps [0].Id;
                    // peeps [0].ModificationDate;
                    // peeps [0].CreationDate;
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

