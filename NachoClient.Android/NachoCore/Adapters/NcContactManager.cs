//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;

namespace NachoCore
{
    public class NcContactManager
    {
        private static volatile NcContactManager instance;
        private static object syncRoot = new Object ();

        public static NcContactManager Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new NcContactManager ();
                    }
                }
                return instance; 
            }
        }

        public event EventHandler ContactsChanged;

        protected NcContactManager ()
        {
            // Watch for changes from the back end
            BackEnd.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_ContactSetChanged == s.Status.SubKind) {
                    LoadContacts ();
                }
            };
        }

        List<McContact> contactList;
        List<McContactStringAttribute> addressList;

        protected void LoadContacts ()
        {
            contactList = BackEnd.Instance.Db.Table<McContact> ().OrderBy (c => c.LastName).ToList ();
            if (null == contactList) {
                contactList = new List<McContact> ();
            }
            addressList = BackEnd.Instance.Db.Table<McContactStringAttribute> ().Where (x => x.Type == McContactStringType.EmailAddress).ToList ();
            if (null == addressList) {
                addressList = new List<McContactStringAttribute> ();
            }
            if (null != ContactsChanged) {
                InvokeOnUIThread.Instance.Invoke (delegate() {  
                    ContactsChanged.Invoke (this, null);
                });
            }
        }

        public INachoContacts GetNachoContactsObject ()
        {
            if ((null == contactList) || (null == addressList)) {
                LoadContacts ();
            }
            return new InternalNachoContactsObject (contactList, addressList);
        }

        protected class InternalNachoContactsObject : INachoContacts
        {
            List<Int64> searchResults;
            List<McContact> contactList;
            List<McContactStringAttribute> addressList;

            public InternalNachoContactsObject (List<McContact> contactList, List<McContactStringAttribute> addressList)
            {
                this.contactList = contactList;
                this.addressList = addressList;
                this.searchResults = new List<Int64>();

            }

            public int Count ()
            {
                return contactList.Count;
            }

            public McContact GetContact (int i)
            {
                var c = contactList.ElementAt (i);
                c.ReadAncillaryData (BackEnd.Instance.Db);
                return c;
            }

            public void Search(string prefix)
            {
                searchResults = MatchesPrefix (prefix);
            }

            public int SearchResultsCount()
            {
                return searchResults.Count;
            }

            public McContact GetSearchResult(int searchIndex)
            {
                var id = searchResults [searchIndex];
                for(int i = 0; i < contactList.Count; i++) {
                    if(id == contactList[i].Id) {
                        return GetContact(i);
                    }
                }
                return null;
            }

            /// <summary>
            /// Returns a list of McContact.Ids.
            /// </summary>
            protected List<Int64> MatchesPrefix (string prefix)
            {
                var list = new HashSet<Int64> ();

                // Empty (or null) means everyone
                if (String.IsNullOrEmpty (prefix)) {
                    foreach (var a in addressList) {
                        list.Add (a.ContactId);
                    }
                    return list.ToList();
                }

                for(int i = 0; i < contactList.Count; i++) {
                    foreach(var c in contactList) {
                        if (StartsWithIgnoringNull (prefix, c.FirstName)) {
                            list.Add (c.Id);
                        } else if (StartsWithIgnoringNull (prefix, c.LastName)) {
                            list.Add (c.Id);
                        }
                    }
                }
                foreach (var a in addressList) {
                    if (StartsWithIgnoringNull (prefix, a.Value)) {
                        list.Add (a.ContactId);
                    }
                }
                return list.ToList();
            }

            protected bool StartsWithIgnoringNull (string prefix, string target)
            {
                NachoCore.NachoAssert.True (null != prefix);
                // Can't match a field that doesn't exist
                if (null == target) {
                    return false;
                }
                // TODO: Verify that we really want InvariantCultureIgnoreCase
                return target.StartsWith (prefix, StringComparison.InvariantCultureIgnoreCase);
            }
        }
    }
}

