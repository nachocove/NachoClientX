//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;
using Android.Provider;
using Android.Widget;
using NachoClient.AndroidClient;
using Android.Content;
using NachoPlatform;

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
            var cr = MainApplication.Instance.ContentResolver;
            var projection = new string[] {
                ContactsContract.Contacts.InterfaceConsts.Id,
                ContactsContract.Contacts.InterfaceConsts.ContactLastUpdatedTimestamp,
                ContactsContract.Contacts.InterfaceConsts.HasPhoneNumber,
            };
            var cur = cr.Query (ContactsContract.Contacts.ContentUri, projection, null, null, null, null);
            if (cur.Count > 0) {
                while (cur.MoveToNext ()) {
                    long id = GetFieldLong (cur, ContactsContract.Contacts.InterfaceConsts.Id);

                    String lastUpdateString = GetField (cur, ContactsContract.Contacts.InterfaceConsts.ContactLastUpdatedTimestamp);
                    var lastUpdate = FromUnixTimeMilliseconds (lastUpdateString);
                    //bool HasPhoneNumber = int.Parse (GetField (cur, ContactsContract.Contacts.InterfaceConsts.HasPhoneNumber)) > 0;
                    var entry = new PlatformContactRecordAndroid (deviceAccount, id, lastUpdate);
                    retval.Add (entry);
                }
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

        public static string GetField (Android.Database.ICursor cur, string Column)
        {
            return cur.GetString (cur.GetColumnIndex (Column));
        }

        public static int GetFieldInt (Android.Database.ICursor cur, string Column)
        {
            return cur.GetInt (cur.GetColumnIndex (Column));
        }

        public static long GetFieldLong (Android.Database.ICursor cur, string Column)
        {
            return cur.GetLong (cur.GetColumnIndex (Column));
        }

        public static DateTime FromUnixTimeMilliseconds (string unixTimeStr)
        {
            long unixTime = long.Parse (unixTimeStr);
            var d = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return d.AddMilliseconds (unixTime);
        }

        public class PlatformContactRecordAndroid : PlatformContactRecord
        {
            McAccount Account;
            long Id;
            string _ServerId;

            public override string ServerId { get { return _ServerId; } }

            DateTime _LastUpdate;

            public override DateTime LastUpdate { get { return _LastUpdate; } }

            public PlatformContactRecordAndroid (McAccount account, long id, DateTime lastUpdate) : base ()
            {
                Id = id;
                Account = account;
                _ServerId = id.ToString ();
                _LastUpdate = lastUpdate;
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
                } else {
                    Contact = contactToUpdate;
                }

                Contact.DeviceLastUpdate = _LastUpdate;

                var cr = MainApplication.Instance.ContentResolver;
                {
                    // Name
                    string whereName = ContactsContract.Data.InterfaceConsts.Mimetype + " = ? AND " + ContactsContract.CommonDataKinds.StructuredName.InterfaceConsts.ContactId + " = ?";
                    String[] whereNameParams = new String[] { ContactsContract.CommonDataKinds.StructuredName.ContentItemType, Contact.ServerId };
                    var pCur = cr.Query (ContactsContract.Data.ContentUri,
                                   null, // FIXME Add projection for speed
                                   whereName, whereNameParams,
                                   ContactsContract.CommonDataKinds.StructuredName.GivenName);
                    bool GotIt = false;
                    while (pCur.MoveToNext ()) {
                        if (GotIt) {
                            Log.Warn (Log.LOG_SYS, "Contact has more than one name");
                        } else {
                            Contact.FirstName = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.GivenName);
                            Contact.LastName = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.FamilyName);
                            Contact.MiddleName = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.MiddleName);
                            Contact.NickName = GetField (pCur, ContactsContract.CommonDataKinds.Nickname.Name);
                            Contact.YomiFirstName = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.PhoneticGivenName);
                            Contact.YomiLastName = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.PhoneticFamilyName);
                            Contact.Title = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.Prefix);
                            Contact.Suffix = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.Suffix);

                            Contact.CompanyName = GetField (pCur, ContactsContract.CommonDataKinds.Organization.Company);
                            Contact.YomiCompanyName = GetField (pCur, ContactsContract.CommonDataKinds.Organization.PhoneticName);
                            Contact.JobTitle = GetField (pCur, ContactsContract.CommonDataKinds.Organization.Title);
                            Contact.Department = GetField (pCur, ContactsContract.CommonDataKinds.Organization.Department);

                            GotIt = true;
                        }
                    }
                    if (!GotIt) {
                        Log.Error (Log.LOG_SYS, "No name found for contact");
                    }
                    pCur.Close ();
                }

                {
                    // Birthday
                    Contact.Dates.RemoveAll ((x) => x.Name == "Birthday");
                    string where = ContactsContract.Data.InterfaceConsts.Mimetype + "= ? AND " +
                                   ContactsContract.CommonDataKinds.Event.InterfaceConsts.Type + "=" +
                                   ContactsContract.CommonDataKinds.Event.GetTypeResource (EventDataKind.Birthday);
                    var pCur = cr.Query (ContactsContract.Data.ContentUri,
                                   null,
                                   where, new String[]{ ContactsContract.CommonDataKinds.Event.ContentItemType },
                                   null);
                    while (pCur.MoveToNext ()) {
                        var birthday = GetField (pCur, ContactsContract.CommonDataKinds.Event.StartDate);
                        Contact.AddDateAttribute (Account.Id, "Birthday", null, DateTime.Parse (birthday));
                    }
                    pCur.Close ();
                }
                {
                    // Addresses
                    var pCur = cr.Query (ContactsContract.Data.ContentUri, 
                                   null, // FIXME Add projection for speed
                                   ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.ContactId + "=? AND " +
                                   ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.Mimetype + "=?",
                                   new String[]{ Contact.ServerId, ContactsContract.CommonDataKinds.StructuredPostal.ContentItemType },
                                   null);
                    while (pCur.MoveToNext ()) {
                        AddressDataKind type = (AddressDataKind)GetFieldInt (pCur, ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.Type);
                        String label = GetField (pCur, ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.Label);
                        string addrType = ContactsContract.CommonDataKinds.StructuredPostal.GetTypeLabel (MainApplication.Instance.ApplicationContext.Resources, type, label);
                        var attr = new McContactAddressAttribute ();
                        attr.Street = GetField (pCur, ContactsContract.CommonDataKinds.StructuredPostal.Street);
                        attr.PostalCode = GetField (pCur, ContactsContract.CommonDataKinds.StructuredPostal.Postcode);
                        attr.City = GetField (pCur, ContactsContract.CommonDataKinds.StructuredPostal.City);
                        attr.Country = GetField (pCur, ContactsContract.CommonDataKinds.StructuredPostal.Country);
                        attr.State = GetField (pCur, ContactsContract.CommonDataKinds.StructuredPostal.Region);
                        Contact.AddAddressAttribute (Account.Id, addrType, label, attr);
                    }
                    pCur.Close ();
                }

                {
                    // Phone numbers
                    var pCur = cr.Query (
                                   ContactsContract.CommonDataKinds.Phone.ContentUri,
                                   new String[]{ }, 
                                   ContactsContract.CommonDataKinds.Phone.InterfaceConsts.ContactId + " = ?",
                                   new String[]{ Contact.ServerId }, null);
                    while (pCur.MoveToNext ()) {
                        String phoneNo = GetField (pCur, ContactsContract.CommonDataKinds.Phone.Number);
                        PhoneDataKind type = (PhoneDataKind)GetFieldInt (pCur, ContactsContract.CommonDataKinds.Phone.InterfaceConsts.Type);
                        String phLabel = GetField (pCur, ContactsContract.CommonDataKinds.Phone.InterfaceConsts.Label);
                        string phoneType = ContactsContract.CommonDataKinds.Phone.GetTypeLabel (MainApplication.Instance.ApplicationContext.Resources, type, phLabel);
                        string name;
                        string label;
                        Log.Info (Log.LOG_SYS, "Phone type: {0}:{1}:{2}", phoneType, phLabel, type);
                        if (phoneType.ToLowerInvariant () == "home") {
                            name = "HomePhoneNumber";
                            label = "Home";
                        } else if (phoneType.ToLowerInvariant () == "work") {
                            name = "BusinessPhoneNumber";
                            label = "Work";
                        } else {
                            name = "MobilePhoneNumber";
                            label = null;
                        }
                        Contact.AddPhoneNumberAttribute (Account.Id, name, label, phoneNo);
                    }
                    pCur.Close ();
                }
                {
                    // Emails
                    var pCur = cr.Query (
                                   ContactsContract.CommonDataKinds.Email.ContentUri,
                                   null, 
                                   ContactsContract.CommonDataKinds.Email.InterfaceConsts.ContactId + " = ?",
                                   new String[]{ Contact.ServerId }, null);
                    var i = 0;
                    while (pCur.MoveToNext ()) {
                        String email = GetField (pCur, ContactsContract.CommonDataKinds.Email.Address);
                        EmailDataKind type = (EmailDataKind)GetFieldInt (pCur, ContactsContract.CommonDataKinds.Email.InterfaceConsts.Type);
                        String label = GetField (pCur, ContactsContract.CommonDataKinds.Email.InterfaceConsts.Label);
                        string emailType = ContactsContract.CommonDataKinds.Email.GetTypeLabel (MainApplication.Instance.ApplicationContext.Resources, type, label);
                        Log.Info (Log.LOG_SYS, "Email type: {0}:{1}:{2}", emailType, label, type);
                        Contact.AddEmailAddressAttribute (Account.Id, string.Format ("Email{0}Address", i), emailType, email); // FIXME what are name and label?
                        i++;
                    }
                    pCur.Close ();
                }
                {
                    // Notes
                    var pCur = cr.Query (ContactsContract.Data.ContentUri, 
                        null, // FIXME Add projection for speed
                        ContactsContract.CommonDataKinds.Note.InterfaceConsts.ContactId + "=? AND " +
                        ContactsContract.CommonDataKinds.Note.InterfaceConsts.Mimetype + "=?",
                        new String[]{ Contact.ServerId, ContactsContract.CommonDataKinds.Note.ContentItemType },
                        null);
                    bool GotIt = false;
                    while (pCur.MoveToNext ()) {
                        if (GotIt) {
                            Log.Warn (Log.LOG_SYS, "More than one note");
                        } else {
                            var note = GetField (pCur, ContactsContract.CommonDataKinds.Note.NoteColumnId);
                            if (!string.IsNullOrEmpty (note)) {
                                McBody body = null;
                                if (0 != Contact.BodyId) {
                                    body = McBody.QueryById<McBody> (Contact.BodyId);
                                }
                                if (null == body) {
                                    body = McBody.InsertFile (Account.Id, McAbstrFileDesc.BodyTypeEnum.PlainText_1, note);
                                } else {
                                    body.UpdateData (note);
                                }
                                Contact.BodyId = body.Id;
                            }
                        }
                    }
                }
                {
                    // Photo
                    var contactUri = ContentUris.WithAppendedId (ContactsContract.Contacts.ContentUri, Id);
                    var contactPhotoUri = Android.Net.Uri.WithAppendedPath (contactUri, Android.Provider.Contacts.Photos.ContentUri.EncodedPath);
                    var photoFd = Assets.AndroidAssetManager.OpenFd (contactPhotoUri.EncodedPath);
                    var photoStream = photoFd.CreateInputStream ();
                    McPortrait portrait = null;
                    if (0 != Contact.PortraitId) {
                        portrait = McPortrait.QueryById<McPortrait> (Contact.PortraitId);
                    }
                    if (null == portrait) {
                        portrait = McPortrait.InsertFile (Account.Id, photoStream);
                    } else {
                        portrait.UpdateData (photoStream);
                    }
                    Contact.PortraitId = portrait.Id;
                }
                return NcResult.OK (Contact);
            }
        }
    }
}

