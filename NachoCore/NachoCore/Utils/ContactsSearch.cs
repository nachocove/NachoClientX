//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;

using NachoCore.Index;
using NachoCore.Model;
using NachoPlatform;
using System.Collections.Concurrent;

namespace NachoCore.Utils
{

    public class ContactSearchResults
    {
        public readonly string Query;
        public readonly string [] Tokens;
        public readonly int [] ContactIds;

        public ContactSearchResults (string query, string [] tokens, int [] contactIds)
        {
            Query = query;
            Tokens = tokens;
            ContactIds = contactIds;
        }
    }

    public abstract class LocalAndGlobalContactSearcher<ResultType> : Searcher<ResultType>
    {

        readonly ContactServerSearcher ServerSearcher = new ContactServerSearcher ();

        public override void Search (string query)
        {
            base.Search (query);
            // Kicking off a sever search will result in new McContacts being added to the
            // database and search index.  We use IndexChangedStatus to indicate that the
            // base searcher class should automatically re-run the latest query when the
            // contact index chages.  Because the server results will be added to our local index
            // and because we'll re-run the search automatically, there is no need to directly
            // handle results from the server searcher.
            if (query != null && query.Length > 2) {
                ServerSearcher.Search (query);
            }
        }

        public override void Cleanup ()
        {
            ServerSearcher.Cleanup ();
            base.Cleanup ();
        }

        /// <summary>
        /// The contacts index changes when we do a server search and contacts are added
        /// to the global address list (GAL) folder.  In such a case, we want the searcher
        /// to automatically re-run the last requested search.  Implementing this property
        /// tells the base Searcher class to watch for events of this sub kind, and re-run
        /// the latest requested search.
        /// </summary>
        /// <value>The index changed status.</value>
        protected override NcResult.SubKindEnum IndexChangedStatus {
            get {
                return NcResult.SubKindEnum.Info_ContactIndexUpdated;
            }
        }
    }

    public class ContactSearcher : LocalAndGlobalContactSearcher<ContactSearchResults>
    {

        public int MaxResults = 100;

        protected override ContactSearchResults SearchResults (string query)
        {
            var results = NcIndex.Main.SearchContacts (query, maxResults: MaxResults);
            var ids = results.Documents.Select (r => r.IntegerContactId).ToArray ();
            return new ContactSearchResults (query, results.ParsedTokens, ids);
        }
    }

    public class ContactServerSearcher : ServerSearcher<int []>
    {

        List<McAccount> _Accounts;
        List<McAccount> Accounts {
            get {
                if (_Accounts == null) {
                    _Accounts = McAccount.QueryByAccountCapabilities (McAccount.AccountCapabilityEnum.ContactReader).ToList ();
                }
                return _Accounts;
            }
        }

        protected override Dictionary<int, string> CreateServerSearchTokens (string query)
        {
            var tokens = new Dictionary<int, string> ();
            foreach (var account in Accounts) {
                tokens [account.Id] = BackEnd.Instance.StartSearchContactsReq (account.Id, query, null).GetValue<string> ();
            }
            return tokens;
        }

        protected override NcResult.SubKindEnum SuccessStatus {
            get {
                return NcResult.SubKindEnum.Info_ContactSearchCommandSucceeded;
            }
        }

        protected override NcResult.SubKindEnum ErrorStatus {
            get {
                return NcResult.SubKindEnum.Error_ContactSearchCommandFailed;
            }
        }

        protected override void HandleStatus (NcResult status)
        {
        }
    }

    public class EmailAutocompleteSearchResults
    {
        public readonly string Query;
        public readonly string [] Tokens;
        public readonly McContactEmailAddressAttribute [] EmailAttributes;

        public EmailAutocompleteSearchResults (string query, string [] tokens, McContactEmailAddressAttribute [] emailAttributes)
        {
            Query = query;
            Tokens = tokens;
            EmailAttributes = emailAttributes;
        }
    }

    public class EmailAutocompleteSearcher : LocalAndGlobalContactSearcher<EmailAutocompleteSearchResults>
    {

        public int MaxResults = 100;

        protected override EmailAutocompleteSearchResults SearchResults (string query)
        {
            var emailAttributes = new List<McContactEmailAddressAttribute> ();
            var foundAttributeIds = new HashSet<int> ();

            var results = NcIndex.Main.SearchContactsNameAndEmails (query, maxResults: MaxResults);
            var contactIds = results.Documents.Select (r => r.IntegerContactId);
            var remainingResults = MaxResults;
            var tokens = results.ParsedTokens;
            foreach (var contactId in contactIds) {
                var contactEmails = McContactEmailAddressAttribute.QueryByContactId<McContactEmailAddressAttribute> (contactId);
                contactEmails.Sort ((x, y) => {
                    return Convert.ToInt32 (y.MatchesTokens (tokens)) - Convert.ToInt32 (x.MatchesTokens (tokens));
                });
                emailAttributes.AddRange (contactEmails);
                remainingResults -= contactEmails.Count;
                if (remainingResults <= 0) {
                    break;
                }
            }
            return new EmailAutocompleteSearchResults (query, tokens, emailAttributes.ToArray ());
        }
    }

}
