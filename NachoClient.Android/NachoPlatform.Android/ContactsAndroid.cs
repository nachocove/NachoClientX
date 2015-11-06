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
            var cur = cr.Query (ContactsContract.Contacts.ContentUri,
                new string[] {
                    ContactsContract.Contacts.InterfaceConsts.Id,
                    ContactsContract.Contacts.InterfaceConsts.ContactLastUpdatedTimestamp,
                    ContactsContract.Contacts.InterfaceConsts.HasPhoneNumber,
                },
                null, null, null, null);
            if (cur.Count > 0) {
                while (cur.MoveToNext ()) {
                    String id = GetField (cur, ContactsContract.Contacts.InterfaceConsts.Id);

                    String lastUpdateString = GetField (cur, ContactsContract.Contacts.InterfaceConsts.ContactLastUpdatedTimestamp);
                    var lastUpdate = FromUnixTimeMilliseconds (lastUpdateString);
                    bool HasPhoneNumber = int.Parse (GetField (cur, ContactsContract.Contacts.InterfaceConsts.HasPhoneNumber)) > 0;
                    var entry = new PlatformContactRecordAndroid (deviceAccount, id, lastUpdate);
                    entry.fromCursor (cr, id, HasPhoneNumber);
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

        public static DateTime FromUnixTimeMilliseconds (string unixTimeStr)
        {
            long unixTime = long.Parse (unixTimeStr);
            var d = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return d.AddMilliseconds (unixTime);
        }

        public class PlatformContactRecordAndroid : PlatformContactRecord
        {
            public McContact Contact;
            McAccount Account;

            public override string ServerId { get { return Contact.ServerId; } }

            public override DateTime LastUpdate { get { return Contact.LastModified; } }

            public PlatformContactRecordAndroid (McAccount account, string serverId, DateTime lastUpdate) : base ()
            {
                Account = account;
                Contact = new McContact ();
                Contact.AccountId = account.Id;
                Contact.ServerId = serverId;
                Contact.LastModified = lastUpdate;
            }

            public void fromCursor (ContentResolver cr, string id, bool HasPhoneNumber)
            {
                {
                    string whereName = ContactsContract.Data.InterfaceConsts.Mimetype + " = ? AND " + ContactsContract.CommonDataKinds.StructuredName.InterfaceConsts.ContactId + " = ?";
                    String[] whereNameParams = new String[] { ContactsContract.CommonDataKinds.StructuredName.ContentItemType, id };
                    var pCur = cr.Query (ContactsContract.Data.ContentUri, null, whereName, whereNameParams, ContactsContract.CommonDataKinds.StructuredName.GivenName);
                    bool GotIt = false;
                    while (pCur.MoveToNext ()) {
                        var first = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.GivenName);
                        var last = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.FamilyName);
                        var middle = GetField (pCur, ContactsContract.CommonDataKinds.StructuredName.MiddleName);
                        if (GotIt) {
                            Log.Error (Log.LOG_SYS, "Contact has more than one name? Name: {0} {1} {2}", first, middle, last);
                        } else {
                            Contact.FirstName = first;
                            Contact.LastName = last;
                            Contact.MiddleName = middle;
                            GotIt = true;
                        }
                    }
                    if (!GotIt) {
                        Log.Error (Log.LOG_SYS, "No name found for contact");
                    }
                    pCur.Close ();
                }

                {
                    var pCur = cr.Query (ContactsContract.Data.ContentUri, 
                                   null,
                                   ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.ContactId + "=? AND " +
                                   ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.Mimetype + "=?",
                                   new String[]{ id, ContactsContract.CommonDataKinds.StructuredPostal.ContentItemType },
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

                if (HasPhoneNumber) {
                    var pCur = cr.Query (
                                   ContactsContract.CommonDataKinds.Phone.ContentUri,
                                   new String[]{ }, 
                                   ContactsContract.CommonDataKinds.Phone.InterfaceConsts.ContactId + " = ?",
                                   new String[]{ id }, null);
                    while (pCur.MoveToNext ()) {
                        String phoneNo = GetField (pCur, ContactsContract.CommonDataKinds.Phone.Number);
                        PhoneDataKind type = (PhoneDataKind)GetFieldInt (pCur, ContactsContract.CommonDataKinds.Phone.InterfaceConsts.Type);
                        String label = GetField (pCur, ContactsContract.CommonDataKinds.Phone.InterfaceConsts.Label);
                        string phoneType = ContactsContract.CommonDataKinds.Phone.GetTypeLabel (MainApplication.Instance.ApplicationContext.Resources, type, label);
                        Contact.AddPhoneNumberAttribute (Account.Id, phoneType, label, phoneNo); // FIXME what are name and label?
                    }
                    pCur.Close ();
                }
                {
                    var pCur = cr.Query (
                                   ContactsContract.CommonDataKinds.Email.ContentUri,
                                   null, 
                                   ContactsContract.CommonDataKinds.Email.InterfaceConsts.ContactId + " = ?",
                                   new String[]{ id }, null);
                    while (pCur.MoveToNext ()) {
                        String email = GetField (pCur, ContactsContract.CommonDataKinds.Email.Address);
                        EmailDataKind type = (EmailDataKind)GetFieldInt (pCur, ContactsContract.CommonDataKinds.Email.InterfaceConsts.Type);
                        String label = GetField (pCur, ContactsContract.CommonDataKinds.Email.InterfaceConsts.Label);
                        string emailType = ContactsContract.CommonDataKinds.Email.GetTypeLabel (MainApplication.Instance.ApplicationContext.Resources, type, label);

                        Contact.AddEmailAddressAttribute (Account.Id, emailType, label, email); // FIXME what are name and label?
                    }
                    pCur.Close ();
                }
            }

            public override NcResult ToMcContact (McContact contactToUpdate)
            {
                return NcResult.OK (Contact);
            }
        }
    }
}

