//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
   
    public interface IRtfConverter {

        string ToHtml (string rtf);
        string ToTxt (string rtf);

    }
}

