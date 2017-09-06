//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace NachoCore.Index
{
    /// <summary>
    /// The indexer is in charge of getting all searchable items addted to and removed from the
    /// search indexes.  There is only ever one instance of the indexer, so it can managed tasks
    /// without stepping on itself.
    /// </summary>
    public class Indexer
    {
        #region Getting the indexer

        /// <summary>
        /// The shared singleton indexer 
        /// </summary>
        public readonly static Indexer Instance = new Indexer ();

        /// <summary>
        /// Private constructor to enforce singleton patttern
        /// </summary>
        Indexer ()
        {
        }

        #endregion

        #region Adding & Removing Items

        /// <summary>
        /// Notify the indexer that an email message needs to be added to the index
        /// </summary>
        /// <param name="message">The message to index</param>
        public void Add (McEmailMessage message)
        {
            Log.LOG_SEARCH.Info ("Indexer Add requested for message {0}", message.Id);
            // FIXME: no need to double enqueue here...maybe we need a different flagging mechanism to prevent lost jobs
            JobQueue.Enqueue (IndexEmailMessagesJob);
            SetNeedsWork ();
        }

        /// <summary>
        /// Notify the indexer that a contact needs to be added to the index
        /// </summary>
        /// <param name="contact">The contact to index</param>
        public void Add (McContact contact)
        {
            Log.LOG_SEARCH.Info ("Indexer Add requested for contact {0}", contact.Id);
            // FIXME: no need to double enqueue here...maybe we need a different flagging mechanism to prevent lost jobs
            JobQueue.Enqueue (IndexContactsJob);
            SetNeedsWork ();
        }

        /// <summary>
        /// Notify the indexer that an email message needs to be removed from the index
        /// </summary>
        /// <param name="message">The message to unindex</param>
        public void Remove (McEmailMessage message)
        {
            Log.LOG_SEARCH.Info ("Indexer Remove requested for message {0}", message.Id);
            NcTask.Run (() => {
                var item = new McSearchUnindexQueueItem () {
                    AccountId = message.AccountId,
                    DocumentId = message.GetIndexDocumentId (),
                };
                item.Insert ();
                // FIXME: no need to double enqueue here...maybe we need a different flagging mechanism to prevent lost jobs
                JobQueue.Enqueue (UnindexJob);
                SetNeedsWork ();
            }, "Indexer.RemoveMessage");
        }

        /// <summary>
        /// Notify the indexer that a contact needs to be removed from the index
        /// </summary>
        /// <param name="contact">The contact to unindex</param>
        public void Remove (McContact contact)
        {
            Log.LOG_SEARCH.Info ("Indexer Remove requested for contact {0}", contact.Id);
            NcTask.Run (() => {
                var item = new McSearchUnindexQueueItem () {
                    AccountId = contact.AccountId,
                    DocumentId = contact.GetIndexDocumentId (),
                };
                item.Insert ();
                // FIXME: no need to double enqueue here...maybe we need a different flagging mechanism to prevent lost jobs
                JobQueue.Enqueue (UnindexJob);
                SetNeedsWork ();
            }, "Indexer.RemoveContact");
        }

        /// <summary>
        /// Delete the indexed items for a given account
        /// </summary>
        /// <remarks>
        /// This method doesn't make its own task because the only time it's called
        /// is already on a background task.
        /// </remarks>
        /// <param name="accountId">Account id number</param>
        public void RemoveAccount (int accountId)
        {
            Log.LOG_SEARCH.Info ("Indexer Remove requested for account {0}", accountId);
            // Insert a special item without a documentId, which indicates its the account
            // that should be unindexed
            var item = new McSearchUnindexQueueItem () {
                AccountId = accountId
            };
            item.Insert ();
            // FIXME: no need to double enqueue here...maybe we need a different flagging mechanism to prevent lost jobs
            JobQueue.Enqueue (UnindexJob);
            SetNeedsWork ();
        }

        #endregion

        #region Worker

        enum StateEnum
        {
            Stopped,
            Started,
            Working
        }

        int State;

        /// <summary>
        /// Start the Indexer.  Typically called on app launch or foreground, this will start a worker to see if there are
        /// any items right away.  Calling start doesn't mean that a worker will be continuously running, because a
        /// worker will only stay alive as long as there is work to do.  The service must be started, however, before a
        /// worker is allowed to run, and calling <see cref="Stop"/> not only interrupts any running worker, but prevents
        /// further workers from running until Start is called again.  It is safe to call Start multiple times, although
        /// a warning will be logged if you do because repeat calls likely indicate an issue with the calling code.
        /// </summary>
        public void Start ()
        {
            if (Interlocked.CompareExchange (ref State, (int)StateEnum.Started, (int)StateEnum.Stopped) == (int)StateEnum.Stopped) {
                Log.LOG_SEARCH.Info ("Indexer started");
                JobQueue.Enqueue (IndexEmailMessagesJob);
                JobQueue.Enqueue (IndexContactsJob);
                JobQueue.Enqueue (UnindexJob);
                SetNeedsWork ();
            } else {
                Log.LOG_SEARCH.Warn ("Start() called on Indexer that was not stopped");
            }
        }

        /// <summary>
        /// Stop the indexer. Typically called when the app goes to background, this will interrupt any worker currently
        /// running and prevent another worker from running until <see cref="Start"/> is called.  It is safe to call
        /// Stop multiple times, although this should not be standard practice.
        /// </summary>
        public void Stop ()
        {
            Interlocked.Exchange (ref State, (int)StateEnum.Stopped);
            Log.LOG_SEARCH.Info ("Indexer stopped");
        }

        /// <summary>
        /// Notify the indexer that some work is required.  This will launch a worker if one is not already running.  It
        /// has no effect if the indexer is stopped.
        /// </summary>
        void SetNeedsWork ()
        {
            if (Interlocked.CompareExchange (ref State, (int)StateEnum.Working, (int)StateEnum.Started) == (int)StateEnum.Started) {
                Log.LOG_SEARCH.Info ("Indexer launching worker");
                NcTask.Run (Work, "Indexer");
            }
        }

        /// <summary>
        /// The jobs that the worker needs to perform.  There are three possible jobs:
        /// <list>
        /// <item><description><see cref="IndexEmailMessagesJob"/></description></item>
        /// <item><description><see cref="IndexContactsJob"/></description></item>
        /// <item><description><see cref="UnindexJob"/></description></item>
        /// </list>
        /// That initially populate the queue.  The worker simply dequeues a job and runs it.
        /// Each job runs only a small batch at a time so no one job takes up too much time.
        /// If a job thinks it has more work to do, it will at itself back to the queue, although
        /// at the end so other jobs run before it gets another shot.
        /// The <see cref="Add"/> and <see cref="Remove"/> methods also put a job on the queue
        /// to ensure it isn't missed if a worker has already started.
        /// </summary>
        readonly ConcurrentQueue<Action> JobQueue = new ConcurrentQueue<Action> ();

        /// <summary>
        /// The body of the worker task that gets launched as needed.  This worker dequeues jobs
        /// from the the <see cref="JobQueue"/>, runs them, checking the state between each run
        /// so it can end if the indexer has been stopped.
        /// </summary>
        void Work ()
        {
            var originalThreadPriority = Thread.CurrentThread.Priority;
            Thread.CurrentThread.Priority = ThreadPriority.Lowest;
            Log.LOG_SEARCH.Info ("Indexer worker running");
            try {
                while (State == (int)StateEnum.Working && JobQueue.TryDequeue (out var job)) {
                    job ();
                }
            } finally {
                if (Interlocked.CompareExchange (ref State, (int)StateEnum.Started, (int)StateEnum.Working) != (int)StateEnum.Working) {
                    Log.LOG_SEARCH.Info ("Indexer worker stopped before completion");
                    JobQueue.Clear ();
                } else {
                    Log.LOG_SEARCH.Info ("Indexer worker done");
                    // It's possible that an Add or Remove method added a job to the queue
                    // after the last time we looked.  If that method's call to SetNeedsWork
                    // happened before we changed the state back to Started, no new worker task was
                    // launched.  Therefore, we need to check the queue one more time, and
                    // call SetNeedsWork ourselves to ensure that the job gets picked up.
                    // Note that if the Add/Remove method's call to SetNeedsWork instead came
                    // after we changed the state back to Started, we'll be calling SetNeedsWork
                    // redundantly here, which is not a problem because it will instead be our
                    // call that won't launch a new worker task.
                    if (!JobQueue.IsEmpty) {
                        SetNeedsWork ();
                    }
                }
                Thread.CurrentThread.Priority = originalThreadPriority;
            }
        }

        const int MaxIndexEmailMessagesBatchCount = 5;

        /// <summary>
        /// A <see cref="JobQueue"/> element, query and index a small batch of messages
        /// </summary>
        void IndexEmailMessagesJob ()
        {
            var limit = MaxIndexEmailMessagesBatchCount;
            var messages = McEmailMessage.QueryNeedsIndexing (maxMessages: limit);
            Log.LOG_SEARCH.Info ("Indexer found {0} messages to index", messages.Count);
            IndexEmailMessages (messages.ToArray ());
            if (messages.Count == limit) {
                JobQueue.Enqueue (IndexEmailMessagesJob);
            }
        }

        const int MaxIndexContactsBatchCount = 5;

        /// <summary>
        /// A <see cref="JobQueue"/> element, query and index a small batch of contacts
        /// </summary>
        void IndexContactsJob ()
        {
            var limit = MaxIndexContactsBatchCount;
            var contacts = McContact.QueryNeedIndexing (maxContact: limit);
            Log.LOG_SEARCH.Info ("Indexer found {0} contacts to index", contacts.Count);
            IndexContacts (contacts.ToArray ());
            if (contacts.Count == limit) {
                JobQueue.Enqueue (IndexContactsJob);
            }
        }

        const int MaxUnindexBatchCount = 5;

        /// <summary>
        /// A <see cref="JobQueue"/> element, query and unindex a small batch of items
        /// </summary>
        void UnindexJob ()
        {
            var limit = MaxUnindexBatchCount;
            var items = McSearchUnindexQueueItem.Query (maxItems: limit);
            Log.LOG_SEARCH.Info ("Indexer found {0} unindex items", items.Count);
            Unindex (items.ToArray ());
            if (items.Count == limit) {
                JobQueue.Enqueue (UnindexJob);
            }
        }

        /// <summary>
        /// Index a group of messages in a transaction
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="messages">Messages.</param>
        void IndexEmailMessages (McEmailMessage [] messages)
        {
            // A note on stale data:
            // 1. If an account is deleted, its entire index is blown away, but that
            //    can't happen until a transaction completes, and starting a new
            //    transaction on a deleted index will return a null transaction.
            //    Since we watch out for a null transaction, we're safe from inserting
            //    items into an index that has been deleted.
            // 2. If a message has been deleted, its unindex job should still be pending
            //    so it's okay to insert it in the index because it will quickly be removed
            //    when the unindex job runs
            using (var transaction = NcIndex.Main.Transaction ()) {
                if (transaction != null) {
                    NcModel.Instance.RunInTransaction (() => {
                        foreach (var message in messages) {
                            if (message.IsIndexed == 0) {
                                transaction.Add (message);
                            } else {
                                transaction.Update (message);
                            }
                            message.UpdateIsIndex (message.GetIndexVersion ());
                        }
                        transaction.Commit ();
                    });
                }
            }
        }

        /// <summary>
        /// Index a group of contacts in a transaction
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="contacts">Contacts.</param>
        void IndexContacts (McContact [] contacts)
        {
            // A note on stale data:
            // 1. If an account is deleted, its entire index is blown away, but that
            //    can't happen until a transaction completes, and starting a new
            //    transaction on a deleted index will return a null transaction.
            //    Since we watch out for a null transaction, we're safe from inserting
            //    items into an index that has been deleted.
            // 2. If a contact has been deleted, its unindex job should still be pending
            //    so it's okay to insert it in the index because it will quickly be removed
            //    when the unindex job runs
            using (var transaction = NcIndex.Main.Transaction ()) {
                if (transaction != null) {
                    NcModel.Instance.RunInTransaction (() => {
                        foreach (var contact in contacts) {
                            if (contact.IndexVersion == 0) {
                                transaction.Add (contact);
                            } else {
                                transaction.Update (contact);
                            }
                            contact.UpdateIndexVersion (contact.GetIndexVersion ());
                        }
                        transaction.Commit ();
                    });
                }
            }
        }

        /// <summary>
        /// Unindex a group of items in a transaction
        /// </summary>
        /// <returns>The unindex.</returns>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="items">Items.</param>
        void Unindex (McSearchUnindexQueueItem [] items)
        {
            // A note on stale data:
            // 1. If an account is deleted, its entire index is blown away, but that
            //    can't happen until a transaction completes, and starting a new
            //    transaction on a deleted index will return a null transaction.
            //    Since we watch out for a null transaction, we're safe from removing
            //    items from an index that has been deleted.
            using (var transaction = NcIndex.Main.Transaction ()) {
                if (transaction != null) {
                    NcModel.Instance.RunInTransaction (() => {
                        foreach (var item in items) {
                            // special case when documentId is empty, means we should unindex the account
                            if (string.IsNullOrEmpty (item.DocumentId)) {
                                transaction.RemoveAccount (item.AccountId);
                            } else {
                                transaction.Remove (item.DocumentId);
                            }
                            item.Delete ();
                        }
                        transaction.Commit ();
                    });
                }
            }
        }

        #endregion

    }

    #region Email Message Extesnsions

    public static class EmailMessageExtensions
    {

        /// <summary>
        /// Get the ID string to use in the search index, unique among all messages
        /// </summary>
        /// <returns>A string identifier</returns>
        /// <param name="message">Message.</param>
        public static string GetIndexId (this McEmailMessage message)
        {
            return message.Id.ToString ();
        }

        public static string GetIndexDocumentId (this McEmailMessage message)
        {
            return string.Format ("{0}_{1}", EmailMessageDocument.DocumentType, message.Id);
        }

        /// <summary>
        /// Get the body content that will be indexed for search purposes.  This method may block while the content
        /// is extracted, so it should be called on a background thread.
        /// </summary>
        /// <returns>The body content.</returns>
        /// <param name="message">Message.</param>
        public static string GetIndexContent (this McEmailMessage message)
        {
            var body = message.GetBodyIfComplete ();
            if (body == null) {
                return message.BodyPreview;
            }
            var bundle = new NcEmailMessageBundle (body);
            if (bundle.NeedsUpdate) {
                bundle.Update ();
            }
            return bundle.FullText;
        }
    }

    #endregion

    #region Contact Extensions

    public static class ContactExtensions
    {

        /// <summary>
        /// Get the ID string to use in the search index, unique among all contacts
        /// </summary>
        /// <returns>A string identifier</returns>
        /// <param name="contact">Contact.</param>
        public static string GetIndexId (this McContact contact)
        {
            return contact.Id.ToString ();
        }

        public static string GetIndexDocumentId (this McContact contact)
        {
            return string.Format ("{0}_{1}", ContactDocument.DocumentType, contact.Id);
        }
    }

    #endregion

    #region AddingTransaction Extensions

    public static class AddingTransactionExtensions
    {
        /// <summary>
        /// Add an email message to the search index for this transaction.  This is a convenience
        /// method that mostly just creates a <see cref="EmailMessageDocument"/> and forwards
        /// it along to the <see cref="Transaction.Add"/> method
        /// </summary>
        /// <returns>The add.</returns>
        /// <param name="transaction">Transaction.</param>
        /// <param name="message">The message to add</param>
        public static void Add (this Transaction transaction, McEmailMessage message)
        {
            try {
                var doc = new EmailMessageDocument (message);
                transaction.Add (doc.Document, EmailMessageDocument.Analyzer);
            } catch (NullReferenceException e) {
                Log.Error (Log.LOG_SEARCH, "Add EmailMessage: caught null exception - {0}", e);
            }
        }

        public static void Update (this Transaction transaction, McEmailMessage message)
        {
            try {
                var doc = new EmailMessageDocument (message);
                transaction.Update (doc.DocumentIdTerm, doc.Document, EmailMessageDocument.Analyzer);
            } catch (NullReferenceException e) {
                Log.Error (Log.LOG_SEARCH, "Update EmailMessage: caught null exception - {0}", e);
            }
        }

        /// <summary>
        /// Add a contact to the search index for this transaction.  This is a convenience
        /// method that mostly just creates a  <see cref="ContactDocument"/> and forwards
        /// it along to the <see cref="Transaction.Add"/> method
        /// </summary>
        /// <returns>The add.</returns>
        /// <param name="transaction">Transaction.</param>
        /// <param name="contact">The contact to add</param>
        public static void Add (this Transaction transaction, McContact contact)
        {
            try {
                var doc = new ContactDocument (contact);
                transaction.Add (doc.Document, ContactDocument.Analyzer);
            } catch (NullReferenceException e) {
                Log.Error (Log.LOG_SEARCH, "Add Contact: caught null exception - {0}", e);
            }
        }

        public static void Update (this Transaction transaction, McContact contact)
        {
            try {
                var doc = new ContactDocument (contact);
                transaction.Update (doc.DocumentIdTerm, doc.Document, ContactDocument.Analyzer);
            } catch (NullReferenceException e) {
                Log.Error (Log.LOG_SEARCH, "Update Contact: caught null exception - {0}", e);
            }
        }
    }

    #endregion
}
