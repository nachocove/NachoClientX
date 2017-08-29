//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;

using NachoCore.Brain;
using NachoCore.Index;
using NachoCore.Model;
using NachoPlatform;
using System.Collections.Concurrent;

namespace NachoCore.Utils
{
    // Search contacts.  Use class ContactsEmailSearch to limit the search to contacts with e-mail addresses.
    // Use class ContactsGeneralSearch to search all contacts.  The API for both classes is the same:
    // 1. Pass a delegate to the constructor that will update the UI when search results are available.
    // 2. Call SearchFor() to start a search.  Call SearchFor() again when the search string changes.
    // 3. Call Dispose() when searching is complete.

    public abstract class ContactsSearchCommon : IDisposable
    {
        public delegate void UpdateUiAction (string searchString, List<McContactEmailAddressAttribute> results);

        UpdateUiAction updateUi;

        object lockObject = new object ();
        bool disposed = false;

        bool searchInProgress = false;
        string lastSearch = null;
        string currentSearch = null;
        string nextSearch = null;
        bool serverResultsPending = false;

        List<McAccount> accounts;
        IDictionary<int, string> accountSearchTokens;

        public void SearchFor (string searchString)
        {
            if (disposed) {
                throw new ObjectDisposedException ("ContactsSearchCommon");
            }

            if (null == searchString) {
                searchString = "";
            }

            bool startSearching;
            lock (lockObject) {
                nextSearch = searchString;
                serverResultsPending = false;
                startSearching = !searchInProgress;
                searchInProgress = true;
            }

            if (startSearching) {
                StartSearch ();
            }
        }

        void StartSearch ()
        {

            NcTask.Run (() => {

                if (null == accounts) {
                    accounts = McAccount.QueryByAccountCapabilities (McAccount.AccountCapabilityEnum.ContactReader).ToList ();
                }

                bool keepSearching = false;
                do {
                    bool serverOnly;
                    string searchString;
                    lock (lockObject) {
                        serverOnly = serverResultsPending;
                        if (serverOnly) {
                            currentSearch = lastSearch;
                            serverResultsPending = false;
                        } else {
                            currentSearch = nextSearch;
                            nextSearch = null;
                        }
                        searchString = currentSearch;
                    }
                    // searchString can only be null if Dispose() was called while this task was queued up waiting to start.
                    if (null != searchString) {
                        if (serverOnly) {
                            IncorporateServerResults (searchString);
                        } else {
                            // Start the server-based search for any account where a search is not already in progress.
                            if (!string.IsNullOrEmpty (searchString) && 2 < searchString.Length && 0 < accounts.Count) {
                                foreach (var account in accounts) {
                                    if (!accountSearchTokens.ContainsKey (account.Id)) {
                                        accountSearchTokens [account.Id] = BackEnd.Instance.StartSearchContactsReq (account.Id, searchString, null).GetValue<string> ();
                                    }
                                }
                            }
                            // Do the local search, which is subclass-specific.
                            DoSearch (searchString);
                        }
                    }
                    lock (lockObject) {
                        lastSearch = searchString;
                        keepSearching = (null != nextSearch) || serverResultsPending;
                        if (!keepSearching) {
                            // This has to happen within this lock statement.  It can't be delayed until after the while loop exits.
                            currentSearch = null;
                            searchInProgress = false;
                        }
                    }
                } while (keepSearching);
            }, "ContactsSearch");
        }

        protected ContactsSearchCommon (UpdateUiAction updateUi)
        {
            this.updateUi = updateUi;
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            // Initializing accounts requires a database query, so delay that until there is a background thread.
            accounts = null;
            accountSearchTokens = new ConcurrentDictionary<int, string> ();
        }

        protected void UpdateUi (string searchString, List<McContactEmailAddressAttribute> results)
        {
            InvokeOnUIThread.Instance.Invoke (() => {
                updateUi (searchString, results);
            });
        }

        protected abstract void DoSearch (string searchString);
        protected abstract void IncorporateServerResults (string searchString);

        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
        }

        protected virtual void Dispose (bool disposing)
        {
            if (disposed) {
                return;
            }
            disposed = true;
            if (disposing) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                lock (lockObject) {
                    // Let the current search continue, but prevent any new search from starting.
                    nextSearch = null;
                    serverResultsPending = false;
                    // If there is a current search, stop it from updating the UI when it completes.
                    updateUi = (string searchString, List<McContactEmailAddressAttribute> results) => { };
                }
                accountSearchTokens.Clear ();
            }
        }

        void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_ContactSearchCommandSucceeded == s.Status.SubKind &&
                null != s.Account && null != s.Tokens &&
                accountSearchTokens.Keys.Contains (s.Account.Id) &&
                s.Tokens.Contains (accountSearchTokens [s.Account.Id])) {
                accountSearchTokens.Remove (s.Account.Id);
                bool startSearch;
                lock (lockObject) {
                    startSearch = (null != lastSearch) && !searchInProgress;
                    if (startSearch) {
                        searchInProgress = true;
                    }
                    if (null == nextSearch) {
                        serverResultsPending = true;
                    }
                }
                if (startSearch) {
                    StartSearch ();
                }
            }
        }

        /// <summary>
        /// Helper class for ranking and filtering potential matches for a contact search.
        /// </summary>
        protected class SearchMatch
        {
            public McContact contact;
            public McContactEmailAddressAttribute attribute;
            public string email;
            public int matchScore;

            public SearchMatch (McContact contact, McContactEmailAddressAttribute attribute)
            {
                this.contact = contact;
                this.attribute = attribute;
                this.email = attribute.Value;
                this.matchScore = -1;
            }
        }

        protected static bool StartsWith (string s, string substring)
        {
            if (null == s) {
                return false;
            }
            return s.StartsWith (substring, StringComparison.InvariantCultureIgnoreCase);
        }

        protected static bool Contains (string s, string substring)
        {
            if (null == s) {
                return false;
            }
            return 0 <= s.IndexOf (substring, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Return the quality or value of the contact based on its source.
        /// </summary>
        protected static int ContactSourceScore (McContact contact)
        {
            if (contact.IsRic ()) {
                return 3;
            }
            switch (contact.Source) {
            case McContact.ItemSource.ActiveSync:
                return 5;
            case McContact.ItemSource.Device:
                return 4;
            case McContact.ItemSource.Internal:
                return 2;
            default:
                return 1;
            }
        }

        /// <summary>
        /// Does the given contact have a name?
        /// </summary>
        protected static bool HasName (McContact contact)
        {
            return !string.IsNullOrEmpty (contact.FirstName) || !string.IsNullOrEmpty (contact.LastName);
        }

        /// <summary>
        /// Do the two contacts have the exact same name?  That includes the company name, but ignores case.
        /// </summary>
        protected static bool SameName (McContact x, McContact y)
        {
            return string.Equals (x.FirstName, y.FirstName, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals (x.MiddleName, y.MiddleName, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals (x.LastName, y.LastName, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals (x.Suffix, y.Suffix, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals (x.CompanyName, y.CompanyName, StringComparison.InvariantCultureIgnoreCase);
        }

    }

    /// <summary>
    /// Search all accounts for contacts that have an e-mail address.  Dispose() must be called when
    /// searching has completed.
    /// </summary>
    public class ContactsEmailSearch : ContactsSearchCommon
    {
        List<McContactEmailAddressAttribute> previousResults = null;

        // [McEmailAddress.Id -> McEmailAddress.Score]  This is a cache of the brain
        // scores for email addresses.  Keeping this cache greatly reduces the number
        // of McEmailAddress.QueryById() calls.
        Dictionary<int, double> addressScores = new Dictionary<int, double> ();

        /// <summary>
        /// Initializes a new instance of the <see cref="NachoCore.Utils.ContactsEmailSearch"/> class.
        /// </summary>
        /// <param name="updateUi">Code that will be called on the UI thread when search results are available.</param>
        public ContactsEmailSearch (UpdateUiAction updateUi)
            : base (updateUi)
        {
        }

        protected override void DoSearch (string searchString)
        {
            var searchWords = searchString.Trim ().Split (new char [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var contactMatches = new List<McContact> ();
            var emailMatches = new List<McContactEmailAddressAttribute> ();

            foreach (var searchWord in searchWords) {
                // Search the database for contacts whose name matches the search string.
                contactMatches.AddRange (McContact.SearchAllContactsByName (searchWord));
                // Search the database for e-mail addresses that match the search string.
                emailMatches.AddRange (McContact.SearchAllContactEmail (searchWord));
            }

            var allMatches = new List<SearchMatch> ();
            var matchedAttributeIds = new HashSet<int> ();
            var matchedContactIds = new HashSet<int> ();

            // Convert the matched e-mail addresses into SearchMatch objects, avoiding duplicates.
            foreach (var emailMatch in emailMatches) {
                if (matchedAttributeIds.Add (emailMatch.Id)) {
                    // An old bug resulted in some databases that have orphaned McContactEmailAddressAttribute records.
                    // Don't crash if one of those orphaned objects is found during the search.
                    var contact = McContact.QueryById<McContact> ((int)emailMatch.ContactId);
                    if (null != contact) {
                        allMatches.Add (new SearchMatch (contact, emailMatch));
                    }
                }
            }

            // Convert the e-mail addresses of the matched contacts into SearchMatch objects, avoiding duplicates.
            foreach (var contactMatch in contactMatches) {
                if (0 < contactMatch.EmailAddresses.Count && !contactMatch.IsAwaitingDelete && matchedContactIds.Add (contactMatch.Id)) {
                    foreach (var emailAttribute in contactMatch.EmailAddresses) {
                        if (matchedAttributeIds.Add (emailAttribute.Id)) {
                            allMatches.Add (new SearchMatch (contactMatch, emailAttribute));
                        }
                    }
                }
            }

            if (0 == allMatches.Count) {
                var emptyResult = new List<McContactEmailAddressAttribute> ();
                if (null != previousResults && 0 != previousResults.Count) {
                    UpdateUi (searchString, emptyResult);
                }
                previousResults = emptyResult;
                return;
            }

            // Score each match based on how well it matches the search string and on the brain score of the email address.
            foreach (var match in allMatches) {
                int score = 0;
                foreach (var word in searchWords) {
                    if (StartsWith (match.email, word)) {
                        score += 5 * word.Length;
                    } else if (StartsWith (match.contact.LastName, word)) {
                        score += 4 * word.Length;
                    } else if (StartsWith (match.contact.FirstName, word)) {
                        score += 3 * word.Length;
                    } else if (StartsWith (match.contact.CompanyName, word)) {
                        score += 2 * word.Length;
                    } else if (Contains (match.email, "@" + word)) {
                        score += 2 * word.Length;
                    }
                }
                if (1 < searchWords.Length) {
                    if (Contains (match.contact.LastName, searchString)) {
                        score += 8 * searchString.Length;
                    } else if (Contains (match.contact.FirstName, searchString)) {
                        score += 6 * searchString.Length;
                    } else if (Contains (match.contact.CompanyName, searchString)) {
                        score += 4 * searchString.Length;
                    }
                }
                if (0 != match.contact.PortraitId) {
                    score += 1;
                }
                double addressScore;
                if (!addressScores.TryGetValue (match.attribute.EmailAddress, out addressScore)) {
                    var emailAddress = McEmailAddress.QueryById<McEmailAddress> (match.attribute.EmailAddress);
                    if (null != emailAddress) {
                        addressScore = emailAddress.Score;
                    } else {
                        addressScore = 0;
                    }
                    addressScores [match.attribute.EmailAddress] = addressScore;
                }
                score += (int)(addressScore * 10);

                match.matchScore = score;
            }

            // Sort the matches by (1) e-mail address, so we can remove duplicates,
            // (2) match score, (3) the quality of the contact, prefering contacts
            // with more information.
            allMatches.Sort ((SearchMatch x, SearchMatch y) => {
                int emailCompare = string.Compare (x.email, y.email);
                if (0 != emailCompare) {
                    return emailCompare;
                }
                if (x.matchScore != y.matchScore) {
                    return y.matchScore - x.matchScore;
                }
                var xHasPortrait = 0 != x.contact.PortraitId;
                var yHasPortrait = 0 != y.contact.PortraitId;
                if (xHasPortrait && !yHasPortrait) {
                    return -1;
                }
                if (!xHasPortrait && yHasPortrait) {
                    return 1;
                }
                var xHasLast = !string.IsNullOrEmpty (x.contact.LastName);
                var yHasLast = !string.IsNullOrEmpty (y.contact.LastName);
                if (xHasLast && !yHasLast) {
                    return -1;
                }
                if (!xHasLast && yHasLast) {
                    return 1;
                }
                var xHasFirst = !string.IsNullOrEmpty (x.contact.FirstName);
                var yHasFirst = !string.IsNullOrEmpty (y.contact.FirstName);
                if (xHasFirst && !yHasFirst) {
                    return -1;
                }
                if (!xHasFirst && yHasFirst) {
                    return 1;
                }
                var xHasPhone = 0 != x.contact.PhoneNumbers.Count;
                var yHasPhone = 0 != y.contact.PhoneNumbers.Count;
                if (xHasPhone && !yHasPhone) {
                    return -1;
                }
                if (!xHasPhone && yHasPhone) {
                    return 1;
                }
                return 0;
            });

            // Remove duplicate e-mail addresses.
            var uniqueMatches = new List<SearchMatch> ();
            for (int i = 0; i < allMatches.Count; ++i) {
                if (0 == i || allMatches [i - 1].email != allMatches [i].email) {
                    uniqueMatches.Add (allMatches [i]);
                }
            }
            allMatches = uniqueMatches;

            // Sort the results by how well they match the search string.
            allMatches.Sort ((SearchMatch x, SearchMatch y) => {
                return y.matchScore - x.matchScore;
            });

            var result = allMatches.Select (x => x.attribute).ToList ();
            if (!result.Equals (previousResults)) {
                UpdateUi (searchString, result);
            }
            previousResults = result;
        }

        protected override void IncorporateServerResults (string searchString)
        {
            // The database fields that identify a contact that came from a server GAL search are not indexed.
            // So it is faster to repeat the entire search than to search just the GAL contacts.
            DoSearch (searchString);
        }
    }

    /// <summary>
    /// Search all contacts in all accounts.  Dispose() must be called when searching has completed.
    /// </summary>
    public class ContactsGeneralSearch : ContactsSearchCommon
    {
        List<McContactEmailAddressAttribute> previousResults = null;

        string lastIndexSearchString = null;
        List<MatchedItem> lastIndexResults = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="NachoCore.Utils.ContactsGeneralSearch"/> class.
        /// </summary>
        /// <param name="updateUi">Code that will be called on the UI thread when search results are available.</param>
        public ContactsGeneralSearch (UpdateUiAction updateUi)
            : base (updateUi)
        {
        }

        protected override void DoSearch (string searchString)
        {
            var searchWords = searchString.Trim ().Split (new char [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var rawContacts = new List<McContact> ();
            var dbEmailMatches = new List<McContactEmailAddressAttribute> ();

            foreach (var searchWord in searchWords) {
                // Search the database for contacts whose name matches the search string.
                rawContacts.AddRange (McContact.SearchAllContactsByName (searchWord));
                // Search the database for e-mail addresses that match the search string.
                dbEmailMatches.AddRange (McContact.SearchAllContactEmail (searchWord));
            }

            // Add the contacts associated with the e-mail addresses that were found to the set of contacts.
            rawContacts.AddRange (McContact.QueryByIds (dbEmailMatches.Select (x => x.ContactId).Distinct ().ToList ()));

            // If the search string is only one or two characters, don't bother searching the index.
            // When the user types the first couple letters, we want the app to display some results
            // quickly, and searching the index can take a long time.
            if (2 < searchString.Length) {
                // This method can be called multiple times with the same string as search results
                // arrive from the servers.  The database might change between those calls, but the
                // index won't.  So cache the results of one index search, and reuse that if the
                // search string hasn't changed.
                List<MatchedItem> indexMatches;
                if (searchString == lastIndexSearchString) {
                    indexMatches = lastIndexResults;
                } else {
                    indexMatches = new List<MatchedItem> ();
                    foreach (var account in McAccount.QueryByAccountCapabilities (McAccount.AccountCapabilityEnum.ContactReader)) {
                        var index = Indexer.Instance.IndexForAccount (account.Id);
                        indexMatches.AddRange (index.SearchAllContactFields (searchString, maxMatches: 100));
                    }
                    lastIndexSearchString = searchString;
                    lastIndexResults = indexMatches;
                }

                rawContacts.AddRange (McContact.QueryByIds (indexMatches.Select (x => x.Id).Distinct ().ToList ()));
            }

            var allMatches = new List<SearchMatch> ();
            var matchedContactIds = new HashSet<int> ();

            // Convert the contacts that were found into SearchMatch objects.
            foreach (var rawContact in rawContacts) {
                if (!rawContact.IsAwaitingDelete && matchedContactIds.Add (rawContact.Id)) {
                    if (0 == rawContact.EmailAddresses.Count) {
                        // Create a dummy e-mail attribute object
                        allMatches.Add (new SearchMatch (rawContact, new McContactEmailAddressAttribute () {
                            AccountId = rawContact.AccountId,
                            ContactId = rawContact.Id,
                            Value = "",
                        }));
                    } else {
                        // Create SearchMatch objects for all of the contact's e-mail addresses.
                        // All but one of them will be filtered out later, but we need SearchMatch
                        // objects for all of them now so we can figure out which one best matches
                        // the search string.
                        foreach (var emailAttribute in rawContact.EmailAddresses) {
                            allMatches.Add (new SearchMatch (rawContact, emailAttribute));
                        }
                    }
                }
            }

            // Score each match to see how well it matches the search string
            foreach (var match in allMatches) {
                int score = 0;
                foreach (var word in searchWords) {
                    if (StartsWith (match.contact.LastName, word)) {
                        score += 5 * word.Length;
                    } else if (StartsWith (match.contact.FirstName, word)) {
                        score += 4 * word.Length;
                    } else if (StartsWith (match.contact.CompanyName, word)) {
                        score += 3 * word.Length;
                    } else if (StartsWith (match.email, word)) {
                        score += 3 * word.Length;
                    } else if (Contains (match.email, "@" + word)) {
                        score += 2 * word.Length;
                    }
                }
                if (1 < searchWords.Length) {
                    if (Contains (match.contact.LastName, searchString)) {
                        score += 10 * searchString.Length;
                    } else if (Contains (match.contact.FirstName, searchString)) {
                        score += 8 * searchString.Length;
                    } else if (Contains (match.contact.CompanyName, searchString)) {
                        score += 6 * searchString.Length;
                    }
                }
                if (0 != match.contact.PortraitId) {
                    score += 1;
                }
                match.matchScore = score;
            }

            // Sort by contact.  Within each contact, sort the e-mail addresses by how well they match
            // the search string and then whether or not they are the default e-mail address.
            allMatches.Sort ((SearchMatch x, SearchMatch y) => {
                if (x.contact.Id != y.contact.Id) {
                    return x.contact.Id - y.contact.Id;
                }
                if (x.matchScore != y.matchScore) {
                    return y.matchScore - x.matchScore;
                }
                if (x.attribute.IsDefault && !y.attribute.IsDefault) {
                    return -1;
                }
                if (!x.attribute.IsDefault && y.attribute.IsDefault) {
                    return 1;
                }
                return 0;
            });

            // Eliminate duplicate entries for the same contact, so that each contact has only
            // one e-mail address in the results.
            var uniqueContacts = new List<SearchMatch> ();
            for (int i = 0; i < allMatches.Count; ++i) {
                if (0 == i || allMatches [i - 1].contact.Id != allMatches [i].contact.Id) {
                    uniqueContacts.Add (allMatches [i]);
                }
            }
            allMatches = uniqueContacts;

            // Sort by e-mail address.  For contacts with the same e-mail address, sort by name,
            // then by the quality of the source of the contact.  (E.g. Synched contacts are
            // preferred over gleaned contacts.)
            allMatches.Sort ((SearchMatch x, SearchMatch y) => {
                int emailCompare = string.Compare (x.email, y.email);
                if (0 != emailCompare) {
                    return emailCompare;
                }
                bool xHasName = HasName (x.contact);
                bool yHasName = HasName (y.contact);
                if (xHasName && !yHasName) {
                    return -1;
                }
                if (!xHasName && yHasName) {
                    return 1;
                }
                int lastCompare = string.Compare (x.contact.LastName, y.contact.LastName);
                if (0 != lastCompare) {
                    return lastCompare;
                }
                int firstCompare = string.Compare (x.contact.FirstName, y.contact.FirstName);
                if (0 != firstCompare) {
                    return firstCompare;
                }
                return ContactSourceScore (y.contact) - ContactSourceScore (x.contact);
            });

            // Eliminate contacts that appear to be duplicates.  A duplicate is
            // (1) not a synched contact or a device contact
            // (2) has the same e-mail address
            // (3) has the same name, or doesn't have a name.
            uniqueContacts = new List<SearchMatch> ();
            for (int i = 0; i < allMatches.Count; ++i) {
                var curr = allMatches [i];
                if (0 == i || curr.contact.IsSynced () || curr.contact.IsDevice ()) {
                    uniqueContacts.Add (allMatches [i]);
                } else {
                    var prev = allMatches [i - 1];
                    if (string.IsNullOrEmpty (curr.email) || curr.email != prev.email) {
                        uniqueContacts.Add (curr);
                    } else if (HasName (curr.contact) && !SameName (curr.contact, prev.contact)) {
                        uniqueContacts.Add (curr);
                    }
                }
            }
            allMatches = uniqueContacts;

            // Sort the resulting list by how well it matches the search string
            allMatches.Sort ((SearchMatch x, SearchMatch y) => {
                return y.matchScore - x.matchScore;
            });

            var result = allMatches.Select (x => x.attribute).ToList ();
            if (!result.Equals (previousResults)) {
                UpdateUi (searchString, result);
            }
            previousResults = result;
        }

        protected override void IncorporateServerResults (string searchString)
        {
            // We want to redo the database search, but not the index search.  But DoSearch() does that automatically.
            DoSearch (searchString);
        }
    }
}
