//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Store;
using Lucene.Net.Index;
using Lucene.Net.Documents;
using Lucene.Net.Search;

namespace NachoCore.Index
{
    public class NcIndex : IDisposable
    {
        /// <summary>
        /// The lucene version, used in various places that should all agree,
        /// so it's helpful to have a single place to change if needed
        /// </summary>
        public const Lucene.Net.Util.Version LuceneVersion = Lucene.Net.Util.Version.LUCENE_30;

        /// <summary>
        /// Every document stores a unque identifier under this field name
        /// </summary>
        public const string DocumentIdFieldName = "docid";

        /// <summary>
        /// Most documents will contain a type field under this name for easy filtering by type.
        /// Since we don't currently have a use case for querying across document types, this field
        /// is currently always used in a query.  Since we don't need the documents to all be in a shared
        /// index, perhaps we should split them up and use an index per document type
        /// </summary>
        public const string DocumentTypeFieldName = "type";

        /// <summary>
        /// Most documents will contain an account id under this field name for easy filtering by account
        /// </summary>
        public const string AccountIdFieldName = "accountid";

        #region Getting an Index

        /// <summary>
        /// The main search index, which includes emails and contacts across all accounts.  This is currently
        /// the only index we have.
        /// </summary>
        public readonly static NcIndex Main = new NcIndex (MainPath);

        /// <summary>
        /// The path on disk to the main index
        /// </summary>
        static string MainPath {
            get {
                return Path.Combine (NcApplication.GetDataDirPath (), "mainindex");
            }
        }

        /// <summary>
        /// Create an index that resides at the given path
        /// </summary>
        /// <param name="indexDirectoryPath">The disk location where all of the index's data lives</param>
        NcIndex (string indexDirectoryPath)
        {
            IndexDirectory = FSDirectory.Open (indexDirectoryPath);
        }

        /// <summary>
        /// The Lucene-specific directory object
        /// </summary>
        readonly FSDirectory IndexDirectory;

        #endregion

        #region Searching Emails

        /// <summary>
        /// Search the subject and body of all emails, limiting to a specific account if desired.
        /// This will not search the sender or receiver fields like To/From/etc.  We have found the
        /// results to be sub par, and instead do a separate search with <see cref="SearchContactsNameAndEmails"/>
        /// to get a list of contacts that can be displayed separately and can link to a full list of emails
        /// related to the contact.
        /// </summary>
        /// <returns>The emails.</returns>
        /// <param name="userQueryString">The search string entered by the user</param>
        /// <param name="accountId">An optional account id, <c>0</c> for all accounts</param>
        public IEnumerable<EmailMessageDocument> SearchEmails (string userQueryString, int accountId = 0)
        {
            var query = EmailMessageDocument.ContentQuery (userQueryString, accountId);
            return Search (query, (doc, score) => new EmailMessageDocument (doc, score));
        }

        #endregion

        #region Searching Contacts

        /// <summary>
        /// Search all contact fields
        /// </summary>
        /// <returns>The contacts.</returns>
        /// <param name="userQueryString">The search string entered by the user</param>
        public IEnumerable<ContactDocument> SearchContacts (string userQueryString, int maxResults = 100)
        {
            var query = ContactDocument.GeneralQuery (userQueryString);
            return Search (query, (doc, score) => new ContactDocument (doc, score), maxResults: maxResults);
        }

        /// <summary>
        /// Search for contacts who match the query string in name or email only.
        /// Useful when the user is trying to find emails from a specific person, this
        /// provides a list of contacts from which the user can narrow down there search
        /// in a precice way.
        /// </summary>
        /// <returns>The contacts name and emails.</returns>
        /// <param name="userQueryString">The search string entered by the user</param>
        public IEnumerable<ContactDocument> SearchContactsNameAndEmails (string userQueryString)
        {
            var query = ContactDocument.NameAndEmailQuery (userQueryString);
            return Search (query, (doc, score) => new ContactDocument (doc, score));
        }

        #endregion

        #region Adding and Removing Items

        /// <summary>
        /// Edits to the index are all done within a transaction, which can be started by this method.
        /// Note that currently only the <see cref="Indexer"/> creates any transactions, and it's likely
        /// that any future writing should be done through the indexer too.
        /// Typically this is used with a <c>using</c> statement
        /// <example>
        /// <code>
        /// using (var transaction = index.Transaction()){
        ///     transaction.Add(document1, analyzer);
        ///     transaction.Add(document2, analyzer);
        ///     ...
        ///     transaction.Commit();
        /// }
        /// </code>
        /// </example>
        /// </summary>
        /// <returns>The transaction.</returns>
        public Transaction Transaction ()
        {
            return new Transaction (IndexDirectory);
        }

        #endregion

        #region Disposable interface

        bool IsDisposed;

        public void Dispose ()
        {
            if (!IsDisposed) {
                IndexDirectory.Dispose ();
                IsDisposed = true;
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Search the given query and return a list of results for the given document type.  This is only useful
        /// for queries that return a single document type, which are currently the only queries we do.
        /// </summary>
        /// <remarks>
        /// Due to limitations in c# templating, we can't call a constructor of a generic type that takes parameters.
        /// However, this can be worked around by providing a factory function argument that can take a parameter and
        /// return the appropriate type.
        /// </remarks>
        /// <returns>The documents matching the query</returns>
        /// <param name="query">The structured lucene query</param>
        /// <param name="factory">Template constructor workaround, see remarks</param>
        /// <param name="maxResults">The maxiumum number of results</param>
        /// <typeparam name="DocumentType">The type of document being searched</typeparam>
        public IEnumerable<DocumentType> Search<DocumentType> (Query query, Func<Document, float, DocumentType> factory, int maxResults = 1000)
        {
            try {
                using (var reader = IndexReader.Open (IndexDirectory, readOnly: true)) {
                    var searcher = new IndexSearcher (reader);
                    var results = searcher.Search (query, maxResults);
                    return results.ScoreDocs.Select (x => factory (searcher.Doc (x.Doc), x.Score));
                }
            } catch (NoSuchDirectoryException) {
                // This can happen if a search is done before anything is written to the index.
            }
            return new DocumentType [0];
        }

        #endregion
    }

    #region Transactions

    public class Transaction : IDisposable
    {

        readonly IndexWriter Writer;
        bool NeedsCommit;

        /// <summary>
        /// Create a new transaction for an <see cref="NcIndex"/>.  This method should only be used by NcIndex.
        /// </summary>
        /// <param name="directory">Directory.</param>
        public Transaction (FSDirectory directory)
        {
            Writer = new IndexWriter (directory, new StandardAnalyzer (Lucene.Net.Util.Version.LUCENE_30), IndexWriter.MaxFieldLength.UNLIMITED);
        }

        /// <summary>
        /// Commit changes, if necessary.  You must call this before closing the transaction if you want the changes
        /// written
        /// </summary>
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
        }

        /// <summary>
        /// Add a document with the given analyzer. 
        /// </summary>
        /// <returns>The add.</returns>
        /// <param name="document">Document.</param>
        /// <param name="analyzer">Analyzer.</param>
        public void Add (Document document, Analyzer analyzer)
        {
            Writer.AddDocument (document, analyzer);
            NeedsCommit = true;
        }

        /// <summary>
        /// Update a document uniquely identified by the given term.  This will remove any document
        /// with that term, and add the given document to the index.
        /// </summary>
        /// <returns>The update.</returns>
        /// <param name="term">Term.</param>
        /// <param name="document">Document.</param>
        /// <param name="analyzer">Analyzer.</param>
        public void Update (Term term, Document document, Analyzer analyzer)
        {
            Writer.UpdateDocument (term, document, analyzer);
            NeedsCommit = true;
        }

        /// <summary>
        /// Remove a document with the given value for the <c>docid</c> field
        /// </summary>
        /// <returns>The remove.</returns>
        /// <param name="documentId">Document identifier.</param>
        public void Remove (string documentId)
        {
            var term = new Term (NcIndex.DocumentIdFieldName, documentId);
            Remove (term);
        }

        /// <summary>
        /// Remote any documents with the given term
        /// </summary>
        /// <returns>The remove.</returns>
        /// <param name="term">Term.</param>
        public void Remove (Term term)
        {
            Writer.DeleteDocuments (term);
            NeedsCommit = true;
        }

        /// <summary>
        /// Remove all email messages
        /// </summary>
        public void RemoveAllMessages ()
        {
            var term = new Term (NcIndex.DocumentTypeFieldName, EmailMessageDocument.DocumentType);
            Remove (term);
        }

        /// <summary>
        /// Remove all documents with the <c>accountid</c> field equal to the given value
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        public void RemoveAccount (int accountId)
        {
            var term = new Term (NcIndex.AccountIdFieldName, accountId.ToString ());
            Remove (term);
        }

    }

    #endregion
}
