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
            var analyzer = new PerFieldAnalyzerWrapper (new StandardAnalyzer (NcIndex.LuceneVersion));
            analyzer.AddAnalyzer (EmailFieldName, new KeywordAnalyzer ());
            analyzer.AddAnalyzer (DomainFieldName, new KeywordAnalyzer ());
            analyzer.AddAnalyzer (PhoneFieldName, new KeywordAnalyzer ());
            // TODO: customize per field
            return analyzer;
        }

        public readonly static Analyzer Analyzer = CreateAnalyzer ();

        public static Query GeneralQuery (string userQueryString)
        {
            return new BooleanQuery {
                { TypeQuery (), Occur.MUST },
                { new BooleanQuery {
                        { FieldQuery (NameFieldName, userQueryString), Occur.SHOULD },
                        { FieldQuery (AliasFieldName, userQueryString), Occur.SHOULD },
                        { FieldQuery (NicknameFieldName, userQueryString), Occur.SHOULD },
                        { FieldQuery (CompanyFieldName, userQueryString), Occur.SHOULD },
                        { FieldQuery (EmailFieldName, userQueryString), Occur.SHOULD },
                        { FieldQuery (DomainFieldName, userQueryString), Occur.SHOULD },
                        { FieldQuery (PhoneFieldName, userQueryString), Occur.SHOULD },
                        { FieldQuery (AddressFieldName, userQueryString), Occur.SHOULD },
                        { FieldQuery (NoteFieldName, userQueryString), Occur.SHOULD },
                }, Occur.MUST },
            };
        }

        public static Query GeneralAccountQuery (int accountId, string userQueryString)
        {
            var query = GeneralQuery (userQueryString) as BooleanQuery;
            query.Add (new TermQuery (new Term (NcIndex.AccountIdFieldName, accountId.ToString ())), Occur.MUST);
            return query;
        }

        public static Query NameAndEmailQuery (string userQueryString)
        {
            return new BooleanQuery {
                { TypeQuery (), Occur.MUST },
                { HasEmailQuery (), Occur.MUST },
                { new BooleanQuery {
                        { FieldQuery (NameFieldName, userQueryString), Occur.SHOULD },
                        { FieldQuery (AliasFieldName, userQueryString), Occur.SHOULD },
                        { FieldQuery (NicknameFieldName, userQueryString), Occur.SHOULD },
                        { FieldQuery (CompanyFieldName, userQueryString), Occur.SHOULD },
                        { FieldQuery (EmailFieldName, userQueryString), Occur.SHOULD },
                        { FieldQuery (DomainFieldName, userQueryString), Occur.SHOULD },
                }, Occur.MUST },
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

        static Query FieldQuery (string fieldName, string userQueryString)
        {
            var reader = new StringReader (userQueryString);
            // While some of our fields use a keyword analyzer, we always want to
            // parse the query into multiple tokens, and then match each query token
            // to the indexed tokens.  This allows a search for "some person" to still
            // match an email address that has been tokenized as the single keyword
            // somep@company.com, because the "some" token matches the prefix.  If we 
            // instead used a keyword analyzer for the query, we'd get back "some person"
            // as the only token, and it would not match somep@company.com.
            var analyzer = new StandardAnalyzer (NcIndex.LuceneVersion);
            var stream = analyzer.TokenStream (fieldName, reader);
            var termAttribute = stream.AddAttribute<ITermAttribute> ();
            stream.Reset ();
            var query = new BooleanQuery ();
            while (stream.IncrementToken ()) {
                query.Add (new PrefixQuery (new Term (fieldName, termAttribute.Term)), Occur.SHOULD);
            }
            return query;
        }

    }
}
