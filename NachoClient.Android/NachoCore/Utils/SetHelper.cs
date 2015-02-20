//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

namespace NachoCore.Utils
{
    public interface ISetComparable<T>
    {
        bool IsEquivalent (T a);
    }

    public class SetHelper<T> where T : ISetComparable<T>
    {
        public static bool IsInList (T item, List<T> list)
        {
            foreach (var i in list) {
                if (item.IsEquivalent (i)) {
                    return true;
                }
            }
            return false;
        }

        public static bool IsSuperSet (List<T> superset, List<T> subset)
        {
            if (superset.Count < subset.Count) {
                return false;
            }
            foreach (var item in subset) {
                if (!IsInList (item, superset)) {
                    return false;
                }
            }
            return true;

        }
    }
}

