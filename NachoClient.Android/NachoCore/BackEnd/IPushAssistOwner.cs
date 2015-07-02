//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;

namespace NachoCore
{
    public enum PushAssistProtocol
    {
        UNKNOWN = 0,
        ACTIVE_SYNC = 1,
        IMAP = 2,
    };

    public class PushAssistParameters
    {
        public string RequestUrl;

        public byte[] RequestData;

        public HttpRequestHeaders RequestHeaders;

        public HttpContentHeaders ContentHeaders;

        public HttpResponseHeaders ResponseHeaders;

        public byte[] ExpectedResponseData;

        public byte[] NoChangeResponseData;

        public PushAssistProtocol Protocol;

        public byte[] CommandTerminator;

        public byte[] CommandAcknowledgement;

        public int ResponseTimeoutMsec;

        public int WaitBeforeUseMsec;
    }

    public interface IPushAssistOwner : IBEContext
    {
        PushAssistParameters PushAssistParameters ();
    }
}

