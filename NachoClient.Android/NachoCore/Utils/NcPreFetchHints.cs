//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

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

        public void AddHint (int AccountId, McEmailMessage email)
        {
            AddHint (AccountId, email.Id);
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

        #region PerAccountFetchHints

        public class PerAccountFetchHints
        {
            public class Hint {
                public int Id;
                public int Priority;

                public Hint (int id, int prio)
                {
                    Id = id;
                    Priority = prio;
                }
            }

            readonly NcCircularBuffer<Hint> AccountHints;
            int HintCounter;

            public PerAccountFetchHints (int maxSize)
            {
                AccountHints = new NcCircularBuffer<Hint> (maxSize);
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
                    AccountHints.Enqueue (new Hint (Id, HintCounter));
                }
            }

            public List<int> GetHints (int count)
            {
                lock (AccountHints) {
                    var hintList = new List<int> ();
                    List<Hint> hints = AccountHints.ToList ();
                    hints.Sort ((h1, h2) => {
                        return h1.Priority - h2.Priority;
                    });
                    foreach (Hint h in hints.Take(count)) {
                        hintList.Add (h.Id);
                        AccountHints.Remove (h);
                    }
                    return hintList;
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

