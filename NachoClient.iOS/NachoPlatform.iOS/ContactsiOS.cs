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
        public const int SchemaRev = 0;
        private static volatile Contacts instance;
        private static object syncRoot = new Object ();
        private static object AbLockObj = new Object ();
        private ABAddressBook Ab;

        public event EventHandler ChangeIndicator;

        private Contacts ()
        {
            ABAddressBookCreate ();
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

        private void Dispatch (object sender, EventArgs ea)
        {
            if (null != ChangeIndicator) {
                ChangeIndicator (this, ea);
            }
        }

        public bool AuthorizationStatus { get { 
                return ABAuthorizationStatus.Authorized == ABAddressBook.GetAuthorizationStatus (); 
            } }

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

        private void ABAddressBookCreate ()
        {
            NSError err; 
            Ab = ABAddressBook.Create (out err);
            if (null != err) {
                Log.Error (Log.LOG_SYS, "ABAddressBook.Create: {0}", GetNSErrorString (err));
                Ab = null;
            }
            // setup external change.
            if (null != Ab) {
                Ab.ExternalChange += Dispatch;
            }
        }

        public void AskForPermission (Action<bool> result)
        {
            ABAddressBookCreate ();
            if (null == Ab) {
                result (false);
                return;
            }
            Ab.RequestAccess ((granted, reqErr) => {
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
            var retval = new List<PlatformContactRecordiOS> ();
            lock (AbLockObj) {
                if (ABAddressBook.GetAuthorizationStatus () != ABAuthorizationStatus.Authorized) {
                    Log.Warn (Log.LOG_SYS, "GetContacts: not Authorized: {0}", ABAddressBook.GetAuthorizationStatus ());
                    NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                        Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_NeedContactsPermission),
                        Account = ConstMcAccount.NotAccountSpecific,
                    });
                    return null;
                }
                if (null == Ab) {
                    return null;
                }
                var sources = Ab.GetAllSources ();
                Log.Info (Log.LOG_SYS, "GetContacts: Processing {0} sources", sources.Length);
                var cancellationToken = NcTask.Cts.Token;
                foreach (var source in sources) {
                    cancellationToken.ThrowIfCancellationRequested ();
                    NcAbate.PauseWhileAbated ();
                    switch (source.SourceType) {
                    // FIXME - exclude only those sources we cover in EAS.
                    case ABSourceType.Exchange:
                    case ABSourceType.ExchangeGAL:
                        continue;
                    default:
                        var peeps = Ab.GetPeople (source);
                        Log.Info (Log.LOG_SYS, "GetContacts: Processing source {0} with {1} contacts", source.SourceType, peeps.Length);
                        foreach (var peep in peeps) {
                            cancellationToken.ThrowIfCancellationRequested ();
                            retval.Add (new PlatformContactRecordiOS () {
                                Person = peep,
                            });
                        }
                        break;
                    }
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
                sb.AppendFormat (" Description: {0}", nsError.LocalizedDescription);
                var userInfo = nsError.UserInfo;
                for (int i = 0; i < userInfo.Keys.Length; i++) {
                    sb.AppendFormat ("[{0}]: {1}\r\n", userInfo.Keys [i].ToString (), userInfo.Values [i].ToString ());
                }
                return sb.ToString ();
            } catch {
                return "";
            }
        }

        public NcResult Add (McContact contact)
        {
            try {
                var person = new ABPerson ();
                person.FirstName = contact.FirstName;
                person.LastName = contact.LastName;
                person.MiddleName = contact.MiddleName;
                // FIXME DAVID - need full translator here.
                lock (AbLockObj) {
                    Ab.Add (person);
                    Ab.Save ();
                }
                contact.ServerId = person.Id.ToString ();
                contact.IsAwaitingCreate = false;
                contact.Update ();
                return NcResult.OK ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "Contacts.Add: {0}", ex.ToString ());
                return NcResult.Error ("Contacts.Add");
            }
        }

        public NcResult Delete (string serverId)
        {
            try {
                lock (AbLockObj) {
                    Ab.Revert ();
                    var dead = Ab.GetPerson (int.Parse (serverId));
                    if (null == dead) {
                        return NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    }
                    Ab.Remove (dead);
                    Ab.Save ();
                }
                return NcResult.OK ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "Contacts.Delete: {0}", ex.ToString ());
                return NcResult.Error ("Contacts.Delete");
            }
        }

        public NcResult Change (McContact contact)
        {
            try {
                lock (AbLockObj) {
                    Ab.Revert ();
                    var changed = Ab.GetPerson (int.Parse (contact.ServerId));
                    if (null == changed) {
                        return NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    }
                    if (null == contact.FirstName) {
                        changed.FirstName = null;
                    } else {
                        changed.FirstName = contact.FirstName;
                    }
                    if (null == contact.LastName) {
                        changed.LastName = null;
                    } else {
                        changed.LastName = contact.LastName;
                    }
                    // FIXME DAVID translator needed.
                    Ab.Save ();
                }
                return NcResult.OK ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "Contacts.Change: {0}", ex.ToString ());
                return NcResult.Error ("Contacts.Change");
            }
        }
    }

    public class PlatformContactRecordiOS : PlatformContactRecord
    {
        public override string ServerId { get { return Person.Id.ToString (); } }

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
                    ServerId = ServerId,
                    AccountId = accountId,
                    OwnerEpoch = Contacts.SchemaRev,
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

            contact.EmailAddresses = new List<McContactEmailAddressAttribute> ();
            var emails = Person.GetEmails ();
            int i = 1;
            if (null != emails) {
                foreach (var email in emails) {
                    // Check if the email address string is valid. iOS contact email address are not
                    // guaranteed to be RFC compliant.
                    var emailAddresses = NcEmailAddress.ParseAddressListString (email.Value);
                    if (null == emailAddresses) {
                        Log.Error (Log.LOG_SYS, "NcEmailAddress.ParseAddressListString returned null");
                        continue;
                    }
                    if (1 != emailAddresses.Count) {
                        Log.Warn (Log.LOG_SYS, "Cannot import invalid email addresses (count={0})", emailAddresses.Count);
                        continue;
                    }
                    contact.AddEmailAddressAttribute (accountId, string.Format ("Email{0}Address", i), null, email.Value);
                    ++i;
                }
            }

            contact.Dates.RemoveAll ((x) => {
                return x.Name == "Birthday";
            });
            var birthday = Person.Birthday;
            if (null != birthday) {
                contact.AddDateAttribute (accountId, "Birthday", null, birthday.ToDateTime ());
            }

            if (null != Person.Note) {
                McBody body = null;
                if (0 != contact.BodyId) {
                    body = McBody.QueryById<McBody> (contact.BodyId);
                }
                if (null == body) {
                    body = McBody.InsertFile (accountId, McAbstrFileDesc.BodyTypeEnum.PlainText_1, Person.Note);
                } else {
                    body.UpdateData (Person.Note);
                }
                contact.BodyId = body.Id;
            }

            contact.PhoneNumbers = new List<McContactStringAttribute> ();
            var phones = Person.GetPhones ();
            if (null != phones) {
                foreach (var phone in phones) {
                    if (null != phone.Value) {
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
                }
            }
            if (null != Person.CreationDate) {
                contact.DeviceCreation = Person.CreationDate.ToDateTime ();
            }
            contact.DeviceLastUpdate = LastUpdate;
            contact.ServerId = ServerId;

            if (Person.HasImage) {
                var data = Person.GetImage (ABPersonImageFormat.OriginalSize);
                if (null != data) {
                    McPortrait portrait = null;
                    if (0 != contact.PortraitId) {
                        portrait = McPortrait.QueryById<McPortrait> (contact.PortraitId);
                    }
                    if (null == portrait) {
                        portrait = McPortrait.InsertFile (accountId, data.ToArray ());
                    } else {
                        portrait.UpdateData (data.ToArray ());
                    }
                    contact.PortraitId = portrait.Id;
                }
            }
            // TODO: Street addresses, IM addresses, etc.

            return NcResult.OK (contact);
        }
    }
}
