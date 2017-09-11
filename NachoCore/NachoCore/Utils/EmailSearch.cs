//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoCore.Index;

namespace NachoCore.Utils
{

    class EmailSearchResults
    {
        public string Query;
        public string [] Tokens;
        public readonly int [] ContactIds;
        public readonly int [] MessageIds;

        public EmailSearchResults (string query, string [] tokens, int [] contactIds, int [] messageIds)
        {
            Query = query;
            Tokens = tokens;
            ContactIds = contactIds;
            MessageIds = messageIds;
        }
    }

    class EmailSearcher : Searcher<EmailSearchResults>
    {
        public McAccount Account;
        public int MaxContactResults = 5;
        public int MaxMessageResults = 100;

        protected override EmailSearchResults SearchResults (string query)
        {
            var accountId = (Account?.AccountType ?? McAccount.AccountTypeEnum.Unified) == McAccount.AccountTypeEnum.Unified ? 0 : Account.Id;
            var contactResults = NcIndex.Main.SearchContactsNameAndEmails (query, maxResults: MaxContactResults);
            var messageResults = NcIndex.Main.SearchEmails (query, accountId: accountId, maxResults: MaxMessageResults);
            var contactIds = contactResults.Documents.Select (r => r.IntegerContactId).ToArray ();
            var messageIds = messageResults.Documents.Select (r => r.IntegerMessageId).ToArray ();
            return new EmailSearchResults (query, contactResults.ParsedTokens, contactIds, messageIds);
        }
    }

    class EmailServerSearcher : ServerSearcher<int []>
    {
        public McAccount Account;

        protected override Dictionary<int, string> CreateServerSearchTokens (string query)
        {
            var tokens = new Dictionary<int, string> ();
            if (Account.AccountType == McAccount.AccountTypeEnum.Unified) {
                var accounts = McAccount.QueryByAccountCapabilities (McAccount.AccountCapabilityEnum.EmailReaderWriter).Where (x => x.AccountType != McAccount.AccountTypeEnum.Unified).ToList ();
                foreach (var account in accounts) {
                    tokens [account.Id] = BackEnd.Instance.StartSearchEmailReq (account.Id, query, null).GetValue<string> ();
                }
            } else {
                tokens [Account.Id] = BackEnd.Instance.StartSearchEmailReq (Account.Id, query, null).GetValue<string> ();
            }
            return tokens;
        }

        protected override NcResult.SubKindEnum SuccessStatus {
            get {
                return NcResult.SubKindEnum.Info_EmailSearchCommandSucceeded;
            }
        }

        protected override NcResult.SubKindEnum ErrorStatus {
            get {
                return NcResult.SubKindEnum.Error_EmailSearchCommandFailed;
            }
        }

        public override void Search (string query)
        {
            ResultsFromAllAccounts.Clear ();
            base.Search (query);
        }

        readonly List<int> ResultsFromAllAccounts = new List<int> ();

        protected override void HandleStatus (NcResult status)
        {
            var messageIds = status.GetValue<List<NcEmailMessageIndex>> ().Select (x => x.Id);
            ResultsFromAllAccounts.AddRange (messageIds);
            FoundResults (ResultsFromAllAccounts.ToArray ());
        }
    }

}

