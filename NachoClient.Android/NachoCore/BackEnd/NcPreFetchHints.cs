//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections.Generic;
using JetBlack.Caching.Collections.Generic;

namespace NachoCore.Utils
{
    public class NcPreFetchHints
    {
        public const int KMaxFetchHintsPerAccount = 20;
        readonly ConcurrentDictionary<int, PerAccountFetchHints> Hints;

        public NcPreFetchHints ()
        {
            Hints = new ConcurrentDictionary<int, PerAccountFetchHints> ();
        }

        public void AddHint (int AccountId, int Id)
        {
            PerAccountFetchHints accountHints;
            if (!Hints.TryGetValue (AccountId, out accountHints)) {
                accountHints = new PerAccountFetchHints (KMaxFetchHintsPerAccount);
                Hints [AccountId] = accountHints;
            }
            accountHints.AddHint (Id);
        }

        public List<int> GetHints (int AccountId, int count)
        {
            PerAccountFetchHints accountHints;
            if (!Hints.TryGetValue (AccountId, out accountHints)) {
                return new List<int> ();
            }
            return accountHints.GetHints (count);
        }

        public int Count (int AccountId)
        {
            PerAccountFetchHints accountHints;
            if (Hints.TryGetValue (AccountId, out accountHints)) {
                return accountHints.Count ();
            } else {
                return 0;
            }
        }

        public int Count ()
        {
            int count = 0;
            foreach (PerAccountFetchHints accountHints in Hints.Values) {
                count += accountHints.Count ();
            }
            return count;
        }

        public int OverrunCounter ()
        {
            int count = 0;
            foreach (PerAccountFetchHints accountHints in Hints.Values) {
                count += accountHints.OverrunCounter;
            }
            return count;
        }

        public void Reset ()
        {
            foreach (var i in Hints.Keys) {
                PerAccountFetchHints x;
                Hints.TryRemove (i, out x);
            }
        }

        public void RemoveHint (int AccountId, int Id)
        {
            PerAccountFetchHints accountHints;
            if (Hints.TryGetValue (AccountId, out accountHints)) {
                accountHints.RemoveHint (Id);
            }            
        }

        #region PerAccountFetchHints

        public class PerAccountFetchHints
        {
            public class Hint
            {
                public int Id;
                public int Priority;

                public Hint (int id, int prio)
                {
                    Id = id;
                    Priority = prio;
                }
            }

            readonly CircularBuffer<Hint> AccountHints;
            int HintCounter;
            public int OverrunCounter { get; protected set; }

            public PerAccountFetchHints (int maxSize)
            {
                AccountHints = new CircularBuffer<Hint> (maxSize);
                HintCounter = 0;
            }

            public void AddHint (int Id)
            {
                lock (AccountHints) {
                    HintCounter++;
                    for (var i = 0; i<AccountHints.Count; i++) {
                        Hint h = AccountHints [i];
                        if (h.Id == Id) {
                            // update the priority
                            h.Priority = HintCounter;
                            return;
                        }
                    }

                    // We didn't find an item on the list. Add it.
                    if (AccountHints.Count == AccountHints.Capacity) {
                        // need to remove the lowest priority element
                        int lowestPrioIdx = -1;
                        for (var i = 0; i<AccountHints.Count; i++) {
                            if (lowestPrioIdx < 0 || AccountHints[lowestPrioIdx].Priority > AccountHints[i].Priority) {
                                lowestPrioIdx = i;
                            }
                        }
                        if (lowestPrioIdx >= 0) {
                            AccountHints.RemoveAt (lowestPrioIdx);
                        }
                    }
                    NcAssert.True (AccountHints.Count < AccountHints.Capacity);
                    if (null != AccountHints.Enqueue (new Hint (Id, HintCounter))) {
                        OverrunCounter++;
                    }
                }
            }

            public List<int> GetHints (int count)
            {
                lock (AccountHints) {
                    var hintList = new List<int> ();
                    List<Hint> hints = AccountHints.ToList ();
                    hints.Sort ((h1, h2) => {
                        return h2.Priority - h1.Priority;
                    });
                    foreach (Hint h in hints.Take(count)) {
                        hintList.Add (h.Id);
                        var idx = AccountHints.IndexOf (h);
                        if (idx >= 0) {
                            // NOTE: This is an O(n) operation, since elements will get moved to
                            // possibly fill the hole left by the removal.
                            AccountHints.RemoveAt (idx);
                        }
                    }
                    return hintList;
                }
            }

            public void RemoveHint (int Id)
            {
                lock (AccountHints) {
                    for (var i = 0; i<AccountHints.Count; i++) {
                        Hint h = AccountHints [i];
                        if (h.Id == Id) {
                            var idx = AccountHints.IndexOf (h);
                            if (idx >= 0) {
                                // NOTE: This is an O(n) operation, since elements will get moved to
                                // possibly fill the hole left by the removal.
                                AccountHints.RemoveAt (idx);
                            }
                            break;
                        }
                    }
                }
            }

            public int Count ()
            {
                lock (AccountHints) {
                    return AccountHints.Count;
                }
            }
        }

        #endregion
    }
}

