//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Search;

using NachoCore.Model;

namespace NachoCore.Index
{
    public class EmailMessageDocument
    {
        public const int Version = 10;
        public readonly Document Document;
        public readonly float Score;

        public const string DocumentType = "message";

        public const string MessageIdFieldName = "messageid";
        public const string SubjectFieldName = "subject";
        public const string BodyFieldName = "body";

        public readonly Field DocumentId;
        public readonly Field Type;
        public readonly Field AccountId;
        public readonly Field MessageId;
        public readonly Field Subject;
        public readonly Field Body;

        public EmailMessageDocument (McEmailMessage message)
        {
            Document = new Document ();
            Score = 0.0f;

            DocumentId = new Field (NcIndex.DocumentIdFieldName, message.GetIndexDocumentId (), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS, Field.TermVector.NO);
            Type = new Field (NcIndex.DocumentTypeFieldName, DocumentType, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS, Field.TermVector.NO);
            AccountId = new Field (NcIndex.AccountIdFieldName, message.AccountId.ToString (), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS, Field.TermVector.NO);
            MessageId = new Field (MessageIdFieldName, message.Id.ToString (), Field.Store.YES, Field.Index.NO, Field.TermVector.NO);
            Subject = new Field (SubjectFieldName, message.Subject ?? "", Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.NO);
            Body = new Field (BodyFieldName, message.GetIndexContent () ?? "", Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.NO);

            Document.Add (DocumentId);
            Document.Add (Type);
            Document.Add (AccountId);
            Document.Add (MessageId);
            Document.Add (Subject);
            Document.Add (Body);
        }

        public EmailMessageDocument (Document document, float score = 0.0f)
        {
            Document = document;
            Score = score;
            DocumentId = Document.GetField (NcIndex.DocumentIdFieldName);
            Type = document.GetField (NcIndex.DocumentTypeFieldName);
            AccountId = document.GetField (NcIndex.AccountIdFieldName);
            MessageId = document.GetField (MessageIdFieldName);
        }

        static Analyzer CreateAnalyzer ()
        {
            var stopWords = new HashSet<string> ();
            return new StandardAnalyzer (NcIndex.LuceneVersion, stopWords: stopWords);
        }

        public readonly static Analyzer Analyzer = CreateAnalyzer ();

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

        public int IntegerMessageId {
            get {
                return int.Parse (MessageId.StringValue);
            }
        }

        static Query TypeQuery ()
        {
            return new TermQuery (new Term (NcIndex.DocumentTypeFieldName, DocumentType));
        }

        static Query AccountQuery (int accountId)
        {
            return new TermQuery (new Term (NcIndex.AccountIdFieldName, accountId.ToString ()));
        }

        public static Query ContentQuery (string userQueryString, out string [] parsedTokens, int accountId = 0)
        {
            // First we want to parse the query into tokens, and the easiest way is to use
            // the standard analyzer, but one without any stop words so nothing the user typed
            // is thrown away.  This is important because our final token will be used as
            // a prefix query, so we want it even if it appears to be a stop word as typed.
            var stopWords = new HashSet<string> ();
            var analyzer = new StandardAnalyzer (NcIndex.LuceneVersion, stopWords: stopWords);
            var tokens = analyzer.TokenizeQueryString (userQueryString);
            parsedTokens = tokens.ToArray ();
            if (tokens.Count == 0) {
                return null;
            }

            var lastToken = tokens [tokens.Count - 1];
            tokens.RemoveAt (tokens.Count - 1);

            // Find documents that
            // 1. Are messages
            // 2. Match the account (if nonzero)
            // 3. Have a subject OR body that contains all of the terms
            // 4. The final term is treated as a prefix for live searching ability
            var query = new BooleanQuery {
                { TypeQuery (), Occur.MUST }
            };
            if (accountId != 0) {
                query.Add (AccountQuery (accountId), Occur.MUST);
            }
            query.Add (new BooleanQuery {
                {FieldQuery(SubjectFieldName, tokens, lastToken), Occur.SHOULD},
                {FieldQuery(BodyFieldName, tokens, lastToken), Occur.SHOULD}
            }, Occur.MUST);
            return query;
        }

        static Query FieldQuery (string fieldName, IEnumerable<string> tokens, string lastToken)
        {
            var query = new BooleanQuery ();
            foreach (var token in tokens) {
                query.Add (new TermQuery (new Term (fieldName, token)), Occur.MUST);
            }
            query.Add (new PrefixQuery (new Term (fieldName, lastToken)), Occur.MUST);
            return query;
        }

    }
}
