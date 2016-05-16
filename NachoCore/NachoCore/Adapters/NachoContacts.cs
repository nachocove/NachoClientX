//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;

namespace NachoCore
{
    public class NachoContacts : INachoContacts
    {
        List<NcContactIndex> list;

        public NachoContacts (List<NcContactIndex> list)
        {
            this.list = list;
        }
            
        public int Count ()
        {
            return list.Count;
        }

        public NcContactIndex GetContactIndex(int i)
        {
            return list.ElementAt (i);
        }

        public void Search (string prefix)
        {
        }

        public int SearchResultsCount ()
        {
            return 0;
        }

        public NcContactIndex GetSearchResult(int searchIndex)
        {
            return null;
        }
    }
}
