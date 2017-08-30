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

        #region Managing Account Indexes

        readonly ConcurrentDictionary<int, NcIndex> IndexesByAccount = new ConcurrentDictionary<int, NcIndex> ();

        /// <summary>
        /// Get the index corresponding to a particular account.  Each account has its own index primarily
        /// to allow for easy and thorough deletion of the entire account data.
        /// </summary>
        /// <returns>The index for an account.</returns>
        /// <param name="accountId">The id number of the account</param>
        public NcIndex IndexForAccount (int accountId)
        {
            if (IndexesByAccount.TryGetValue (accountId, out var index)) {
                return index;
            }
            var indexPath = Model.NcModel.Instance.GetIndexPath (accountId);
            index = new NcIndex (indexPath);
            if (!IndexesByAccount.TryAdd (accountId, index)) {
                // A race happens and this thread loses. There should be an Index in the dictionary now
                index.Dispose ();
                index = null;
                var got = IndexesByAccount.TryGetValue (accountId, out index);
                Utils.NcAssert.True (got);
            }
            return index;
        }

        /// <summary>
        /// Delete the indexed items for a given account
        /// </summary>
        /// <param name="accountId">Account id number</param>
        public void DeleteIndex (int accountId)
        {
            var index = IndexForAccount (accountId);
            index.MarkForDeletion ();
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
                    DocumentId = message.GetIndexId (),
                    DocumentType = EmailMessageIndexDocument.DocumentType
                };
                item.Insert ();
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
                    DocumentId = contact.GetIndexId (),
                    DocumentType = ContactIndexDocument.DocumentType
                };
                item.Insert ();
                JobQueue.Enqueue (UnindexJob);
                SetNeedsWork ();
            }, "Indexer.RemoveContact");
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
            JobQueue.Enqueue (IndexEmailMessagesJob);
            JobQueue.Enqueue (IndexContactsJob);
            JobQueue.Enqueue (UnindexJob);
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
            var messagesByAccount = new Dictionary<int, List<McEmailMessage>> ();
            foreach (var message in messages) {
                if (!messagesByAccount.ContainsKey (message.AccountId)) {
                    messagesByAccount [message.AccountId] = new List<McEmailMessage> ();
                }
                messagesByAccount [message.AccountId].Add (message);
            }
            foreach (var pair in messagesByAccount) {
                IndexEmailMessages (pair.Key, pair.Value.ToArray ());
            }
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
            var contactsByAccount = new Dictionary<int, List<McContact>> ();
            foreach (var contact in contacts) {
                if (!contactsByAccount.ContainsKey (contact.AccountId)) {
                    contactsByAccount [contact.AccountId] = new List<McContact> ();
                }
                contactsByAccount [contact.AccountId].Add (contact);
            }
            foreach (var pair in contactsByAccount) {
                IndexContacts (pair.Key, pair.Value.ToArray ());
            }
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
            var itemsByAccount = new Dictionary<int, List<McSearchUnindexQueueItem>> ();
            foreach (var item in items) {
                if (!itemsByAccount.ContainsKey (item.AccountId)) {
                    itemsByAccount [item.AccountId] = new List<McSearchUnindexQueueItem> ();
                }
                itemsByAccount [item.AccountId].Add (item);
            }
            foreach (var pair in itemsByAccount) {
                Unindex (pair.Key, pair.Value.ToArray ());
            }
            if (items.Count == limit) {
                JobQueue.Enqueue (UnindexJob);
            }
        }

        /// <summary>
        /// Index a group of messages in a transaction
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="messages">Messages.</param>
        void IndexEmailMessages (int accountId, McEmailMessage [] messages)
        {
            // TODO: make sure each message still exists and the account exists
            var index = IndexForAccount (accountId);
            // remove any messages that are already in the index
            using (var transaction = index.RemovingTransaction ()) {
                if (transaction != null) {
                    foreach (var message in messages) {
                        if (message.IsIndexed != 0) {
                            transaction.Remove (EmailMessageIndexDocument.DocumentType, message.GetIndexId ());
                        }
                    }
                    transaction.Commit ();
                }
            }
            // add all messages to the index
            using (var transaction = index.AddingTransaction ()) {
                if (transaction != null) {
                    NcModel.Instance.RunInTransaction (() => {
                        foreach (var message in messages) {
                            transaction.Add (message);
                            message.UpdateIsIndex (message.SetIndexVersion ());
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
        void IndexContacts (int accountId, McContact [] contacts)
        {
            // TODO: make sure each contact still exists and the account exists
            var index = IndexForAccount (accountId);
            // remove any contacts that are already in the index
            using (var transaction = index.RemovingTransaction ()) {
                if (transaction != null) {
                    foreach (var contact in contacts) {
                        if (contact.IndexVersion != 0) {
                            transaction.Remove (ContactIndexDocument.DocumentType, contact.GetIndexId ());
                        }
                    }
                    transaction.Commit ();
                }
            }
            // add all contacts to the index
            using (var transaction = index.AddingTransaction ()) {
                if (transaction != null) {
                    NcModel.Instance.RunInTransaction (() => {
                        foreach (var contact in contacts) {
                            transaction.Add (contact);
                            contact.SetIndexVersion ();
                            contact.UpdateIndexVersion ();
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
        void Unindex (int accountId, McSearchUnindexQueueItem [] items)
        {
            var index = IndexForAccount (accountId);
            using (var transaction = index.RemovingTransaction ()) {
                NcModel.Instance.RunInTransaction (() => {
                    foreach (var item in items) {
                        transaction.Remove (item.DocumentType, item.DocumentId);
                        item.Delete ();
                    }
                    transaction.Commit ();
                });
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
                return null;
            }
            var bundle = new NcEmailMessageBundle (body);
            if (bundle.NeedsUpdate) {
                bundle.Update ();
            }
            return bundle.FullText;
        }

        /// <summary>
        /// Get the structured parameters that can be used to create a <see cref="EmailMessageIndexDocument"/>.
        /// This method may block while the content parameter is extracted, so it should be called on a background thread.
        /// </summary>
        /// <returns>The parameters.</returns>
        /// <param name="message">Message.</param>
        public static EmailMessageIndexParameters GetIndexParameters (this McEmailMessage message)
        {

            var parameters = new EmailMessageIndexParameters () {
                From = NcEmailAddress.ParseAddressListString (message.From),
                To = NcEmailAddress.ParseAddressListString (message.To),
                Cc = NcEmailAddress.ParseAddressListString (message.Cc),
                Bcc = NcEmailAddress.ParseAddressListString (message.Bcc),
                ReceivedDate = message.DateReceived,
                Subject = message.Subject,
                Preview = message.BodyPreview,
            };
            parameters.Content = message.GetIndexContent ();
            return parameters;
        }

        /// <summary>
        /// Get the document that can be added to a search index.  This method may block while the parameters
        /// are extracted, so it should be called on a background thread.
        /// </summary>
        /// <returns>The document.</returns>
        /// <param name="message">Message.</param>
        public static EmailMessageIndexDocument GetIndexDocument (this McEmailMessage message)
        {
            if (message.IsJunk) {
                return null;
            }
            var id = message.GetIndexId ();
            var parameters = message.GetIndexParameters ();
            var doc = new EmailMessageIndexDocument (id, parameters);
            return doc;
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

        /// <summary>
        /// Get the parameters that can be used to construct a <see cref="ContactIndexDocument"/>
        /// </summary>
        /// <returns>The parameters.</returns>
        /// <param name="contact">Contact.</param>
        public static ContactIndexParameters GetIndexParameters (this McContact contact)
        {
            var parameters = new ContactIndexParameters () {
                FirstName = contact.FirstName,
                MiddleName = contact.MiddleName,
                LastName = contact.LastName,
                CompanyName = contact.CompanyName,
            };
            foreach (var emailAddress in contact.EmailAddresses) {
                parameters.EmailAddresses.Add (emailAddress.Value);
                var addr = NcEmailAddress.ParseMailboxAddressString (emailAddress.Value);
                if (addr != null) {
                    var idx = emailAddress.Value.IndexOf ("@");
                    parameters.EmailDomains.Add (emailAddress.Value.Substring (idx + 1));
                }
            }
            foreach (var phoneNumber in contact.PhoneNumbers) {
                parameters.PhoneNumbers.Add (phoneNumber.Value);
            }
            foreach (var address in contact.Addresses) {
                string addressString = address.Street + "\n" + address.City + "\n" + address.State + "\n" + address.PostalCode + "\n" + address.Country + "\n";
                parameters.Addresses.Add (addressString);
            }
            var body = contact.GetBodyIfComplete ();
            if (body != null) {
                try {
                    parameters.Note = File.ReadAllText (body.GetFilePath ());
                } catch (IOException) {
                }
            }
            return parameters;
        }

        /// <summary>
        /// Get the document that can be added to a search index
        /// </summary>
        /// <returns>The index document.</returns>
        /// <param name="contact">Contact.</param>
        public static ContactIndexDocument GetIndexDocument (this McContact contact)
        {
            var id = contact.GetIndexId ();
            var parameters = contact.GetIndexParameters ();
            var doc = new ContactIndexDocument (id, parameters);
            return doc;
        }
    }

    #endregion

    #region AddingTransaction Extensions

    public static class AddingTransactionExtensions
    {
        /// <summary>
        /// Add an email message to the search index for this transaction.  This is a convenience
        /// method that mostly just calls <see cref="EmailMessageExtensions.GetIndexDocument"/> and forwards
        /// it along to the <see cref="AddingTransaction.Add(NcIndexDocument)"/> method
        /// </summary>
        /// <returns>The add.</returns>
        /// <param name="transaction">Transaction.</param>
        /// <param name="message">The message to add</param>
        public static void Add (this AddingTransaction transaction, McEmailMessage message)
        {
            try {
                var doc = message.GetIndexDocument ();
                if (doc != null) {
                    transaction.Add (doc);
                }
            } catch (NullReferenceException e) {
                Log.Error (Log.LOG_SEARCH, "IndexEmailmessage: caught null exception - {0}", e);
            }
        }

        /// <summary>
        /// Add a contact to the search index for this transaction.  This is a convenience
        /// method that mostly just calls <see cref="ContactExtensions.GetIndexDocument"/> and forwards
        /// it along to the <see cref="AddingTransaction.Add(NcIndexDocument)"/> method
        /// </summary>
        /// <returns>The add.</returns>
        /// <param name="transaction">Transaction.</param>
        /// <param name="contact">The contact to add</param>
        public static void Add (this AddingTransaction transaction, McContact contact)
        {
            try {
                var doc = contact.GetIndexDocument ();
                if (doc != null) {
                    transaction.Add (doc);
                }
            } catch (NullReferenceException e) {
                Log.Error (Log.LOG_SEARCH, "IndexEmailmessage: caught null exception - {0}", e);
            }
        }
    }

    #endregion
}
