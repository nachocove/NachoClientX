//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Brain;
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Generic;

namespace Test.Common
{
    // public static bool AreDifferent (List<McEmailMessageThread> oldList, List<NcEmailMessageIndex> newList, out List<int> adds, out List<int> deletes)

    public class NcMessageThreadsTest
    {
        public NcMessageThreadsTest ()
        {
        }

        public bool IsNullOrNotEmpty (List<int> l)
        {
            return (null == l) || (0 < l.Count);
        }

        public List<McEmailMessageThread> CreateMessageThreadList (params int[] values)
        {
            var list = new List<McEmailMessageThread> ();

            foreach (var v in values) {
                var n = new NcEmailMessageIndex ();
                n.Id = v;
                var e = new McEmailMessageThread ();
                e.Add (n);
                list.Add (e);
            }
            return list;
        }

        public List<McEmailMessageThread> CreateMessageIndexList (params int[] values)
        {
            var list = new List<NcEmailMessageIndex> ();

            foreach (var v in values) {
                var n = new NcEmailMessageIndex ();
                n.Id = v;
                list.Add (n);
            }
            return NcMessageThreads.ThreadByMessage (list);
        }

        public void CheckAddsAndDeletes (List<McEmailMessageThread> oldList, List<McEmailMessageThread> newList, List<int> adds, List<int> deletes)
        {
            Assert.True (IsNullOrNotEmpty (adds));
            Assert.True (IsNullOrNotEmpty (deletes));

            if ((null == adds) && (null == deletes)) {
                return;
            }

            if (null == adds) {
                adds = new List<int> ();
            }
            if (null == deletes) {
                deletes = new List<int> ();
            }

            Assert.AreEqual (newList.Count - oldList.Count, adds.Count - deletes.Count);
        }

        [Test]
        public void AreDifferentTest ()
        {
            List<int> adds;
            List<int> deletes;

            // Empty lists are different because we need to re-draw the empty list cell
            Assert.True (NcMessageThreads.AreDifferent (null, null, out adds, out deletes));
            Assert.True (IsNullOrNotEmpty (adds));
            Assert.True (IsNullOrNotEmpty (deletes));

            var oldList = CreateMessageThreadList ();
            var newList = CreateMessageIndexList ();
            // Special case -- force release when new list is null (AreDifferent is true)
            Assert.True (NcMessageThreads.AreDifferent (oldList, newList, out adds, out deletes));
            Assert.True (IsNullOrNotEmpty (adds));
            Assert.True (IsNullOrNotEmpty (deletes));

            oldList = CreateMessageThreadList (1);
            newList = CreateMessageIndexList (1);
            Assert.False (NcMessageThreads.AreDifferent (oldList, newList, out adds, out deletes));
            Assert.True (IsNullOrNotEmpty (adds));
            Assert.True (IsNullOrNotEmpty (deletes));

            oldList = CreateMessageThreadList (1, 2, 3);
            newList = CreateMessageIndexList (1, 2, 3);
            Assert.False (NcMessageThreads.AreDifferent (oldList, newList, out adds, out deletes));
            Assert.True (IsNullOrNotEmpty (adds));
            Assert.True (IsNullOrNotEmpty (deletes));

            oldList = CreateMessageThreadList (1);
            newList = CreateMessageIndexList ();
            Assert.True (NcMessageThreads.AreDifferent (oldList, newList, out adds, out deletes));
            CheckAddsAndDeletes (oldList, newList, adds, deletes);

            oldList = CreateMessageThreadList ();
            newList = CreateMessageIndexList (1);
            Assert.True (NcMessageThreads.AreDifferent (oldList, newList, out adds, out deletes));
            CheckAddsAndDeletes (oldList, newList, adds, deletes);

            oldList = CreateMessageThreadList (1);
            newList = CreateMessageIndexList (2);
            Assert.True (NcMessageThreads.AreDifferent (oldList, newList, out adds, out deletes));
            CheckAddsAndDeletes (oldList, newList, adds, deletes);

            oldList = CreateMessageThreadList (1);
            newList = CreateMessageIndexList (1, 2);
            Assert.True (NcMessageThreads.AreDifferent (oldList, newList, out adds, out deletes));
            CheckAddsAndDeletes (oldList, newList, adds, deletes);

            oldList = CreateMessageThreadList (1, 2);
            newList = CreateMessageIndexList (1);
            Assert.True (NcMessageThreads.AreDifferent (oldList, newList, out adds, out deletes));
            CheckAddsAndDeletes (oldList, newList, adds, deletes);

            oldList = CreateMessageThreadList (1, 2, 3, 4);
            newList = CreateMessageIndexList (2, 3, 4);
            Assert.True (NcMessageThreads.AreDifferent (oldList, newList, out adds, out deletes));
            CheckAddsAndDeletes (oldList, newList, adds, deletes);

            oldList = CreateMessageThreadList (3, 2, 1);
            newList = CreateMessageIndexList (4, 3, 2, 1);
            Assert.True (NcMessageThreads.AreDifferent (oldList, newList, out adds, out deletes));
            CheckAddsAndDeletes (oldList, newList, adds, deletes);

            oldList = CreateMessageThreadList (4, 3, 2, 1);
            newList = CreateMessageIndexList (4, 2, 1);
            Assert.True (NcMessageThreads.AreDifferent (oldList, newList, out adds, out deletes));
            CheckAddsAndDeletes (oldList, newList, adds, deletes);

        }
    }
}

