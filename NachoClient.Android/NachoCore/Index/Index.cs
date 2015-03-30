//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MimeKit;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Store;
using Lucene.Net.Index;
using Lucene.Net.Documents;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Search.Payloads;

namespace NachoCore.Index
{
    public class NcIndex : IDisposable
    {
        private StandardAnalyzer Analyzer;
        private FSDirectory IndexDirectory;

        private Mutex Lock;
        private IndexWriter Writer;
        private IndexReader Reader;

        public bool Dirty { protected set; get; }

        public NcIndex (string indexDirectoryPath)
        {
            Analyzer = new StandardAnalyzer (Lucene.Net.Util.Version.LUCENE_30);
            IndexDirectory = FSDirectory.Open (indexDirectoryPath);
            Lock = new Mutex ();
        }

        public void Dispose ()
        {
            Analyzer.Dispose ();
            IndexDirectory.Dispose ();
            Analyzer = null;
            IndexDirectory = null;
        }

        private void Debug (string fmt, params object[] args)
        {
            #if INDEX_DEBUG
            Console.WriteLine (fmt, args);
            #endif
        }

        // Add() is significantly slower than BeginAddTransaction() + BatchAdd() + EndAddTransaction()
        // So, if you are going to use more than one item, please use the later combination.
        public long Add (NcIndexDocument doc)
        {
            BeginAddTransaction ();
            var bytesIndexed = BatchAdd (doc);
            EndAddTransaction ();
            return bytesIndexed;
        }

        public long BatchAdd (NcIndexDocument doc)
        {
            if (null == Writer) {
                throw new ArgumentNullException ("writer not set up");
            }

            // Index the document
            Writer.AddDocument (doc.Doc);
            Dirty = true;

            return doc.BytesIndexed;
        }

        public void BeginAddTransaction ()
        {
            Lock.WaitOne ();
            if (null != Writer) {
                throw new ArgumentException ("writer already exists");
            }
            Dirty = false;
            Writer = new IndexWriter (IndexDirectory, Analyzer, IndexWriter.MaxFieldLength.UNLIMITED);
        }

        public void EndAddTransaction ()
        {
            if (Dirty) {
                Writer.Commit ();
                Dirty = false;
            }
            Writer.Dispose ();
            Writer = null;
            Lock.ReleaseMutex ();
        }

        // Remove() is significantly slower than BeginRemoveTransaction() + BatchRemove() + EndRemoveTransaction()
        // So, if you are going to remove more than one item, please use the later combination.
        public bool Remove (string type, string id)
        {
            if (!BeginRemoveTransaction ()) {
                return false;
            }
            var isRemoved = BatchRemove (type, id);
            EndRemoveTransaction ();
            return isRemoved;
        }

        public bool BatchRemove (string type, string id)
        {
            if (null == Reader) {
                throw new ArgumentNullException ("reader not set up");
            }
            var parser = new QueryParser (Lucene.Net.Util.Version.LUCENE_30, "id", Analyzer);
            var queryString = String.Format ("type:{0} AND id:{1}", type, id);
            var query = parser.Parse (queryString);
            var searcher = new IndexSearcher (Reader);
            var matches = searcher.Search (query, 2);
            if (1 != matches.TotalHits) {
                if (1 < matches.TotalHits) {
                    Debug ("{0}:{1} is not unique {2}", type, id, matches.TotalHits);
                } else if (0 == matches.TotalHits) {
                    Debug ("{0}:{1} not found", type, id);
                }
                return false;
            }
            Reader.DeleteDocument (matches.ScoreDocs [0].Doc);
            Dirty = true;
            return true;
        }

        public bool BeginRemoveTransaction ()
        {
            Lock.WaitOne ();
            if (null != Reader) {
                throw new ArgumentException ("reader already exists");
            }
            try {
                Reader = IndexReader.Open (IndexDirectory, false);
            } catch (Lucene.Net.Store.NoSuchDirectoryException) {
                // This can happen if the removal is done before anything is written to the index.
                Lock.ReleaseMutex ();
                return false;
            }
            return true;
        }

        public void EndRemoveTransaction ()
        {
            Reader.Commit ();
            Reader.Dispose ();
            Reader = null;
            Lock.ReleaseMutex ();
        }

        public List<MatchedItem> Search (string queryString, int maxMatches = 1000)
        {
            List<MatchedItem> matchedItems = new List<MatchedItem> ();
            try {
                using (var reader = IndexReader.Open (IndexDirectory, true)) {
                    var parser = new QueryParser (Lucene.Net.Util.Version.LUCENE_30, "body", Analyzer);
                    var query = parser.Parse (queryString);
                    var searcher = new IndexSearcher (reader);
                    var matches = searcher.Search (query, maxMatches);
                    foreach (var scoreDoc in matches.ScoreDocs) {
                        matchedItems.Add (new MatchedItem (searcher.Doc (scoreDoc.Doc)));
                    }
                }
            } catch (Lucene.Net.Store.NoSuchDirectoryException) {
                // This can happen if a search is done before anything is written to the index.
            }
            return matchedItems;
        }

        public List<MatchedItem> SearchAllFields (string queryString, int maxMatches = 1000)
        {
            List<MatchedItem> matchedItems = new List<MatchedItem> ();
            try {
                using (var reader = IndexReader.Open (IndexDirectory, true)) {
                    var fields = new string[] { "body", "from", "subject", };
                    var parser = new MultiFieldQueryParser (Lucene.Net.Util.Version.LUCENE_30, fields, Analyzer);
                    var query = parser.Parse (queryString);
                    var searcher = new IndexSearcher (reader);
                    var matches = searcher.Search (query, maxMatches);
                    foreach (var scoreDoc in matches.ScoreDocs) {
                        matchedItems.Add (new MatchedItem (searcher.Doc (scoreDoc.Doc)));
                    }
                }
            } catch (Lucene.Net.Store.NoSuchDirectoryException) {
                // This can happen if a search is done before anything is written to the index.
            }
            return matchedItems;
        }
    }

    public class MatchedItem
    {
        public string Type { get; protected set; }

        public string Id { get; protected set; }

        public MatchedItem (string type, string id)
        {
            Type = type;
            Id = id;
        }

        public MatchedItem (Document doc)
        {
            var field = doc.GetField ("type");
            Type = field.StringValue;
            field = doc.GetField ("id");
            Id = field.StringValue;
        }
    }
}
