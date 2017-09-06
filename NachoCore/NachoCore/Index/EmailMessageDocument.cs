//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Search;
using Lucene.Net.Analysis.Tokenattributes;

using NachoCore.Model;

namespace NachoCore.Index
{
    public class EmailMessageDocument
    {
        public const int Version = 1;
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
            var analyzer = new PerFieldAnalyzerWrapper (new StandardAnalyzer (NcIndex.LuceneVersion));
            // TODO: customize per field
            return analyzer;
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

        public static Query ContentQuery (string userQueryString, int accountId = 0)
        {
            var query = new BooleanQuery {
                { TypeQuery (), Occur.MUST },
                { FieldQuery (SubjectFieldName, userQueryString), Occur.MUST },
                { FieldQuery (BodyFieldName, userQueryString), Occur.MUST },
            };
            if (accountId != 0) {
                query.Add (AccountQuery (accountId), Occur.MUST);
            }
            return query;
        }

        static Query FieldQuery (string fieldName, string userQueryString)
        {
            var reader = new StringReader (userQueryString);
            var analyzer = new StandardAnalyzer (NcIndex.LuceneVersion);
            var stream = analyzer.TokenStream (fieldName, reader);
            var termAttribute = stream.AddAttribute<TermAttribute> ();
            stream.Reset ();
            var query = new BooleanQuery ();
            while (stream.IncrementToken ()) {
                query.Add (new PrefixQuery (new Term (fieldName, termAttribute.Term)), Occur.SHOULD);
            }
            return query;
        }

    }
}
