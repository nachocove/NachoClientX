//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Provider;
using NachoClient.AndroidClient;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;
using MimeKit;

namespace NachoPlatform
{
    public sealed class Contacts : IPlatformContacts
    {
        private const int SchemaRev = 0;
        private static volatile Contacts instance;
        private static object syncRoot = new Object ();

        public event EventHandler ChangeIndicator;

        private Contacts ()
        {
            InvokeOnUIThread.Instance.Invoke (() => {
                MainApplication.Instance.ContentResolver.RegisterContentObserver (ContactsContract.Contacts.ContentUri, true, new NcContactContentObserver (this, new Handler ()));
            });
        }

        public class NcContactContentObserver : Android.Database.ContentObserver
        {
            Contacts Owner { get; set; }

            public NcContactContentObserver (Contacts owner, Handler handler) : base (handler)
            {
                Owner = owner;
            }

            public override void OnChange (bool selfChange)
            {
                if (Owner.ChangeIndicator != null) {
                    Owner.ChangeIndicator (this, new EventArgs ());
                }
            }
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

        public void AskForPermission (Action<bool> result)
        {
            // Should never be called on Android.
        }

        public IEnumerable<PlatformContactRecord> GetContacts ()
        {
            var deviceAccount = McAccount.GetDeviceAccount ();
            var retval = new List<PlatformContactRecordAndroid> ();

            Android.Database.ICursor cur;

            try {
                var cr = MainApplication.Instance.ContentResolver;
                var projection = new [] {
                    ContactsContract.Contacts.InterfaceConsts.Id,
                    ContactsContract.Contacts.InterfaceConsts.ContactLastUpdatedTimestamp,
                    ContactsContract.Contacts.InterfaceConsts.Starred,
                };
                cur = cr.Query (ContactsContract.Contacts.ContentUri, projection, null, null, null, null);
                NcAssert.NotNull (cur);
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "ContactsAndroid:GetContacts setup {0}", ex);
                return retval;
            }

            try {
                if (cur.Count > 0) {
                    while (cur.MoveToNext ()) {
                        try {
                            long id = GetFieldLong (cur, ContactsContract.Contacts.InterfaceConsts.Id);
                            String lastUpdateString = GetField (cur, ContactsContract.Contacts.InterfaceConsts.ContactLastUpdatedTimestamp);
                            var lastUpdate = FromUnixTimeMilliseconds (lastUpdateString, DateTime.UtcNow);
                            int starred = GetFieldInt (cur, ContactsContract.Contacts.InterfaceConsts.Starred);
                            var entry = new PlatformContactRecordAndroid (deviceAccount, id, lastUpdate, starred != 0);
                            retval.Add (entry);
                        } catch {
                            Log.Error (Log.LOG_SYS, "ContactsAndroid:GetContacts exception, record skipped");
                        }
                    }
                }
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "ContactsAndroid:GetContacts main loop {0}", ex);
            } finally {
                cur.Close ();
            }
            return retval;
        }

        public NcResult Add (McContact contact)
        {
            return NcResult.Error ("Android Contacts.Add not yet implemented.");
        }

        public NcResult Delete (string serverId)
        {
            return NcResult.Error ("Android Contacts.Delete not yet implemented.");
        }

        public NcResult Change (McContact contact)
        {
            return NcResult.Error ("Android Contacts.Change not yet implement.");
        }

        public bool AuthorizationStatus {
            get {
                // TODO Might need to do more here for api >= 23.
                return true;
            }
        }

        #region helper-functons

        public static string GetField (Android.Database.ICursor cur, string Column)
        {
            return cur.GetString (cur.GetColumnIndex (Column));
        }

        public static byte[] GetFieldByte (Android.Database.ICursor cur, string Column)
        {
            return cur.GetBlob (cur.GetColumnIndex (Column));
        }

        public static int GetFieldInt (Android.Database.ICursor cur, string Column)
        {
            return cur.GetInt (cur.GetColumnIndex (Column));
        }

        public static long GetFieldLong (Android.Database.ICursor cur, string Column)
        {
            return cur.GetLong (cur.GetColumnIndex (Column));
        }

        public static DateTime FromUnixTimeMilliseconds (string unixTimeStr, DateTime defaultValue)
        {
            try {
                return long.Parse (unixTimeStr).JavaMsToDateTime ();
            } catch (Exception) {
                return defaultValue;
            }
        }

        #endregion

        #region PlatformContactRecordAndroid

        public class PlatformContactRecordAndroid : PlatformContactRecord
        {
            McAccount Account;
            string _ServerId;

            public override string ServerId { get { return _ServerId; } }

            DateTime _LastUpdate;

            public override DateTime LastUpdate { get { return _LastUpdate; } }

            bool Starred;

            public PlatformContactRecordAndroid (McAccount account, long id, DateTime lastUpdate, bool starred) : base ()
            {
                Account = account;
                _ServerId = id.ToString ();
                _LastUpdate = lastUpdate;
                Starred = starred;
            }

            public override NcResult ToMcContact (McContact contactToUpdate)
            {
                McContact Contact;
                if (null == contactToUpdate) {
                    Contact = new McContact ();
                    Contact.AccountId = Account.Id;
                    Contact.ServerId = _ServerId;
                    Contact.Source = McAbstrItem.ItemSource.Device;
                    Contact.OwnerEpoch = Contacts.SchemaRev;
                    Contact.IsVip = Starred;
                } else {
                    if (Starred != contactToUpdate.IsVip) {
                        contactToUpdate.SetVIP (Starred);
                    }
                    Contact = contactToUpdate;
                }

                Contact.DeviceLastUpdate = _LastUpdate;

                new FromAndroidContent (Contact).Populate ();

                return NcResult.OK (Contact);
            }
        }

        #endregion

        #region FromAndroidContent

        public class FromAndroidContent
        {
            McContact Contact;

            public FromAndroidContent (McContact contact)
            {
                Contact = contact;
            }

            public void Populate ()
            {
                GetContactInfo ();
                GetContactNick ();
                GetContactAddress ();
                GetContactPhones ();
                GetContactEmails ();
                GetContactCompany ();
                GetContactBirthday ();
                GetContactNotes ();
                GetContactPhoto ();
            }

            protected void GetContactInfo ()
            {
                var pCur = MainApplication.Instance.ContentResolver.Query (ContactsContract.Data.ContentUri,
                               null, // FIXME Add projection for speed
                               ContactsContract.Data.InterfaceConsts.Mimetype + " = ? AND " + ContactsContract.CommonDataKinds.StructuredName.InterfaceConsts.ContactId + " = ?",
                               new [] { ContactsContract.CommonDataKinds.StructuredName.ContentItemType, Contact.ServerId },
                               ContactsContract.CommonDataKinds.StructuredName.GivenName);
                bool GotIt = false;
                if (pCur.MoveToFirst ()) {
                    do {
                        if (GotIt) {
                            Log.Warn (Log.LOG_SYS, "Contact has more than one name");
                        } else {
                            Contact.FirstName = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.GivenName);
                            Contact.LastName = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.FamilyName);
                            Contact.MiddleName = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.MiddleName);
                            Contact.YomiFirstName = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.PhoneticGivenName);
                            Contact.YomiLastName = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.PhoneticFamilyName);
                            Contact.Title = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.Prefix);
                            Contact.Suffix = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.Suffix);
                            GotIt = true;
                        }
                    } while (pCur.MoveToNext ());
                }
                pCur.Close ();
                if (!GotIt) {
                    Log.Error (Log.LOG_SYS, "No name found for contact");
                }
            }

            protected void GetContactNick ()
            {
                var pCur = MainApplication.Instance.ContentResolver.Query (ContactsContract.Data.ContentUri,
                               null, // FIXME Add projection for speed
                               ContactsContract.Data.InterfaceConsts.Mimetype + " = ? AND " + ContactsContract.CommonDataKinds.Nickname.InterfaceConsts.ContactId + " = ?",
                               new [] { ContactsContract.CommonDataKinds.Nickname.ContentItemType, Contact.ServerId },
                               null);
                var GotIt = false;
                if (pCur.MoveToFirst ()) {
                    do {
                        if (GotIt) {
                            Log.Warn (Log.LOG_SYS, "Contact has more than one nickname");
                        } else {
                            Contact.NickName = GetField (pCur, ContactsContract.CommonDataKinds.Nickname.Name);
                        }
                    } while (pCur.MoveToNext ());
                }
                pCur.Close ();
            }

            protected void GetContactCompany ()
            {
                var pCur = MainApplication.Instance.ContentResolver.Query (ContactsContract.Data.ContentUri,
                               null, // FIXME Add projection for speed
                               ContactsContract.Data.InterfaceConsts.Mimetype + " = ? AND " + ContactsContract.CommonDataKinds.Organization.InterfaceConsts.ContactId + " = ?",
                               new [] { ContactsContract.CommonDataKinds.Organization.ContentItemType, Contact.ServerId },
                               null);
                var GotIt = false;
                if (pCur.MoveToFirst ()) {
                    do {
                        if (GotIt) {
                            Log.Warn (Log.LOG_SYS, "Contact has more than one nickname");
                        } else {
                            Contact.CompanyName = GetField (pCur, ContactsContract.CommonDataKinds.Organization.Company);
                            Contact.YomiCompanyName = GetField (pCur, ContactsContract.CommonDataKinds.Organization.PhoneticName);
                            Contact.JobTitle = GetField (pCur, ContactsContract.CommonDataKinds.Organization.Title);
                            Contact.Department = GetField (pCur, ContactsContract.CommonDataKinds.Organization.Department);
                            GotIt = true;
                        }
                    } while (pCur.MoveToNext ());
                }
                pCur.Close ();
            }

            const int TYPE_BIRTHDAY = 3;
            // bug in xamarin. This isn't exposed.
            protected void GetContactBirthday ()
            {
                // Birthday (Not sure how to get this yet. All java examples use Event.TYPE_BIRTHDAY, but that doesn't seem to exist in C#
                Contact.Dates.RemoveAll ((x) => x.Name == "Birthday");
                var pCur = MainApplication.Instance.ContentResolver.Query (ContactsContract.Data.ContentUri,
                               null,
                               ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.ContactId + "=? AND " +
                               ContactsContract.CommonDataKinds.Event.InterfaceConsts.Mimetype + "=? AND " +
                               ContactsContract.CommonDataKinds.Event.InterfaceConsts.Type + "=" + TYPE_BIRTHDAY,
                               new []{ Contact.ServerId, ContactsContract.CommonDataKinds.Event.ContentItemType },
                               null);
                while (pCur.MoveToNext ()) {
                    var birthday = GetField (pCur, ContactsContract.CommonDataKinds.Event.StartDate);
                    try {
                        Contact.AddDateAttribute (Contact.AccountId, "Birthday", null, DateTime.Parse (birthday));
                    } catch (FormatException ex) {
                        // FIXME saw a birthday of "--03-18". Need to be able to handle this.
                        Log.Warn (Log.LOG_CONTACTS, "Badly formatted birthday: {0}", ex.Message);
                    }
                }
                pCur.Close ();
            }

            protected void GetContactAddress ()
            {
                var pCur = MainApplication.Instance.ContentResolver.Query (ContactsContract.Data.ContentUri, 
                               null, // FIXME Add projection for speed
                               ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.ContactId + "=? AND " +
                               ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.Mimetype + "=?",
                               new []{ Contact.ServerId, ContactsContract.CommonDataKinds.StructuredPostal.ContentItemType },
                               null);
                if (pCur.MoveToFirst ()) {
                    do {
                        AddressDataKind type = (AddressDataKind)GetFieldInt (pCur, ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.Type);
                        String label = GetField (pCur, ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.Label);
                        string addrType = ContactsContract.CommonDataKinds.StructuredPostal.GetTypeLabel (MainApplication.Instance.ApplicationContext.Resources, type, label);
                        var attr = new McContactAddressAttribute ();
                        attr.Street = GetField (pCur, ContactsContract.CommonDataKinds.StructuredPostal.Street);
                        attr.PostalCode = GetField (pCur, ContactsContract.CommonDataKinds.StructuredPostal.Postcode);
                        attr.City = GetField (pCur, ContactsContract.CommonDataKinds.StructuredPostal.City);
                        attr.Country = GetField (pCur, ContactsContract.CommonDataKinds.StructuredPostal.Country);
                        attr.State = GetField (pCur, ContactsContract.CommonDataKinds.StructuredPostal.Region);
                        Contact.AddAddressAttribute (Contact.AccountId, addrType, label, attr);
                    } while (pCur.MoveToNext ());
                }
                pCur.Close ();
            }

            protected void GetContactPhones ()
            {
                Contact.PhoneNumbers = new List<McContactStringAttribute> ();
                var pCur = MainApplication.Instance.ContentResolver.Query (
                               ContactsContract.CommonDataKinds.Phone.ContentUri,
                               null, 
                               ContactsContract.CommonDataKinds.Phone.InterfaceConsts.ContactId + " = ?",
                               new []{ Contact.ServerId }, null);
                if (pCur.MoveToFirst ()) {
                    do {
                        String phoneNo = GetField (pCur, ContactsContract.CommonDataKinds.Phone.Number);
                        PhoneDataKind type = (PhoneDataKind)GetFieldInt (pCur, ContactsContract.CommonDataKinds.Phone.InterfaceConsts.Type);
                        String phLabel = GetField (pCur, ContactsContract.CommonDataKinds.Phone.InterfaceConsts.Label);
                        string phoneType = ContactsContract.CommonDataKinds.Phone.GetTypeLabel (MainApplication.Instance.ApplicationContext.Resources, type, phLabel);
                        int starred = GetFieldInt (pCur, ContactsContract.CommonDataKinds.Phone.InterfaceConsts.Starred);
                        string name;
                        string label;
                        if (!string.IsNullOrEmpty (phoneType)) {
                            phoneType = phoneType.ToLowerInvariant ();
                            if (phoneType == "home") {
                                name = Xml.Contacts.HomePhoneNumber;
                                label = "Home";
                            } else if (phoneType == "work") {
                                name = Xml.Contacts.BusinessPhoneNumber;
                                label = "Work";
                            } else if (phoneType == "mobile") {
                                name = Xml.Contacts.MobilePhoneNumber;
                                label = null;
                            } else if (phoneType == "home fax") {
                                name = Xml.Contacts.HomeFaxNumber;
                                label = null;
                            } else if (phoneType == "work fax") {
                                name = Xml.Contacts.BusinessFaxNumber;
                                label = null;
                            } else {
                                // assume mobile
                                name = Xml.Contacts.MobilePhoneNumber;
                                label = null;
                            }
                        } else {
                            // assume mobile
                            name = Xml.Contacts.MobilePhoneNumber;
                            label = null;
                        }
                        if (starred == 0) {
                            Contact.AddPhoneNumberAttribute (Contact.AccountId, name, label, phoneNo);
                        } else {
                            Contact.AddDefaultPhoneNumberAttribute (Contact.AccountId, name, label, phoneNo);
                        }
                    } while (pCur.MoveToNext ());
                }
                pCur.Close ();
            }

            protected void GetContactEmails ()
            {
                Contact.EmailAddresses = new List<McContactEmailAddressAttribute> ();
                var pCur = MainApplication.Instance.ContentResolver.Query (
                               ContactsContract.CommonDataKinds.Email.ContentUri,
                               null, 
                               ContactsContract.CommonDataKinds.Email.InterfaceConsts.ContactId + " = ?",
                               new []{ Contact.ServerId }, null);
                if (pCur.MoveToFirst ()) {
                    do {
                        String email = GetField (pCur, ContactsContract.CommonDataKinds.Email.Address);
                        EmailDataKind type = (EmailDataKind)GetFieldInt (pCur, ContactsContract.CommonDataKinds.Email.InterfaceConsts.Type);
                        String label = GetField (pCur, ContactsContract.CommonDataKinds.Email.InterfaceConsts.Label);
                        string emailType = ContactsContract.CommonDataKinds.Email.GetTypeLabel (MainApplication.Instance.ApplicationContext.Resources, type, label);
                        int starred = GetFieldInt (pCur, ContactsContract.CommonDataKinds.Email.InterfaceConsts.Starred);
                        InternetAddressList addresses;
                        if (!InternetAddressList.TryParse (email, out addresses) || 0 == addresses.Count) {
                            continue;
                        }
                        if (addresses.Count > 1) {
                            foreach (var iAddr in addresses) {
                                var addr = iAddr as MailboxAddress;
                                if (null == addr) {
                                    continue;
                                }
                                var name = string.Format ("EmailAddress{0}-{1}", emailType, addresses.IndexOf (addr));
                                Contact.AddEmailAddressAttribute (Contact.AccountId, name, emailType, addr.ToString ()); // FIXME what are name and label?
                            }
                        } else {
                            var addr = addresses [0] as MailboxAddress;
                            var name = string.Format ("EmailAddress{0}", emailType, addresses.IndexOf (addr));
                            if (starred == 0) {
                                Contact.AddEmailAddressAttribute (Contact.AccountId, name, emailType, addr.ToString ()); // FIXME what are name and label?
                            } else {
                                Contact.AddDefaultEmailAddressAttribute (Contact.AccountId, name, emailType, addr.ToString ()); // FIXME what are name and label?
                            }
                        }
                    } while (pCur.MoveToNext ());
                }
                pCur.Close ();
            }

            protected void GetContactNotes ()
            {
                var pCur = MainApplication.Instance.ContentResolver.Query (ContactsContract.Data.ContentUri, 
                               null, // FIXME Add projection for speed
                               ContactsContract.CommonDataKinds.Note.InterfaceConsts.ContactId + "=? AND " +
                               ContactsContract.CommonDataKinds.Note.InterfaceConsts.Mimetype + "=?",
                               new []{ Contact.ServerId, ContactsContract.CommonDataKinds.Note.ContentItemType },
                               null);
                bool GotIt = false;
                if (pCur.MoveToFirst ()) {
                    do {
                        if (GotIt) {
                            Log.Warn (Log.LOG_SYS, "More than one note");
                        } else {
                            var note = GetFieldByte (pCur, ContactsContract.CommonDataKinds.Note.NoteColumnId);
                            if (null == note) {
                                Log.Warn (Log.LOG_CONTACTS, "Note found with no note!");
                            }
                            if (null != note && note.Length > 0) {
                                McBody body = null;
                                if (0 != Contact.BodyId) {
                                    body = McBody.QueryById<McBody> (Contact.BodyId);
                                }
                                if (null == body) {
                                    body = McBody.InsertFile (Contact.AccountId, McAbstrFileDesc.BodyTypeEnum.PlainText_1, note);
                                } else {
                                    body.UpdateData (note);
                                }
                                Contact.BodyId = body.Id;
                            }
                        }
                    } while (pCur.MoveToNext ());
                }
                pCur.Close ();
            }

            protected void GetContactPhoto ()
            {
                var pCur = MainApplication.Instance.ContentResolver.Query (ContactsContract.Data.ContentUri, 
                               new [] { ContactsContract.Contacts.InterfaceConsts.PhotoThumbnailUri },
                               ContactsContract.CommonDataKinds.Note.InterfaceConsts.ContactId + "=?",
                               new []{ Contact.ServerId },
                               null);
                bool GotIt = false;
                if (pCur.MoveToFirst ()) {
                    do {
                        if (GotIt) {
                            Log.Warn (Log.LOG_SYS, "More than one photo");
                        } else {
                            var photoUri = GetField (pCur, ContactsContract.Contacts.InterfaceConsts.PhotoThumbnailUri);
                            if (!string.IsNullOrEmpty (photoUri)) {
                                var url = Android.Net.Uri.Parse (photoUri);
                                var mBitmap = MediaStore.Images.Media.GetBitmap (MainApplication.Instance.ContentResolver, url);
                                if (mBitmap != null) {
                                    var ms = new MemoryStream ();

                                    mBitmap.Compress (Bitmap.CompressFormat.Png, 0, ms);
                                    McPortrait portrait = null;
                                    if (0 != Contact.PortraitId) {
                                        portrait = McPortrait.QueryById<McPortrait> (Contact.PortraitId);
                                    }
                                    if (null == portrait) {
                                        portrait = McPortrait.InsertFile (Contact.AccountId, ms.ToArray ());
                                    } else {
                                        portrait.UpdateData (ms.ToArray ());
                                    }
                                    Contact.PortraitId = portrait.Id;
                                }
                            }
                        }
                    } while (pCur.MoveToNext ());
                }
                pCur.Close ();
            }
        }

        #endregion
    }
}

