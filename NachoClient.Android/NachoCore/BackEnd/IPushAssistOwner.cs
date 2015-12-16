//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;

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
        // Common elements
        public PushAssistProtocol Protocol;

        public int ResponseTimeoutMsec;

        public int WaitBeforeUseMsec;

        // ActiveSync Elements
        public string RequestUrl;

        public byte[] RequestData;

        public NcHttpHeaders RequestHeaders;

        public NcHttpHeaders ResponseHeaders;

        public byte[] ExpectedResponseData;

        public byte[] NoChangeResponseData;

        public Credentials MailServerCredentials;

        public bool IsSyncRequest;


        // IMAP elements
        public byte[] IMAPAuthenticationBlob;

        public string IMAPFolderName;

        public bool IMAPSupportsIdle;

        public bool IMAPSupportsExpunge;

        public int IMAPEXISTSCount;

        public uint IMAPUIDNEXT;
    }

    public interface IPushAssistOwner : IBEContext
    {
        PushAssistParameters PushAssistParameters ();
    }
}

