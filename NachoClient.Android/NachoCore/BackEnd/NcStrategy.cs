//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore
{
    public class NcStrategy
    {
        protected IBEContext BEContext;

        public NcStrategy (IBEContext beContext)
        {
            BEContext = beContext;
        }
    }
}
