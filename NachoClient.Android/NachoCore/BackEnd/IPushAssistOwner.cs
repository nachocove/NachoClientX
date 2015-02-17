//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;

namespace NachoCore
{
    public interface IPushAssistOwner : IBEContext
    {
        string PushAssistRequestUrl ();
        HttpHeaders PushAssistRequestHeaders ();
        byte[] PushAssistRequestData ();
        byte[] PushAssistResponseData ();
    }
}

