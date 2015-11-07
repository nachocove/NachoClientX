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
using System.IO;
using Android.Graphics;

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

        public static DateTime FromUnixTimeMilliseconds (string unixTimeStr)
        {
            long unixTime = long.Parse (unixTimeStr);
            var d = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return d.AddMilliseconds (unixTime);
        }

        public class PlatformContactRecordAndroid : PlatformContactRecord
        {
            McAccount Account;
            string _ServerId;

            public override string ServerId { get { return _ServerId; } }

            DateTime _LastUpdate;

            public override DateTime LastUpdate { get { return _LastUpdate; } }

            public PlatformContactRecordAndroid (McAccount account, long id, DateTime lastUpdate) : base ()
            {
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
                string displayName = null;
                var cr = MainApplication.Instance.ContentResolver;
                {
                    // Name
                    var pCur = cr.Query (ContactsContract.Data.ContentUri,
                                   null, // FIXME Add projection for speed
                                   ContactsContract.Data.InterfaceConsts.Mimetype + " = ? AND " + ContactsContract.CommonDataKinds.StructuredName.InterfaceConsts.ContactId + " = ?",
                                   new String[] { ContactsContract.CommonDataKinds.StructuredName.ContentItemType, Contact.ServerId },
                                   ContactsContract.CommonDataKinds.StructuredName.GivenName);
                    bool GotIt = false;
                    if (pCur.MoveToFirst ()) {
                        do {
                            if (GotIt) {
                                Log.Warn (Log.LOG_SYS, "Contact has more than one name");
                            } else {
                                displayName = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.DisplayName);

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
                {
                    // nickname
                    var pCur = cr.Query (ContactsContract.Data.ContentUri,
                                   null, // FIXME Add projection for speed
                                   ContactsContract.Data.InterfaceConsts.Mimetype + " = ? AND " + ContactsContract.CommonDataKinds.Nickname.InterfaceConsts.ContactId + " = ?",
                                   new String[] { ContactsContract.CommonDataKinds.Nickname.ContentItemType, Contact.ServerId },
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
                Log.Info (Log.LOG_SYS, "NcContact({0}): display={1} first={2} middle={3} last={4} (nick={5})", Contact.ServerId, displayName, Contact.FirstName, Contact.MiddleName, Contact.LastName, Contact.NickName);
                {
                    // company stuff
                    var pCur = cr.Query (ContactsContract.Data.ContentUri,
                                   null, // FIXME Add projection for speed
                                   ContactsContract.Data.InterfaceConsts.Mimetype + " = ? AND " + ContactsContract.CommonDataKinds.Organization.InterfaceConsts.ContactId + " = ?",
                                   new String[] { ContactsContract.CommonDataKinds.Organization.ContentItemType, Contact.ServerId },
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
                Log.Info (Log.LOG_SYS, "NcContact({0}): company={1} dept={2} title={3}", Contact.ServerId, Contact.CompanyName, Contact.Department, Contact.JobTitle);

                {
                    // Birthday (Not sure how to get this yet. All java examples use Event.TYPE_BIRTHDAY, but that doesn't seem to exist in C#
//                    Contact.Dates.RemoveAll ((x) => x.Name == "Birthday");
//                    string where = ContactsContract.CommonDataKinds.Event.InterfaceConsts.Mimetype + "= ? AND " +
//                        ContactsContract.CommonDataKinds.Event.InterfaceConsts.Type + "=" +
//                        ContactsContract.CommonDataKinds.Event.InterfaceConsts.Birthday;
//                    var pCur = cr.Query (ContactsContract.Data.ContentUri,
//                                   null,
//                                   where, new String[]{ ContactsContract.CommonDataKinds.Event.ContentItemType },
//                                   null);
//                    while (pCur.MoveToNext ()) {
//                        var birthday = GetField (pCur, ContactsContract.CommonDataKinds.Event.StartDate);
//                        Contact.AddDateAttribute (Account.Id, "Birthday", null, DateTime.Parse (birthday));
//                    }
//                    pCur.Close ();
                }
                {
                    // Addresses
                    var pCur = cr.Query (ContactsContract.Data.ContentUri, 
                                   null, // FIXME Add projection for speed
                                   ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.ContactId + "=? AND " +
                                   ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.Mimetype + "=?",
                                   new String[]{ Contact.ServerId, ContactsContract.CommonDataKinds.StructuredPostal.ContentItemType },
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
                            Log.Info (Log.LOG_SYS, "NcContact({0}): street={1} city={2} state={3} zip={4} country={5}", Contact.ServerId, attr.Street, attr.City, attr.State, attr.PostalCode, attr.Country);
                            Contact.AddAddressAttribute (Account.Id, addrType, label, attr);
                        } while (pCur.MoveToNext ());
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
                    if (pCur.MoveToFirst ()) {
                        do {
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
                            } else if (phoneType.ToLowerInvariant () == "mobile") {
                                name = "MobilePhoneNumber";
                                label = null;
                            } else {
                                // assume mobile
                                name = "MobilePhoneNumber";
                                label = null;
                            }
                            Log.Info (Log.LOG_SYS, "NcContact({0}): phoneType={1} phoneLabel={2} phoneNo={3}", Contact.ServerId, name, label, phoneNo);
                            Contact.AddPhoneNumberAttribute (Account.Id, name, label, phoneNo);
                        } while (pCur.MoveToNext ());
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
                    if (pCur.MoveToFirst ()) {
                        do {
                            String email = GetField (pCur, ContactsContract.CommonDataKinds.Email.Address);
                            EmailDataKind type = (EmailDataKind)GetFieldInt (pCur, ContactsContract.CommonDataKinds.Email.InterfaceConsts.Type);
                            String label = GetField (pCur, ContactsContract.CommonDataKinds.Email.InterfaceConsts.Label);
                            string emailType = ContactsContract.CommonDataKinds.Email.GetTypeLabel (MainApplication.Instance.ApplicationContext.Resources, type, label);
                            var name = string.Format ("EmailAddress{0}", emailType);
                            Log.Info (Log.LOG_SYS, "NcContact({0}): emailType={1} emailLabel={2} email={3} ({4} {5})", Contact.ServerId, name, emailType, email, type, label);
                            Contact.AddEmailAddressAttribute (Account.Id, name, emailType, email); // FIXME what are name and label?
                        } while (pCur.MoveToNext ());
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
                    if (pCur.MoveToFirst ()) {
                        do {
                            if (GotIt) {
                                Log.Warn (Log.LOG_SYS, "More than one note");
                            } else {
                                var note = GetFieldByte (pCur, ContactsContract.CommonDataKinds.Note.NoteColumnId);
                                if (note.Length > 0) {
                                    McBody body = null;
                                    if (0 != Contact.BodyId) {
                                        body = McBody.QueryById<McBody> (Contact.BodyId);
                                    }
                                    if (null == body) {
                                        body = McBody.InsertFile (Account.Id, McAbstrFileDesc.BodyTypeEnum.PlainText_1, note);
                                    } else {
                                        body.UpdateData (note);
                                    }
                                    Log.Info (Log.LOG_SYS, "NcContact({0}): bodyId={1} note={2}", Contact.ServerId, body.Id, note);
                                    Contact.BodyId = body.Id;
                                }
                            }
                        } while (pCur.MoveToNext ());
                    }
                    pCur.Close ();
                }
                {
                    // Photo
                    var pCur = cr.Query (ContactsContract.Data.ContentUri, 
                                   new string[] { ContactsContract.Contacts.InterfaceConsts.PhotoThumbnailUri },
                                   ContactsContract.CommonDataKinds.Note.InterfaceConsts.ContactId + "=?",
                                   new String[]{ Contact.ServerId },
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
                                            portrait = McPortrait.InsertFile (Account.Id, ms.ToArray ());
                                        } else {
                                            portrait.UpdateData (ms.ToArray ());
                                        }
                                        Log.Info (Log.LOG_SYS, "NcContact({0}): portraitId={1} datalen={2}", Contact.ServerId, portrait.Id, ms.Length);
                                        Contact.PortraitId = portrait.Id;
                                    }
                                }
                            }
                        } while (pCur.MoveToNext ());
                    }
                    pCur.Close ();
                }
                return NcResult.OK (Contact);
            }
        }
    }
}

