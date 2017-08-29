//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Concurrent;

namespace NachoCore.Index
{
    public class Indexer
    {
        #region Getting the indexer

        public readonly static Indexer Instance = new Indexer ();

        Indexer ()
        {
        }

        #endregion

        #region Managing Account Indexes

        readonly ConcurrentDictionary<int, NcIndex> IndexesByAccount = new ConcurrentDictionary<int, NcIndex> ();

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

        public void DeleteIndex (int accountId)
        {
            var index = IndexForAccount (accountId);
            index.MarkForDeletion ();
        }

        #endregion

        #region Adding & Removing Items

        public void Add (Model.McEmailMessage message)
        {
        }

        public void Add (Model.McContact contact)
        {
        }

        public void Remove (Model.McEmailMessage message)
        {
        }

        public void Remove (Model.McContact contact)
        {
        }

        #endregion

        #region Indexing Queue

        void EnqueueJob ()
        {
        }

        #endregion

    }
}
