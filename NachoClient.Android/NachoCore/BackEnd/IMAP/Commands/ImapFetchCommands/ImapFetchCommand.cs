﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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

        public ImapFetchCommand (IBEContext beContext, NcImapClient imap, FetchKit Fetchkit) : base (beContext, imap)
        {
            FetchKit = Fetchkit;

            SetupLogRedaction ();
        }
        public ImapFetchCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            pending.MarkDispached ();
            PendingSingle = pending;

            SetupLogRedaction ();
        }

        private void SetupLogRedaction ()
        {
            RedactProtocolLogFunc = RedactProtocolLog;
            NcCapture.AddKind (KImapSyncLogRedaction);
            var flags = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;

            //* 59 FETCH (UID 8721 MODSEQ (952121) BODY[1]<0> {500} ... )
            FetchCmdRegex = new Regex (@"^(?<star>\* )(?<num>\d+ )(?<cmd>FETCH )", flags);
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
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand failed: {0}", result.Message);
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

        private string lastIncompleteLine;
        private bool inFetch = false;
        private Regex FetchCmdRegex;

        ~ImapFetchCommand ()
        {
            if (!string.IsNullOrEmpty (lastIncompleteLine)) {
                Log.Error (Log.LOG_IMAP, "{0}: Line left dangling on exit: {1}", this.GetType ().Name, lastIncompleteLine);
            }
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            // FIXME Need to redact filenames in the BODYSTRUCTURE
            //  Example 1: "NAME" "config.bin"
            //  Example 2: "FILENAME" "config.bin"

            //* 374 FETCH (X-GM-THRID 1505999102887456107 X-GM-MSGID 1505999102887456107 UID 9089 RFC822.SIZE 7245 MODSEQ (967775) INTERNALDATE "07-Jul-2015 01:31:04 +0000" FLAGS () ENVELOPE ("Tue, 07 Jul 2015 01:31:03 +0000" "New voicemail from (727) 373-7491 at 7:29 PM" (("Google Voice" NIL "voice-noreply" "google.com")) (("Google Voice" NIL "voice-noreply" "google.com")) (("Google Voice" NIL "voice-noreply" "google.com")) ((NIL NIL "jan.vilhuber" "gmail.com")) NIL NIL NIL "<001a113445b2b78383051a3ef955@google.com>") BODYSTRUCTURE (("TEXT" "PLAIN" ("CHARSET" "UTF-8" "DELSP" "yes" "FORMAT" "flowed") NIL NIL "7BIT" 691 12 NIL NIL NIL)("TEXT" "HTML" ("CHARSET" "UTF-8") NIL NIL "QUOTED-PRINTABLE" 3724 47 NIL NIL NIL) "ALTERNATIVE" ("BOUNDARY" "001a113445b2b7836a051a3ef952") NIL NIL) BODY[HEADER.FIELDS (IMPORTANCE DKIM-SIGNATURE CONTENT-CLASS)] {559}
            //DKIM-Signature: v=1; a=rsa-sha256; c=relaxed/relaxed; d=google.com;
            //s=20120113; h=mime-version:message-id:date:subject:from:to:content-type;
            //bh=ILYmpZW+xQNvnLdp4jNld6UKgeldZUfXQwjaagKVl1w=;
            //b=kJb23brjhwqokoH3HgGOaO8+hSbldZz5IJ3+JVHkuzyk2hgEwSI3Be4X1sthGZHbBq
            //04pL2r9A/ea1GoI5sonR67hZ7UXufwrzYrSEmvPxziJxTbmopDurVPSW11oG3XWTn5pX
            //Ul3tyYSOKu7sXvmBOG5f99Nb7WFyp9dQQYxNKs/rD2cWwtfME0Lw3bjic7NvcU4Q0giE
            //fLtJnqTCtzX5A/gcYgxvICT1k/yXUlXIv0mfrWit1ZhyGmbNKYLiffG21IG0nHODWLPO
            //ZtuyJyUmJikvd9CLKPL86uRXfYjyKVaQ0n9oN35F5G4brJaDtN9eysLw7LWYObm1Hu0T U2og==
            //
            //)
            // Need to redact the entire Envelope and BODYSTRUCTURE filenames

            if (!isRequest) {
                using (var cap = NcCapture.CreateAndStart (KImapSyncLogRedaction)) {
                    char[] delimiterChars = { '\n' };
                    if (!string.IsNullOrEmpty (lastIncompleteLine)) {
                        logData = lastIncompleteLine + logData;
                        lastIncompleteLine = null;
                    }
                    var lines = new List<string> (logData.Split (delimiterChars));
                    if (!logData.EndsWith ("\n")) {
                        lastIncompleteLine = lines.Last ();
                        lines = lines.Take (lines.Count () - 1).ToList ();
                    }
                    List<string> result = new List<string> ();
                    foreach (var line in lines) {
                        if (FetchCmdRegex.IsMatch (line)) {
                            inFetch = true;
                            char[] space = { ' ' };
                            bool inEnvelope = false;
                            List<string> newLine = new List<string> ();
                            foreach (var token in line.Split (space)) {
                                switch (token) {
                                case "ENVELOPE":
                                    inEnvelope = true;
                                    newLine.Add (token + " ( REDACTED )");
                                    continue;

                                case "BODYSTRUCTURE":
                                    inEnvelope = false;
                                    newLine.Add (token);
                                    continue;

                                default:
                                    if (!inEnvelope) {
                                        newLine.Add (token);
                                    }
                                    break;
                                }
                            }
                            result.Add (string.Join (" ", newLine));
                        } else if (inFetch) {
                            if (line.StartsWith (")")) {
                                inFetch = false;
                                result.Add (line);
                            } else if ("\r" == line) {
                                result.Add (line);
                            } else {
                                result.Add ("REDACTED\r");
                            }
                            continue;
                        } else { // if (!inFetch)
                            result.Add (line);
                        }
                    }
                    var resultData = string.Join ("\n", result);
                    return resultData;
                }
            } else {
                return logData;
            }
        }

        #region ITransferProgress implementation

        const int kProgressReportBatchingBytes = 1024 * 500; // half a meg.
        Stopwatch ReportWatch;
        long lastSize = 0;
        float lastElapsed = 0;
        long lastReportBytes = 0;

        public void Report (long bytesTransferred, long totalSize)
        {
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
                        this.GetType ().Name,
                        percentTransferred,
                        bytesTransferred,
                        totalSize);
                } else {
                    Log.Info (Log.LOG_IMAP, "{0} Download progress: bytesTransferred {1}",
                        this.GetType ().Name,
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
                                         this.GetType ().Name,
                                         percentTransferred,
                                         bytesTransferred,
                                         totalSize,
                                         kSecSinceLast,
                                         kSecTotal);
                    } else {
                                Log.Info (Log.LOG_IMAP, "{0} Download progress: bytesTransferred {2} ({3:0.000} k/sec / {4:0.000} k/sec)",
                            this.GetType ().Name,
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

