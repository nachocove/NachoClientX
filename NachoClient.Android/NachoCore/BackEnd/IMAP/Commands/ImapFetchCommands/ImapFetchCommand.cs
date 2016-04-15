//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Text.RegularExpressions;
using NachoCore.Utils;
using System.Collections.Generic;
using System.Linq;
using MailKit;
using System.Diagnostics;
using NachoCore.Model;

namespace NachoCore.IMAP
{
    public partial class ImapFetchCommand : ImapCommand, ITransferProgress
    {
        private const string KImapSyncLogRedaction = "IMAP Sync Log Redaction";
        FetchKit FetchKit;

        public ImapFetchCommand (IBEContext beContext, FetchKit Fetchkit) : base (beContext)
        {
            FetchKit = Fetchkit;
        }
        public ImapFetchCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            pending.MarkDispatched ();
            PendingSingle = pending;
        }

        protected override Event ExecuteCommand ()
        {
            NcResult result = null;
            if (null != PendingSingle) {
                result = ProcessPending (PendingSingle);
            } else if (null != FetchKit) {
                result = ProcessFetchKit (FetchKit);
            } else {
                result = NcResult.Error ("Unknown operation");
            }

            if (result.isError ()) {
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand failed: {0}", result);
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, result);
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPBDYHRD0");
            } else {
                if (result.isInfo ()) {
                    PendingResolveApply ((pending) => {
                        pending.ResolveAsSuccess (BEContext.ProtoControl, result);
                    });
                }
                return Event.Create ((uint)SmEvt.E.Success, "IMAPBDYSUCC");
            }
        }

        private NcResult ProcessPending (McPending pending)
        {
            switch (pending.Operation) {
            case McPending.Operations.EmailBodyDownload:
                return FetchOneBody (pending);

            case McPending.Operations.AttachmentDownload:
                return FetchAttachment (pending);

            default:
                NcAssert.True (false, string.Format ("ItemOperations: inappropriate McPending Operation {0}", pending.Operation));
                return null; // make the compiler happy.
            }
        }

        private NcResult ProcessFetchKit (FetchKit fetchkit)
        {
            NcResult result = null;
            var fetchresult = FetchBodies (fetchkit);
            if (!fetchresult.isOK ()) {
                result = fetchresult;
            }
            var attachmentresult = FetchAttachments (fetchkit);
            if (!attachmentresult.isOK ()) {
                result = attachmentresult;
            }
            if (null != result) {
                return result;
            }
            return NcResult.OK ();
        }

        #region ITransferProgress implementation

        const int kProgressReportBatchingBytes = 1024 * 500; // half a meg.
        Stopwatch ReportWatch;
        long lastSize = 0;
        float lastElapsed = 0;
        long lastReportBytes = 0;
        bool Enabled = false;

        public void Report (long bytesTransferred, long totalSize)
        {
            if (!Enabled) {
                return;
            }

            if (null == ReportWatch) {
                ReportWatch = new Stopwatch ();
            }
            float percentTransferred = 0;
            if (totalSize > 0) {
                percentTransferred = ((float)bytesTransferred / (float)totalSize) * (float)100;
            }

            if (!ReportWatch.IsRunning) {
                ReportWatch.Start ();
                if (totalSize > 0) {
                    Log.Info (Log.LOG_IMAP, "{0} Download progress {1:0.0}%: bytesTransferred {2} totalSize {3}",
                        CmdNameWithAccount,
                        percentTransferred,
                        bytesTransferred,
                        totalSize);
                } else {
                    Log.Info (Log.LOG_IMAP, "{0} Download progress: bytesTransferred {1}",
                        CmdNameWithAccount,
                        bytesTransferred);
                }
            } else {
                var bytesSinceLastIteration = bytesTransferred - lastSize;
                float elapsed = (float)ReportWatch.ElapsedMilliseconds;
                float kSecSinceLast = ((float)bytesSinceLastIteration / 1024) / ((elapsed - lastElapsed) / 1000);
                float kSecTotal = ((float)bytesTransferred / 1024) / (elapsed / 1000);
                if (lastReportBytes == 0 || bytesTransferred - lastReportBytes > kProgressReportBatchingBytes) {
                    if (totalSize > 0) {
                        Log.Info (Log.LOG_IMAP, "{0} Download progress {1:0.0}%: bytesTransferred {2} totalSize {3} ({4:0.000} k/sec / {5:0.000} k/sec)",
                                         CmdNameWithAccount,
                                         percentTransferred,
                                         bytesTransferred,
                                         totalSize,
                                         kSecSinceLast,
                                         kSecTotal);
                    } else {
                        Log.Info (Log.LOG_IMAP, "{0} Download progress: bytesTransferred {2} ({3:0.000} k/sec / {4:0.000} k/sec)",
                            CmdNameWithAccount,
                            bytesTransferred,
                            kSecSinceLast,
                            kSecTotal);
                        
                    }
                    lastReportBytes = bytesTransferred;
                }
                //Console.WriteLine (logStr);
                lastElapsed = elapsed;
            }
            lastSize = bytesTransferred;
        }

        public void Report (long bytesTransferred)
        {
            Report (bytesTransferred, 0);
        }

        #endregion
    }
}

