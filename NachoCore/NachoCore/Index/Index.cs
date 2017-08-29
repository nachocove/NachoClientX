//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Store;
using Lucene.Net.Index;
using Lucene.Net.Documents;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using System.Text.RegularExpressions;

namespace NachoCore.Index
{
    public class NcIndex : IDisposable
    {
        public const int KTimeoutMsec = 5000;

        #region Getting an Index

        public NcIndex (string indexDirectoryPath)
        {
            Analyzer = new StandardAnalyzer (Lucene.Net.Util.Version.LUCENE_30);
            IndexDirectory = FSDirectory.Open (indexDirectoryPath);
            Lock = new Mutex ();
        }

        public readonly StandardAnalyzer Analyzer;
        public readonly FSDirectory IndexDirectory;

        private Mutex Lock;

        #endregion

        #region Index Status

        private bool Deleted;

        #endregion

        #region Searching Emails

        public List<MatchedItem> SearchAllEmailMessageFields (string queryString, int maxMatches = 1000)
        {
            return SearchFields ("message", queryString, new string [] {
                "body",
                "from",
                "to",
                "cc",
                "bcc",
                "from_domain",
                "to_domain",
                "cc_domain",
                "bcc_domain",
                "subject",
                "preview",
            }, maxMatches, false);
        }

        #endregion

        #region Searching Contacts

        public List<MatchedItem> SearchAllContactFields (string queryString, int maxMatches = 1000)
        {
            return SearchFields ("contact", queryString, new string [] {
                "first_name",
                "middle_name",
                "last_name",
                "company_name",
                "email_address",
                "email_domain",
                "phone_number",
                "address",
                "note"
            }, maxMatches, false);
        }

        #endregion

        #region Adding Items

        public AddingTransaction AddingTransaction ()
        {
            if (!Lock.WaitOne (KTimeoutMsec)) {
                return null;
            }
            if (Deleted) {
                Lock.ReleaseMutex ();
                return null;
            }
            return new AddingTransaction (this, Lock);
        }

        // Add() is significantly slower when inserting multiple items than using a transaction
        // So, if you are going to use more than one item, please use the later combination.
        public long Add (NcIndexDocument doc)
        {
            long bytesIndexed = 0;
            using (var transaction = AddingTransaction ()) {
                if (transaction != null) {
                    bytesIndexed += transaction.Add (doc);
                    transaction.Commit ();
                }
            }
            return bytesIndexed;
        }

        #endregion

        #region Removing Items

        public RemovingTransaction RemovingTransaction ()
        {
            if (!Lock.WaitOne (KTimeoutMsec)) {
                return null;
            }
            if (Deleted) {
                Lock.ReleaseMutex ();
                return null;
            }
            try {
                return new Index.RemovingTransaction (this, Lock);
            } catch (NoSuchDirectoryException) {
                // This can happen if the removal is done before anything is written to the index.
                Lock.ReleaseMutex ();
                return null;
            }
        }

        // Remove() is significantly slower than BeginRemoveTransaction() + BatchRemove() + EndRemoveTransaction()
        // So, if you are going to remove more than one item, please use the later combination.
        public bool Remove (string type, string id)
        {
            var removed = true;
            using (var transaction = RemovingTransaction ()) {
                if (transaction != null) {
                    if (!transaction.Remove (type, id)) {
                        removed = false;
                    }
                    transaction.Commit ();
                }
            }
            return removed;
        }

        #endregion

        #region Disposable interface

        bool IsDisposed;

        public void Dispose ()
        {
            if (!IsDisposed) {
                Analyzer.Dispose ();
                IndexDirectory.Dispose ();
                IsDisposed = true;
            }
        }

        #endregion

        #region Deleting the entire index

        public bool MarkForDeletion ()
        {
            if (!Lock.WaitOne (KTimeoutMsec)) {
                return false;
            }
            Deleted = true;
            Lock.ReleaseMutex ();
            return true;
        }

        #endregion

        #region Private Helpers

        public List<MatchedItem> SearchFields (string type, string queryString, string [] fields, int maxMatches = 1000, bool leadingWildcard = false)
        {
            string newQueryString = "";
            queryString = queryString
                .Replace ("*", " ")
                .Replace ("?", " ")
                .Trim ();
            queryString = Regex.Replace (queryString, @"\s+", " ");
            if (String.IsNullOrEmpty (queryString)) {
                return new List<MatchedItem> ();
            }

            var queryTokens = queryString.Split ().Select (x => (leadingWildcard ? "*" : "") + QueryParser.Escape (x) + "*");
            var fieldQuery = String.Join (" ", queryTokens);

            if (null != type) {
                newQueryString += "+type:" + type;
            }
            newQueryString += " +(";
            foreach (var f in fields) {
                newQueryString += f + ":(" + fieldQuery + ") ";
            }
            newQueryString += ")";
            return Search (newQueryString, maxMatches, leadingWildcard);
        }

        public List<MatchedItem> Search (string queryString, int maxMatches = 1000, bool leadingWildcard = false)
        {
            List<MatchedItem> matchedItems = new List<MatchedItem> ();
            try {
                using (var reader = IndexReader.Open (IndexDirectory, true)) {
                    var parser = new QueryParser (Lucene.Net.Util.Version.LUCENE_30, "body", Analyzer);
                    parser.AllowLeadingWildcard = leadingWildcard;
                    var query = parser.Parse (queryString);
                    var searcher = new IndexSearcher (reader);
                    var matches = searcher.Search (query, maxMatches);
                    foreach (var scoreDoc in matches.ScoreDocs) {
                        matchedItems.Add (new MatchedItem (searcher.Doc (scoreDoc.Doc), scoreDoc.Score));
                    }
                }
            } catch (Lucene.Net.Store.NoSuchDirectoryException) {
                // This can happen if a search is done before anything is written to the index.
            }
            return matchedItems;
        }

        #endregion
    }

    public class AddingTransaction : IDisposable
    {

        readonly IndexWriter Writer;
        bool NeedsCommit;
        readonly Mutex IndexLock;

        public AddingTransaction (NcIndex index, Mutex indexLock)
        {
            Writer = new IndexWriter (index.IndexDirectory, index.Analyzer, IndexWriter.MaxFieldLength.UNLIMITED);
            IndexLock = indexLock;
        }

        public void Commit ()
        {
            if (NeedsCommit) {
                Writer.Commit ();
                NeedsCommit = false;
            }
        }

        public void Dispose ()
        {
            Writer.Dispose ();
            IndexLock.ReleaseMutex ();
        }

        public long Add (NcIndexDocument document)
        {
            Writer.AddDocument (document.Doc);
            NeedsCommit = true;
            return document.BytesIndexed;
        }

    }

    public class RemovingTransaction : IDisposable
    {

        readonly IndexReader Reader;
        readonly Mutex IndexLock;
        bool NeedsCommit;
        NcIndex Index;

        public RemovingTransaction (NcIndex index, Mutex indexLock)
        {
            Index = index;
            IndexLock = indexLock;
            Reader = IndexReader.Open (Index.IndexDirectory, false);
        }

        public void Commit ()
        {
            if (NeedsCommit) {
                Reader.Commit ();
                NeedsCommit = false;
            }
        }

        public bool Remove (string type, string id)
        {
            var parser = new QueryParser (Lucene.Net.Util.Version.LUCENE_30, "id", Index.Analyzer);
            var queryString = String.Format ("type:{0} AND id:{1}", type, id);
            var query = parser.Parse (queryString);
            var searcher = new IndexSearcher (Reader);
            var matches = searcher.Search (query, 2);
            if (matches.TotalHits != 1) {
#if INDEX_DEBUG
                if (1 < matches.TotalHits) {
					Utils.Log.LOG_SEARCH.Debug ("{0}:{1} is not unique {2}", type, id, matches.TotalHits);
				} else if (0 == matches.TotalHits) {
					Utils.Log.LOG_SEARCH.Debug ("{0}:{1} not found", type, id);
				}
#endif
                return false;
            }
            Reader.DeleteDocument (matches.ScoreDocs [0].Doc);
            NeedsCommit = true;
            return true;
        }

        public int RemoveAllMessages ()
        {
            var messages = new Term ("type", "message");
            var count = Reader.DeleteDocuments (messages);
            if (count > 0) {
                NeedsCommit = true;
            }
            return count;
        }

        public void Dispose ()
        {
            Reader.Dispose ();
            IndexLock.ReleaseMutex ();
        }
    }

    public class MatchedItem
    {
        public float Score { get; protected set; }

        public string Type { get; protected set; }

        public string Id { get; protected set; }

        public MatchedItem (string type, string id, float score)
        {
            Score = score;
            Type = type;
            Id = id;
        }

        public MatchedItem (Document doc, float score)
        {
            Score = score;
            var field = doc.GetField ("type");
            Type = field.StringValue;
            field = doc.GetField ("id");
            Id = field.StringValue;
        }
    }

    public class MatchedItemScoreComparer : IComparer<MatchedItem>
    {

        public int Compare (MatchedItem a, MatchedItem b)
        {
            return Math.Sign (b.Score - a.Score);
        }
    }
}
