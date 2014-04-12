//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore
{
    public interface INachoContacts
    {
        int Count ();
        McContact GetContact (int i);

        void Search (string prefix);
        int SearchResultsCount ();
        McContact GetSearchResult(int searchIndex);

        bool isVIP(McContact contact);
        bool isHot(McContact contact);
    }
}