//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using Lucene.Net.Documents;
using Lucene.Net.Search;
using Lucene.Net.Index;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Analysis.Standard;

using NachoCore.Model;

namespace NachoCore.Index
{
    public class ContactDocument
    {
        public const int Version = 10;
        public readonly Document Document;
        public readonly float Score;

        public const string DocumentType = "contact";

        public const string ContactIdFieldName = "messageid";
        public const string NameFieldName = "name";
        public const string AliasFieldName = "alias";
        public const string NicknameFieldName = "nickname";
        public const string CompanyFieldName = "company";
        public const string HasEmailFieldName = "hasemail";
        public const string PhoneFieldName = "phone";
        public const string EmailFieldName = "email";
        public const string DomainFieldName = "domain";
        public const string AddressFieldName = "address";
        public const string NoteFieldName = "note";
        public const string HasEmailTrueValue = "yes";
        public const string HasEmailFalseValue = "no";

        public readonly Field DocumentId;
        public readonly Field Type;
        public readonly Field AccountId;
        public readonly Field ContactId;
        public readonly Field Name;
        public readonly Field Alias;
        public readonly Field Nickname;
        public readonly Field Company;
        public readonly Field HasEmail;
        public readonly Field [] Phones;
        public readonly Field [] Emails;
        public readonly Field [] Domains;
        public readonly Field [] Addresses;
        public readonly Field Note;

        public ContactDocument (McContact contact)
        {
            Document = new Document ();
            Score = 0.0f;

            DocumentId = new Field (NcIndex.DocumentIdFieldName, contact.GetIndexDocumentId (), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS, Field.TermVector.NO);
            Type = new Field (NcIndex.DocumentTypeFieldName, DocumentType, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS, Field.TermVector.NO);
            AccountId = new Field (NcIndex.AccountIdFieldName, contact.AccountId.ToString (), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS, Field.TermVector.NO);
            ContactId = new Field (ContactIdFieldName, contact.Id.ToString (), Field.Store.YES, Field.Index.NO, Field.TermVector.NO);
            Name = new Field (NameFieldName, contact.FullName ?? "", Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.NO);
            Alias = new Field (AliasFieldName, contact.Alias ?? "", Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.NO);
            Nickname = new Field (NicknameFieldName, contact.NickName ?? "", Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.NO);
            Company = new Field (CompanyFieldName, contact.CompanyName ?? "", Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.NO);

            Document.Add (DocumentId);
            Document.Add (Type);
            Document.Add (AccountId);
            Document.Add (ContactId);
            Document.Add (Name);
            Document.Add (Alias);
            Document.Add (Nickname);
            Document.Add (Company);

            var phones = new List<Field> ();
            foreach (var attr in contact.PhoneNumbers) {
                if (!String.IsNullOrEmpty (attr.Value)) {
                    var phone = new Field (PhoneFieldName, attr.Value, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.NO);
                    Document.Add (phone);
                    phones.Add (phone);
                }
            }
            Phones = phones.ToArray ();

            var emails = new List<Field> ();
            var domains = new List<Field> ();
            foreach (var attr in contact.EmailAddresses) {
                if (Mailbox.TryParse (attr.Value, out var mailbox)) {
                    var email = new Field (EmailFieldName, mailbox.Address, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.NO);
                    Document.Add (email);
                    emails.Add (email);
                    var domain = new Field (DomainFieldName, mailbox.Domain, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.NO);
                    Document.Add (domain);
                    domains.Add (domain);
                }
            }
            Emails = emails.ToArray ();
            Domains = domains.ToArray ();

            HasEmail = new Field (HasEmailFieldName, Emails.Length > 0 ? HasEmailTrueValue : HasEmailFalseValue, Field.Store.NO, Field.Index.NOT_ANALYZED_NO_NORMS, Field.TermVector.NO);
            Document.Add (HasEmail);

            var addresses = new List<Field> ();
            foreach (var attr in contact.Addresses) {
                var address = new Field (AddressFieldName, attr.FormattedAddress, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.NO);
                Document.Add (address);
                addresses.Add (address);
            }
            Addresses = addresses.ToArray ();

            var note = contact.GetNote ();
            if (note != null) {
                Note = new Field (NoteFieldName, note, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.NO);
                Document.Add (Note);
            }
        }

        public ContactDocument (Document document, float score = 0.0f)
        {
            Document = document;
            Score = score;
            DocumentId = Document.GetField (NcIndex.DocumentIdFieldName);
            Type = Document.GetField (NcIndex.DocumentTypeFieldName);
            AccountId = Document.GetField (NcIndex.AccountIdFieldName);
            ContactId = Document.GetField (ContactIdFieldName);
        }

        public Term DocumentIdTerm {
            get {
                return new Term (NcIndex.DocumentIdFieldName, DocumentId.StringValue);
            }
        }

        public int IntegerAccountId {
            get {
                return int.Parse (AccountId.StringValue);
            }
        }

        public int IntegerContactId {
            get {
                return int.Parse (ContactId.StringValue);
            }
        }

        static Analyzer CreateAnalyzer ()
        {
            var stopWords = new HashSet<string> ();
            var analyzer = new PerFieldAnalyzerWrapper (new StandardAnalyzer (NcIndex.LuceneVersion, stopWords: stopWords));
            analyzer.AddAnalyzer (EmailFieldName, new KeywordAnalyzer ());
            analyzer.AddAnalyzer (DomainFieldName, new KeywordAnalyzer ());
            analyzer.AddAnalyzer (PhoneFieldName, new KeywordAnalyzer ());
            return analyzer;
        }

        public readonly static Analyzer Analyzer = CreateAnalyzer ();

        public static Query GeneralQuery (string userQueryString, out string [] parsedTokens)
        {
            // First we want to parse the query into tokens, and the easiest way is to use
            // the standard analyzer, but one without any stop words so nothing the user typed
            // is thrown away.  This is important because all of our tokens will be used as
            // prefix queries, so they're useful even if they appear to be stop words as typed.
            var stopWords = new HashSet<string> ();
            var analyzer = new StandardAnalyzer (NcIndex.LuceneVersion, stopWords: stopWords);
            var tokens = analyzer.TokenizeQueryString (userQueryString);
            parsedTokens = tokens.ToArray ();
            if (tokens.Count == 0) {
                return null;
            }
            var fieldNames = new string [] {
                NameFieldName,
                AliasFieldName,
                NicknameFieldName,
                CompanyFieldName,
                EmailFieldName,
                DomainFieldName,
                PhoneFieldName,
                AddressFieldName,
                NoteFieldName
            };
            return new BooleanQuery {
                { TypeQuery (), Occur.MUST },
                { FieldsQuery (fieldNames, tokens), Occur.MUST},
            };
        }

        public static Query GeneralAccountQuery (int accountId, string userQueryString, out string [] parsedTokens)
        {
            var query = GeneralQuery (userQueryString, out parsedTokens) as BooleanQuery;
            query.Add (new TermQuery (new Term (NcIndex.AccountIdFieldName, accountId.ToString ())), Occur.MUST);
            return query;
        }

        public static Query NameAndEmailQuery (string userQueryString, out string [] parsedTokens)
        {
            // While some of our fields use a keyword analyzer, we always want to
            // parse the query into multiple tokens, and then match each query token
            // to the indexed tokens.  This allows a search for "some person" to still
            // match an email address that has been tokenized as the single keyword
            // somep@company.com, because the "some" token matches the prefix.  If we 
            // instead used a keyword analyzer for the query, we'd get back "some person"
            // as the only token, and it would not match somep@company.com.
            var stopWords = new HashSet<string> ();
            var analyzer = new StandardAnalyzer (NcIndex.LuceneVersion, stopWords: stopWords);
            var tokens = analyzer.TokenizeQueryString (userQueryString);
            parsedTokens = tokens.ToArray ();
            if (tokens.Count == 0) {
                return null;
            }
            var fieldNames = new string [] {
                NameFieldName,
                AliasFieldName,
                NicknameFieldName,
                CompanyFieldName,
                EmailFieldName,
                DomainFieldName
            };
            return new BooleanQuery {
                { TypeQuery (), Occur.MUST },
                { HasEmailQuery (), Occur.MUST },
                { FieldsQuery (fieldNames, tokens), Occur.MUST},
            };
        }

        public static Query NameQuery (string userQueryString, out string [] parsedTokens)
        {
            var stopWords = new HashSet<string> ();
            var analyzer = new StandardAnalyzer (NcIndex.LuceneVersion, stopWords: stopWords);
            var tokens = analyzer.TokenizeQueryString (userQueryString);
            parsedTokens = tokens.ToArray ();
            if (tokens.Count == 0) {
                return null;
            }
            var fieldNames = new string [] {
                NameFieldName,
                AliasFieldName,
                NicknameFieldName,
                CompanyFieldName,
            };
            return new BooleanQuery {
                { TypeQuery (), Occur.MUST },
                { HasEmailQuery (), Occur.MUST },
                { FieldsQuery (fieldNames, tokens), Occur.MUST},
            };
        }

        static Query TypeQuery ()
        {
            return new TermQuery (new Term (NcIndex.DocumentTypeFieldName, DocumentType));
        }

        static Query HasEmailQuery ()
        {
            return new TermQuery (new Term (HasEmailFieldName, HasEmailTrueValue));
        }

        /// <summary>
        /// Create a query that requires every token to appear somewhere across all the fields.
        /// </summary>
        /// <returns>The query.</returns>
        /// <param name="fieldNames">Field names.</param>
        /// <param name="tokens">Tokens.</param>
        static Query FieldsQuery (IEnumerable<string> fieldNames, IEnumerable<string> tokens)
        {
            var query = new BooleanQuery ();
            foreach (var token in tokens) {
                var tokenQuery = new BooleanQuery ();
                foreach (var fieldName in fieldNames) {
                    tokenQuery.Add (new PrefixQuery (new Term (fieldName, token)), Occur.SHOULD);
                }
                query.Add (tokenQuery, Occur.MUST);
            }
            return query;
        }

        static Query NameQuery (string fieldName, IEnumerable<string> tokens)
        {
            var query = new BooleanQuery ();
            foreach (var token in tokens) {
                query.Add (new PrefixQuery (new Term (fieldName, token)), Occur.MUST);
            }
            return query;
        }

        static Query EmailQuery (string fieldName, IEnumerable<string> tokens)
        {
            var query = new BooleanQuery ();
            foreach (var token in tokens) {
                query.Add (new PrefixQuery (new Term (fieldName, token)), Occur.SHOULD);
            }
            return query;
        }

    }
}
