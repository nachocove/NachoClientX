//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Brain;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.Brain
{
    public class NcMessageThreads
    {
        public NcMessageThreads ()
        {
        }

        static public List<McEmailMessageThread> ThreadByConversation (List<McEmailMessageThread> list)
        {
            if (null == list) {
                return new List<McEmailMessageThread> ();
            } else {
                return list;
            }
        }

        static public List<McEmailMessageThread> ThreadByMessage (List<McEmailMessageThread> list)
        {
            if (null == list) {
                return new List<McEmailMessageThread> ();
            } else {
                return list;
            }
        }
            
        // Return true or false if old and new lists are different.
        // Return 'deletes' if a series of deletes can transform oldList into newList.
        protected static bool CheckForDeletes (List<McEmailMessageThread> oldList, List<McEmailMessageThread> newList, out List<int> deletes)
        {
            deletes = null;
            if ((null == oldList) || (null == newList)) {
                return true;
            }
            if ((0 == oldList.Count) || (0 == newList.Count)) {
                return true;
            }
            // New list has more; need 'adds'
            if (oldList.Count < newList.Count) {
                return true;
            }
            deletes = new List<int> ();
            int oldListIndex = 0;
            int newListIndex = 0;
            while ((oldListIndex < oldList.Count) && (newListIndex < newList.Count)) {
                var oldId = oldList [oldListIndex].FirstMessageSpecialCaseIndex ();
                var newId = newList [newListIndex].FirstMessageSpecialCaseIndex ();
                if (oldId != newId) {
                    deletes.Add (oldListIndex);
                } else {
                    newListIndex += 1;
                }
                oldListIndex += 1;
            }
            // Delete the end of list, if any more
            while (oldListIndex < oldList.Count) {
                deletes.Add (oldListIndex);
                oldListIndex += 1;
            }
            // Made it to the end of the lists with no deletes; old & new are the same
            if ((newList.Count == newListIndex) && (oldList.Count == oldListIndex)) {
                if (0 == deletes.Count) {
                    deletes = null;
                    return false;
                }
            }
            // Didn't get to the end of the new list, some adds at the end
            if (newList.Count != newListIndex) {
                deletes = null;
            }
            NcAssert.True ((null == deletes) || (0 < deletes.Count));
            return true;
        }

        // Return true if old and new lists are different. Empty lists always differ.
        // Return a list of 'adds' if the new list is strictly additions to the old list.
        protected static bool CheckForAdds (List<McEmailMessageThread> oldList, List<McEmailMessageThread> newList, out List<int> adds)
        {
            adds = null;
            if ((null == oldList) || (null == newList)) {
                return true;
            }
            if ((0 == oldList.Count) || (0 == newList.Count)) {
                return true;
            }
            // Old list has more; need 'deletes'
            if (oldList.Count > newList.Count) {
                return true;
            }
            adds = new List<int> ();
            int oldListIndex = 0;
            int newListIndex = 0;

            while ((oldListIndex < oldList.Count) && (newListIndex < newList.Count)) {
                var oldId = oldList [oldListIndex].FirstMessageSpecialCaseIndex ();
                var newId = newList [newListIndex].FirstMessageSpecialCaseIndex ();
                if (oldId != newId) {
                    adds.Add (newListIndex);
                } else {
                    oldListIndex += 1;
                }
                newListIndex += 1;
            }

            // Adds the end of list, if any more
            while (newListIndex < newList.Count) {
                adds.Add (newListIndex);
                newListIndex += 1;
            }
            // Made it to the end of the lists with no adds; old & new are the same
            if ((newList.Count == newListIndex) && (oldList.Count == oldListIndex)) {
                if (0 == adds.Count) {
                    adds = null;
                    return false;
                }
            }
            // Didn't get to the end of the old list, some deletes at the end
            if (oldList.Count != oldListIndex) {
                adds = null;
            }
            NcAssert.True ((null == adds) || (0 < adds.Count));
            return true;
        }

        // Return true if lists differ. Return a list of additions or deletions
        // If either list is empty, always returns true with no additions or deletions.
        // (Kind of a kludge but needed because empty list really has one cell "Empty list".)
        public static bool AreDifferent (List<McEmailMessageThread> oldList, List<McEmailMessageThread> newList, out List<int> adds, out List<int> deletes)
        {
            adds = null;
            deletes = null;

            if ((null == oldList) || (0 == oldList.Count)) {
                return true;
            }
            if ((null == newList) || (0 == newList.Count)) {
                return true;
            }

            if (!CheckForDeletes (oldList, newList, out deletes)) {
                return false;
            }
            if (null == deletes) {
                CheckForAdds (oldList, newList, out adds);
            }
            return true;
        }
    }
}
