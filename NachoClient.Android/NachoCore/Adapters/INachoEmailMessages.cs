//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore
{
    public interface INachoEmailMessages
    {
        int Count ();

        /// <summary>
        /// Refresh the email message list. Return true if there is changes; false otherrwise.
        /// </summary>
        bool Refresh ();

        McEmailMessageThread GetEmailThread (int i);

        string DisplayName ();
    }
}
